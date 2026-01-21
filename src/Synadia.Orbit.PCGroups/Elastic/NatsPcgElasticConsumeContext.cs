// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

namespace Synadia.Orbit.PCGroups.Elastic;

/// <summary>
/// Consume context for an elastic partitioned consumer group.
/// </summary>
/// <typeparam name="T">Message data type.</typeparam>
internal sealed class NatsPcgElasticConsumeContext<T> : IAsyncEnumerable<NatsPcgMsg<T>>, IAsyncDisposable
{
    private readonly INatsJSContext _js;
    private readonly string _streamName;
    private readonly string _consumerGroupName;
    private readonly string _memberName;
    private readonly INatsDeserialize<T>? _serializer;
    private readonly ConsumerConfig? _userConfig;

    private readonly CancellationTokenSource _cts = new();
    private readonly object _configLock = new();

    private NatsPcgElasticConfig _config;
    private INatsJSConsumer? _consumer;
    private Task? _watchTask;
    private volatile bool _stopped;
    private volatile bool _needsRecreate;
    private string[] _currentFilters = Array.Empty<string>();

    public NatsPcgElasticConsumeContext(
        INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        NatsPcgElasticConfig config,
        INatsDeserialize<T>? serializer,
        ConsumerConfig? userConfig)
    {
        _js = js;
        _streamName = streamName;
        _consumerGroupName = consumerGroupName;
        _memberName = memberName;
        _config = config;
        _serializer = serializer;
        _userConfig = userConfig;
    }

    public async ValueTask DisposeAsync()
    {
        _stopped = true;
        _cts.Cancel();

        if (_watchTask != null)
        {
            try
            {
                await _watchTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _cts.Dispose();
    }

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateOrGetConsumerAsync(cancellationToken).ConfigureAwait(false);
        _watchTask = Task.Run(() => WatchConfigLoopAsync(), CancellationToken.None);
    }

    public IAsyncEnumerator<NatsPcgMsg<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return ConsumeAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    private async IAsyncEnumerable<NatsPcgMsg<T>> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var linkedToken = linkedCts.Token;

        while (!_stopped && !linkedToken.IsCancellationRequested)
        {
            // Check if we need to recreate the consumer due to membership change
            if (_needsRecreate)
            {
                _needsRecreate = false;
                try
                {
                    await RecreateConsumerAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if (_stopped || linkedToken.IsCancellationRequested)
                    {
                        yield break;
                    }

                    // Backoff and retry
                    var delay = GetBackoffDelay();
                    try
                    {
                        await Task.Delay(delay, linkedToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        yield break;
                    }

                    continue;
                }
            }

            IAsyncEnumerable<INatsJSMsg<T>>? messages;

            try
            {
                if (_consumer == null)
                {
                    yield break;
                }

                var priorityGroup = new NatsJSPriorityGroupOpts
                {
                    Group = NatsPcgConstants.PriorityGroupName,
                };

                var consumeOpts = new NatsJSConsumeOpts
                {
                    MaxMsgs = 100,
                    Expires = NatsPcgConstants.PullTimeout,
                    IdleHeartbeat = TimeSpan.FromMilliseconds(NatsPcgConstants.PullTimeout.TotalMilliseconds / 2),
                    PriorityGroup = priorityGroup,
                };

                messages = _consumer.ConsumeAsync(_serializer, consumeOpts, linkedToken);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                // Consumer deleted - this is expected when membership changes
                if (!_stopped && !linkedToken.IsCancellationRequested)
                {
                    _needsRecreate = true;
                    continue;
                }

                yield break;
            }

            if (messages != null!)
            {
                IAsyncEnumerator<INatsJSMsg<T>>? enumerator = null;
                try
                {
                    enumerator = messages.GetAsyncEnumerator(linkedToken);
                    while (true)
                    {
                        bool hasNext;
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                        {
                            yield break;
                        }
                        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
                        {
                            // Consumer deleted - recreate
                            if (!_stopped && !linkedToken.IsCancellationRequested)
                            {
                                _needsRecreate = true;
                            }

                            break;
                        }

                        if (!hasNext)
                        {
                            break;
                        }

                        if (_stopped || linkedToken.IsCancellationRequested)
                        {
                            yield break;
                        }

                        // Check if we need to stop and recreate
                        if (_needsRecreate)
                        {
                            break;
                        }

                        var msg = enumerator.Current;
                        string strippedSubject = NatsPcgMsg<T>.StripPartitionPrefix(msg.Subject);
                        yield return new NatsPcgMsg<T>((NatsJSMsg<T>)msg, strippedSubject);
                    }
                }
                finally
                {
                    if (enumerator != null)
                    {
                        await enumerator.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private async Task CreateOrGetConsumerAsync(CancellationToken cancellationToken)
    {
        NatsPcgElasticConfig config;
        lock (_configLock)
        {
            config = _config;
        }

        string[] filters = NatsPcgPartitionDistributor.GeneratePartitionFilters(
            config.Members,
            config.MaxMembers,
            config.MemberMappings,
            _memberName);

        _currentFilters = filters;

        string workQueueStreamName = NatsPcgElasticExtensions.GetWorkQueueStreamName(_streamName, _consumerGroupName);

        // Each member gets its own consumer (named after the member)
        string consumerName = _memberName;

        var consumerConfig = new ConsumerConfig(consumerName)
        {
            AckPolicy = _userConfig?.AckPolicy ?? ConsumerConfigAckPolicy.Explicit,
            AckWait = _userConfig?.AckWait ?? NatsPcgConstants.AckWait,
            MaxDeliver = _userConfig?.MaxDeliver ?? -1,
            FilterSubjects = filters,
            PriorityGroups = new[] { NatsPcgConstants.PriorityGroupName },
            PriorityPolicy = ConsumerConfigPriorityPolicy.PinnedClient,
            PinnedTTL = NatsPcgConstants.ConsumerIdleTimeout,
            InactiveThreshold = NatsPcgConstants.ConsumerIdleTimeout,
        };

        try
        {
            _consumer = await _js.CreateOrUpdateConsumerAsync(workQueueStreamName, consumerConfig, cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 400)
        {
            // Consumer might already exist with different filter - try to get it
            _consumer = await _js.GetConsumerAsync(workQueueStreamName, consumerName, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RecreateConsumerAsync()
    {
        NatsPcgElasticConfig config;
        lock (_configLock)
        {
            config = _config;
        }

        // Check if still in membership
        if (!config.IsInMembership(_memberName))
        {
            _stopped = true;
            _cts.Cancel();
            return;
        }

        // Recalculate filters
        string[] filters = NatsPcgPartitionDistributor.GeneratePartitionFilters(
            config.Members,
            config.MaxMembers,
            config.MemberMappings,
            _memberName);

        // Only recreate if filters changed
        if (FiltersEqual(filters, _currentFilters))
        {
            return;
        }

        _currentFilters = filters;

        string workQueueStreamName = NatsPcgElasticExtensions.GetWorkQueueStreamName(_streamName, _consumerGroupName);

        // Each member gets its own consumer (named after the member)
        string consumerName = _memberName;

        var consumerConfig = new ConsumerConfig(consumerName)
        {
            AckPolicy = _userConfig?.AckPolicy ?? ConsumerConfigAckPolicy.Explicit,
            AckWait = _userConfig?.AckWait ?? NatsPcgConstants.AckWait,
            MaxDeliver = _userConfig?.MaxDeliver ?? -1,
            FilterSubjects = filters,
            PriorityGroups = new[] { NatsPcgConstants.PriorityGroupName },
            PriorityPolicy = ConsumerConfigPriorityPolicy.PinnedClient,
            PinnedTTL = NatsPcgConstants.ConsumerIdleTimeout,
            InactiveThreshold = NatsPcgConstants.ConsumerIdleTimeout,
        };

        _consumer = await _js.CreateOrUpdateConsumerAsync(workQueueStreamName, consumerConfig, _cts.Token).ConfigureAwait(false);
    }

    private async Task WatchConfigLoopAsync()
    {
        try
        {
            var kv = _js.Connection.CreateKeyValueStoreContext();

            while (!_stopped && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    INatsKVStore store;
                    try
                    {
                        store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, _cts.Token).ConfigureAwait(false);
                    }
                    catch (NatsJSApiException ex) when (ex.Error.Code == 404)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token).ConfigureAwait(false);
                        continue;
                    }

                    string key = NatsPcgElasticExtensions.GetKvKey(_streamName, _consumerGroupName);

                    var watchOpts = new NatsKVWatchOpts
                    {
                        UpdatesOnly = true,
                    };

                    await foreach (var entry in store.WatchAsync(key, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, opts: watchOpts, cancellationToken: _cts.Token).ConfigureAwait(false))
                    {
                        if (_stopped || _cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (entry.Operation == NatsKVOperation.Del || entry.Operation == NatsKVOperation.Purge)
                        {
                            // Config deleted - stop consuming
                            _stopped = true;
                            _cts.Cancel();
                            break;
                        }

                        if (entry.Value != null && entry.Revision != _config.Revision)
                        {
                            var newConfig = entry.Value with { Revision = entry.Revision };
                            lock (_configLock)
                            {
                                _config = newConfig;
                            }

                            // Check if we're still in membership
                            if (!newConfig.IsInMembership(_memberName))
                            {
                                _stopped = true;
                                _cts.Cancel();
                                break;
                            }

                            // Signal that we need to check if consumer needs recreation
                            _needsRecreate = true;
                        }
                    }
                }
                catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Retry watch after delay
                    if (!_stopped && !_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
        {
            // Expected when stopping
        }
    }

    private static bool FiltersEqual(string[] a, string[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    // ReSharper disable once StaticMemberInGenericType
    private static readonly Random s_random = new();

    private static TimeSpan GetBackoffDelay()
    {
        // Random delay between min and max reconnect delay
        int delayMs;
        lock (s_random)
        {
            delayMs = s_random.Next(
                (int)NatsPcgConstants.MinReconnectDelay.TotalMilliseconds,
                (int)NatsPcgConstants.MaxReconnectDelay.TotalMilliseconds);
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
