// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

namespace Synadia.Orbit.PCGroups.Static;

/// <summary>
/// Consume context for a static partitioned consumer group.
/// </summary>
/// <typeparam name="T">Message data type.</typeparam>
internal sealed class NatsPcgStaticConsumeContext<T> : IAsyncEnumerable<NatsPcgMsg<T>>, IAsyncDisposable
{
    private readonly INatsJSContext _js;
    private readonly string _streamName;
    private readonly string _consumerGroupName;
    private readonly string _memberName;
    private readonly INatsDeserialize<T>? _serializer;
    private readonly ConsumerConfig? _userConfig;

    private readonly CancellationTokenSource _cts = new();

    private NatsPcgStaticConfig _config;
    private INatsJSConsumer? _consumer;
    private Task? _watchTask;
    private volatile bool _stopped;

    public NatsPcgStaticConsumeContext(
        INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        NatsPcgStaticConfig config,
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
            IAsyncEnumerable<INatsJSMsg<T>>? messages = null;

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
                // Consumer deleted - this is expected when config changes
                yield break;
            }

            if (messages != null)
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
                            yield break;
                        }

                        if (!hasNext)
                        {
                            break;
                        }

                        if (_stopped || linkedToken.IsCancellationRequested)
                        {
                            yield break;
                        }

                        var msg = enumerator.Current;
                        var strippedSubject = NatsPcgMsg<T>.StripPartitionPrefix(msg.Subject);
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
        var filters = NatsPcgPartitionDistributor.GeneratePartitionFilters(
            _config.Members,
            _config.MaxMembers,
            _config.MemberMappings,
            _memberName);

        // Apply config filter to partition filters
        var finalFilters = ApplyFilter(filters, _config.Filter);

        var consumerName = NatsPcgStaticExtensions.GetConsumerName(_consumerGroupName);

        var consumerConfig = new ConsumerConfig(consumerName)
        {
            DurableName = consumerName,
            AckPolicy = _userConfig?.AckPolicy ?? ConsumerConfigAckPolicy.Explicit,
            AckWait = _userConfig?.AckWait ?? NatsPcgConstants.AckWait,
            MaxDeliver = _userConfig?.MaxDeliver ?? -1,
            FilterSubjects = finalFilters,
            PriorityGroups = new[] { NatsPcgConstants.PriorityGroupName },
            PriorityPolicy = ConsumerConfigPriorityPolicy.PinnedClient,
            PinnedTTL = NatsPcgConstants.ConsumerIdleTimeout,
            InactiveThreshold = NatsPcgConstants.ConsumerIdleTimeout,
        };

        try
        {
            _consumer = await _js.CreateOrUpdateConsumerAsync(_streamName, consumerConfig, cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 400)
        {
            // Consumer might already exist with different filter - try to get it
            _consumer = await _js.GetConsumerAsync(_streamName, consumerName, cancellationToken).ConfigureAwait(false);
        }
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
                        store = await kv.GetStoreAsync(NatsPcgConstants.StaticKvBucket, _cts.Token).ConfigureAwait(false);
                    }
                    catch (NatsJSApiException ex) when (ex.Error.Code == 404)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token).ConfigureAwait(false);
                        continue;
                    }

                    var key = NatsPcgStaticExtensions.GetKvKey(_streamName, _consumerGroupName);

                    var watchOpts = new NatsKVWatchOpts
                    {
                        UpdatesOnly = true,
                    };

                    await foreach (var entry in store.WatchAsync(key, serializer: NatsPcgJsonSerializer<NatsPcgStaticConfig>.Default, opts: watchOpts, cancellationToken: _cts.Token).ConfigureAwait(false))
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
                            _config = entry.Value with { Revision = entry.Revision };

                            // Check if we're still in membership
                            if (!_config.IsInMembership(_memberName))
                            {
                                _stopped = true;
                                _cts.Cancel();
                                break;
                            }
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

    private static string[] ApplyFilter(string[] partitionFilters, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return partitionFilters;
        }

        var result = new string[partitionFilters.Length];
        for (int i = 0; i < partitionFilters.Length; i++)
        {
            // Replace .> with .{filter}
            var prefix = partitionFilters[i].Substring(0, partitionFilters[i].Length - 1); // Remove ">"
            result[i] = $"{prefix}{filter}";
        }

        return result;
    }
}
