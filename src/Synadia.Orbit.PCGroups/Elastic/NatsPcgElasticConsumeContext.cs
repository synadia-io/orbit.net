// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
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
internal sealed class NatsPcgElasticConsumeContext<T> : INatsPcgConsumeContext
{
    private readonly INatsJSContext _js;
    private readonly string _streamName;
    private readonly string _consumerGroupName;
    private readonly string _memberName;
    private readonly Func<NatsPcgMsg<T>, CancellationToken, ValueTask> _messageHandler;
    private readonly INatsDeserialize<T>? _serializer;
    private readonly ConsumerConfig? _userConfig;

    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<Exception?> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _pinId;
    private readonly object _configLock = new();

    private NatsPcgElasticConfig _config;
    private INatsJSConsumer? _consumer;
    private Task? _consumeTask;
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
        Func<NatsPcgMsg<T>, CancellationToken, ValueTask> messageHandler,
        INatsDeserialize<T>? serializer,
        ConsumerConfig? userConfig)
    {
        _js = js;
        _streamName = streamName;
        _consumerGroupName = consumerGroupName;
        _memberName = memberName;
        _config = config;
        _messageHandler = messageHandler;
        _serializer = serializer;
        _userConfig = userConfig;
        _pinId = $"{memberName}-{Guid.NewGuid():N}";
    }

    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _cts.Cancel();
    }

    public Task<Exception?> WaitAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled)
        {
            return WaitWithCancellationAsync(cancellationToken);
        }

        return _completionSource.Task;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

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

        _consumeTask = Task.Run(() => ConsumeLoopAsync(), CancellationToken.None);
        _watchTask = Task.Run(() => WatchConfigLoopAsync(), CancellationToken.None);
    }

    private async Task CreateOrGetConsumerAsync(CancellationToken cancellationToken)
    {
        NatsPcgElasticConfig config;
        lock (_configLock)
        {
            config = _config;
        }

        var filters = NatsPcgPartitionDistributor.GeneratePartitionFilters(
            config.Members,
            config.MaxMembers,
            config.MemberMappings,
            _memberName);

        _currentFilters = filters;

        var workQueueStreamName = NatsPcgElasticExtensions.GetWorkQueueStreamName(_streamName, _consumerGroupName);
        var consumerName = NatsPcgElasticExtensions.GetConsumerName(_consumerGroupName);

        var consumerConfig = new ConsumerConfig(consumerName)
        {
            DurableName = consumerName,
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

    private async Task ConsumeLoopAsync()
    {
        Exception? error = null;

        try
        {
            while (!_stopped && !_cts.Token.IsCancellationRequested)
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
                        if (_stopped || _cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        // Backoff and retry
                        var delay = GetBackoffDelay();
                        try
                        {
                            await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        continue;
                    }
                }

                try
                {
                    await ConsumeMessagesAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (NatsJSApiException ex) when (ex.Error.Code == 404)
                {
                    // Consumer deleted - this is expected when membership changes
                    if (!_stopped && !_cts.Token.IsCancellationRequested)
                    {
                        _needsRecreate = true;
                    }
                }
                catch (Exception ex)
                {
                    // Backoff and retry
                    if (!_stopped && !_cts.Token.IsCancellationRequested)
                    {
                        var delay = GetBackoffDelay();
                        try
                        {
                            await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                    else
                    {
                        error = ex;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            _completionSource.TrySetResult(error);
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
            Stop();
            return;
        }

        // Recalculate filters
        var filters = NatsPcgPartitionDistributor.GeneratePartitionFilters(
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

        var workQueueStreamName = NatsPcgElasticExtensions.GetWorkQueueStreamName(_streamName, _consumerGroupName);
        var consumerName = NatsPcgElasticExtensions.GetConsumerName(_consumerGroupName);

        var consumerConfig = new ConsumerConfig(consumerName)
        {
            DurableName = consumerName,
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

    private async Task ConsumeMessagesAsync()
    {
        if (_consumer == null)
        {
            return;
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

        await foreach (var msg in _consumer.ConsumeAsync<T>(_serializer, consumeOpts, _cts.Token).ConfigureAwait(false))
        {
            if (_stopped || _cts.Token.IsCancellationRequested)
            {
                break;
            }

            // Check if we need to stop and recreate
            if (_needsRecreate)
            {
                break;
            }

            // Strip partition prefix from subject
            var strippedSubject = NatsPcgMsg<T>.StripPartitionPrefix(msg.Subject);
            var pcMsg = new NatsPcgMsg<T>((NatsJSMsg<T>)msg, strippedSubject);

            try
            {
                await _messageHandler(pcMsg, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
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
                        store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, _cts.Token).ConfigureAwait(false);
                    }
                    catch (NatsJSApiException ex) when (ex.Error.Code == 404)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token).ConfigureAwait(false);
                        continue;
                    }

                    var key = NatsPcgElasticExtensions.GetKvKey(_streamName, _consumerGroupName);

                    var watchOpts = new NatsKVWatchOpts
                    {
                        UpdatesOnly = true,
                    };

                    await foreach (var entry in store.WatchAsync<string>(key, opts: watchOpts, cancellationToken: _cts.Token).ConfigureAwait(false))
                    {
                        if (_stopped || _cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (entry.Operation == NatsKVOperation.Del || entry.Operation == NatsKVOperation.Purge)
                        {
                            // Config deleted - stop consuming
                            Stop();
                            break;
                        }

                        if (entry.Value != null)
                        {
                            var newConfig = JsonSerializer.Deserialize(entry.Value, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);
                            if (newConfig != null && entry.Revision != _config.Revision)
                            {
                                lock (_configLock)
                                {
                                    _config = newConfig with { Revision = entry.Revision };
                                }

                                // Check if we're still in membership
                                if (!newConfig.IsInMembership(_memberName))
                                {
                                    Stop();
                                    break;
                                }

                                // Signal that we need to check if consumer needs recreation
                                _needsRecreate = true;
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

    private async Task<Exception?> WaitWithCancellationAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        var completedTask = await Task.WhenAny(_completionSource.Task, tcs.Task).ConfigureAwait(false);

        if (completedTask == tcs.Task)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return await _completionSource.Task.ConfigureAwait(false);
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
