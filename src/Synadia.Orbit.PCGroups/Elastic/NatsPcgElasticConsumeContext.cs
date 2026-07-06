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
    private readonly bool _drainOnCancel;

    private readonly CancellationTokenSource _cts = new();
    private readonly object _configLock = new();

    private NatsPcgElasticConfig _config;
    private INatsJSConsumer? _consumer;
    private Task? _watchTask;
    private volatile bool _stopped;
    private volatile bool _needsRecreate;
    private volatile string? _currentPinnedId;
    private volatile CancellationTokenSource? _recreateCts;
    private string[] _currentFilters = Array.Empty<string>();

    public NatsPcgElasticConsumeContext(
        INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        NatsPcgElasticConfig config,
        INatsDeserialize<T>? serializer,
        ConsumerConfig? userConfig,
        bool drainOnCancel)
    {
        _js = js;
        _streamName = streamName;
        _consumerGroupName = consumerGroupName;
        _memberName = memberName;
        _config = config;
        _serializer = serializer;
        _userConfig = userConfig;
        _drainOnCancel = drainOnCancel;
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

            IAsyncEnumerable<INatsJSMsg<T>> messages;
            CancellationTokenSource? consumeCts = null;

            try
            {
                if (_consumer == null)
                {
                    // Self-heal: if consumer is null but we're in membership, try to rejoin
                    NatsPcgElasticConfig config;
                    lock (_configLock)
                    {
                        config = _config;
                    }

                    if (config.IsInMembership(_memberName))
                    {
                        try
                        {
                            await Task.Delay(NatsPcgConstants.SelfHealInterval, linkedToken).ConfigureAwait(false);
                            await CreateOrGetConsumerAsync(linkedToken).ConfigureAwait(false);
                            continue;
                        }
                        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                        {
                            yield break;
                        }
                        catch
                        {
                            // Failed to create consumer, will retry after backoff
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
                    DrainOnCancel = _drainOnCancel,
                };

                // Linked source so a membership change can interrupt an idle consume
                // without tearing down the caller's or the context's cancellation.
                consumeCts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
                _recreateCts = consumeCts;

                messages = _consumer.ConsumeAsync(_serializer, consumeOpts, consumeCts.Token);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
                consumeCts?.Dispose();
                yield break;
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                consumeCts?.Dispose();

                // Consumer deleted - this is expected when membership changes
                if (!_stopped && !linkedToken.IsCancellationRequested)
                {
                    _needsRecreate = true;
                    continue;
                }

                yield break;
            }

            IAsyncEnumerator<INatsJSMsg<T>>? enumerator = null;
            try
            {
                // consumeCts is always assigned before messages is set above; the catch
                // blocks all exit, so reaching here guarantees it is non-null.
                enumerator = messages.GetAsyncEnumerator(consumeCts!.Token);
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
                    catch (OperationCanceledException)
                    {
                        // Membership change interrupted an idle consume; re-evaluate
                        break;
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
                        // Underlying consume completed. When cancelled with DrainOnCancel,
                        // the client has already flushed buffered messages, so finish here
                        // instead of looping back to recreate the consumer.
                        if (linkedToken.IsCancellationRequested || _js.Connection.Opts.DrainSubscriptionsOnDispose)
                        {
                            yield break;
                        }

                        break;
                    }

                    // While draining on cancel keep yielding the buffered messages the
                    // client hands us; the loop ends when MoveNextAsync reports completion.
                    if (!_drainOnCancel && (_stopped || linkedToken.IsCancellationRequested))
                    {
                        yield break;
                    }

                    // Check if we need to stop and recreate. Skip while actively
                    // draining on cancel so the buffered messages finish first
                    // instead of being cut off by a mid-drain membership change.
                    if (_needsRecreate && !(_drainOnCancel && linkedToken.IsCancellationRequested))
                    {
                        break;
                    }

                    var msg = enumerator.Current;
                    TrackPinnedId(msg);
                    string strippedSubject = NatsPcgMsg<T>.StripPartitionPrefix(msg.Subject);
                    yield return new NatsPcgMsg<T>((NatsJSMsg<T>)msg, strippedSubject);
                }
            }
            finally
            {
                _recreateCts = null;

                if (enumerator != null)
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }

                consumeCts?.Dispose();
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

        string[] filters = GenerateFiltersForMember(config, _memberName);

        _currentFilters = filters;

        string workQueueStreamName = NatsPcgElasticExtensions.GetWorkQueueStreamName(_streamName, _consumerGroupName);

        var consumerConfig = BuildConsumerConfig(filters);

        try
        {
            _consumer = await _js.CreateOrUpdateConsumerAsync(workQueueStreamName, consumerConfig, cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (Array.IndexOf(NatsPcgConstants.ConsumerCreateConflictErrCodes, ex.Error.ErrCode) >= 0)
        {
            // Consumer might already exist with different filter - try to get it. Match the
            // specific conflict codes rather than any HTTP 400 so a genuine bad-request is not
            // swallowed and re-surfaced as a misleading 404 from the get.
            _consumer = await _js.GetConsumerAsync(workQueueStreamName, _memberName, cancellationToken).ConfigureAwait(false);
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
        string[] filters = GenerateFiltersForMember(config, _memberName);

        // Only recreate if filters changed. Unchanged filters mean this member's
        // partition set is identical, so the existing consumer is still correct.
        if (FiltersEqual(filters, _currentFilters))
        {
            return;
        }

        // Just updating the filters on the existing consumer is not enough: a newly
        // assigned partition may have messages at stream sequences the consumer has
        // already advanced past, which would be silently skipped. The consumer must be
        // deleted and recreated so it restarts from the correct position. Only the pinned
        // member performs the delete; others back off so the pinned member wins the race
        // and to avoid flapping. A consumer that is briefly missing or fails the
        // not-unique check is a normal rebalance condition handled by retry/self-heal.
        string workQueueStreamName = NatsPcgElasticExtensions.GetWorkQueueStreamName(_streamName, _consumerGroupName);

        bool isPinned = await IsCurrentlyPinnedAsync().ConfigureAwait(false);

        if (isPinned)
        {
            try
            {
                await _js.DeleteConsumerAsync(workQueueStreamName, _memberName, _cts.Token).ConfigureAwait(false);
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                // Already gone - normal during rebalance
            }

            // The consumer is gone; drop the stale reference so that if the recreate
            // below throws, the consume loop's self-heal path rejoins instead of
            // consuming a deleted consumer and waiting for a 404 to retry.
            _consumer = null;
        }
        else
        {
            // Give the pinned member a chance to delete and recreate first
            await Task.Delay(GetMembershipBackoffDelay(), _cts.Token).ConfigureAwait(false);
        }

        _consumer = await TryCreateConsumerAsync(workQueueStreamName, filters).ConfigureAwait(false);
        _currentFilters = filters;
    }

    private ConsumerConfig BuildConsumerConfig(string[] filters)
    {
        // Each member gets its own consumer (named after the member)
        return new ConsumerConfig(_memberName)
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
    }

    // Mirrors the Go tryCreateConsumer: create with the desired config; if a consumer
    // already exists (possibly with stale filters left by another member), delete it and
    // create again so we end up with the correct position and filters.
    private async Task<INatsJSConsumer> TryCreateConsumerAsync(string workQueueStreamName, string[] filters)
    {
        var consumerConfig = BuildConsumerConfig(filters);

        try
        {
            return await _js.CreateConsumerAsync(workQueueStreamName, consumerConfig, _cts.Token).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (Array.IndexOf(NatsPcgConstants.ConsumerCreateConflictErrCodes, ex.Error.ErrCode) >= 0)
        {
            // A consumer with this name already exists (stale filters left by us or
            // another member). Delete it and create again with the desired config.
            try
            {
                await _js.DeleteConsumerAsync(workQueueStreamName, _memberName, _cts.Token).ConfigureAwait(false);
            }
            catch (NatsJSApiException delEx) when (delEx.Error.Code == 404)
            {
                // Already gone - fall through to recreate
            }

            // Second attempt. If another member races us and recreates the consumer in
            // the window between delete and create, this throws; the outer consume loop
            // catches it, backs off, and retries the recreate.
            return await _js.CreateConsumerAsync(workQueueStreamName, consumerConfig, _cts.Token).ConfigureAwait(false);
        }
    }

    // Determines whether this member currently holds the pinned client slot, by comparing
    // the pinned id last seen on a delivered message against the consumer's reported state.
    private async Task<bool> IsCurrentlyPinnedAsync()
    {
        string? pinnedId = _currentPinnedId;
        if (string.IsNullOrEmpty(pinnedId) || _consumer == null)
        {
            return false;
        }

        try
        {
            await _consumer.RefreshAsync(_cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // The consumer may not exist yet; treat as not pinned (matches Go)
            return false;
        }

        var groups = _consumer.Info.PriorityGroups;
        if (groups == null)
        {
            return false;
        }

        foreach (var group in groups)
        {
            if (group.Group == NatsPcgConstants.PriorityGroupName && group.PinnedClientId == pinnedId)
            {
                return true;
            }
        }

        return false;
    }

    private void TrackPinnedId(INatsJSMsg<T> msg)
    {
        if (msg.Headers != null && msg.Headers.TryGetValue(NatsPcgConstants.PinIdHeader, out var pinId) && pinId.Count > 0)
        {
            _currentPinnedId = pinId.ToString();
        }
    }

    // Flags a membership-driven recreate and interrupts an idle/blocked consume so the
    // consume loop re-evaluates promptly instead of waiting for the next message.
    private void SignalRecreate()
    {
        _needsRecreate = true;
        try
        {
            _recreateCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Consume loop already moved on; the flag will be picked up at the loop top
        }
    }

    private static string[] GenerateFiltersForMember(NatsPcgElasticConfig config, string memberName)
    {
        var allFilters = new List<string>();
        if (config.PartitioningFilters.Length > 0)
        {
            foreach (var pf in config.PartitioningFilters)
            {
                allFilters.AddRange(NatsPcgPartitionDistributor.GeneratePartitionFilters(
                    config.Members,
                    config.MaxMembers,
                    config.MemberMappings,
                    memberName,
                    pf.Filter));
            }
        }
        else
        {
            allFilters.AddRange(NatsPcgPartitionDistributor.GeneratePartitionFilters(
                config.Members,
                config.MaxMembers,
                config.MemberMappings,
                memberName,
                ">"));
        }

        return allFilters.ToArray();
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

                            // Signal that we need to check if consumer needs recreation,
                            // interrupting an idle consume so the change is picked up promptly
                            SignalRecreate();
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

    // Ordered element-wise comparison. This relies on GeneratePartitionFilters producing
    // filters in a stable order for a given partition set; if that ever stops holding, a
    // reordered-but-equal set would trigger a spurious delete-and-recreate.
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

    private static TimeSpan GetMembershipBackoffDelay()
    {
        int delayMs;
        lock (s_random)
        {
            delayMs = s_random.Next(
                (int)NatsPcgConstants.MembershipBackoffMin.TotalMilliseconds,
                (int)NatsPcgConstants.MembershipBackoffMax.TotalMilliseconds);
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
