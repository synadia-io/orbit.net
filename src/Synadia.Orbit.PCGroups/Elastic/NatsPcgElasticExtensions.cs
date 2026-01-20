// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

namespace Synadia.Orbit.PCGroups.Elastic;

/// <summary>
/// Elastic partitioned consumer group operations.
/// Elastic groups allow dynamic membership changes at runtime.
/// </summary>
public static class NatsPcgElasticExtensions
{
    /// <summary>
    /// Creates an elastic consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="maxNumMembers">Maximum number of members (also the number of partitions).</param>
    /// <param name="filter">Subject filter for the consumer group.</param>
    /// <param name="partitioningWildcards">Wildcard positions for partitioning (1-indexed).</param>
    /// <param name="maxBufferedMessages">Optional maximum number of buffered messages.</param>
    /// <param name="maxBufferedBytes">Optional maximum bytes of buffered messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created configuration.</returns>
    public static async Task<NatsPcgElasticConfig> CreatePcgElasticAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        uint maxNumMembers,
        string filter,
        int[] partitioningWildcards,
        long? maxBufferedMessages = null,
        long? maxBufferedBytes = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfig(maxNumMembers, filter, partitioningWildcards);

        var config = new NatsPcgElasticConfig
        {
            MaxMembers = maxNumMembers,
            Filter = filter,
            PartitioningWildcards = partitioningWildcards,
            MaxBufferedMsgs = maxBufferedMessages,
            MaxBufferedBytes = maxBufferedBytes,
        };

        // Create the work-queue stream that sources from the original stream
        await CreateWorkQueueStreamAsync(js, streamName, consumerGroupName, config, cancellationToken).ConfigureAwait(false);

        // Store config in KV
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await GetOrCreateKvStoreAsync(kv, cancellationToken).ConfigureAwait(false);

        var key = GetKvKey(streamName, consumerGroupName);
        var json = JsonSerializer.Serialize(config, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);

        var revision = await store.CreateAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);

        return config with { Revision = revision };
    }

    /// <summary>
    /// Gets the configuration for an elastic consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configuration.</returns>
    public static async Task<NatsPcgElasticConfig> GetPcgElasticConfigAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, cancellationToken).ConfigureAwait(false);

        var key = GetKvKey(streamName, consumerGroupName);
        var entry = await store.GetEntryAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (entry.Value == null)
        {
            throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found for stream '{streamName}'");
        }

        var config = JsonSerializer.Deserialize(entry.Value, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);
        if (config == null)
        {
            throw new NatsPcgException($"Failed to deserialize config for consumer group '{consumerGroupName}'");
        }

        return config with { Revision = entry.Revision };
    }

    /// <summary>
    /// Starts consuming messages from an elastic consumer group.
    /// </summary>
    /// <typeparam name="T">Message data type.</typeparam>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberName">Name of this member.</param>
    /// <param name="messageHandler">Handler for received messages.</param>
    /// <param name="serializer">Optional deserializer for message data.</param>
    /// <param name="config">Optional consumer configuration overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A consume context for controlling the consumer.</returns>
    public static async Task<INatsPcgConsumeContext> ConsumePcgElasticAsync<T>(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        Func<NatsPcgMsg<T>, CancellationToken, ValueTask> messageHandler,
        INatsDeserialize<T>? serializer = null,
        ConsumerConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var groupConfig = await GetPcgElasticConfigAsync(js, streamName, consumerGroupName, cancellationToken).ConfigureAwait(false);

        if (!groupConfig.IsInMembership(memberName))
        {
            throw new NatsPcgMembershipException($"Member '{memberName}' is not in membership for consumer group '{consumerGroupName}'");
        }

        // Elastic groups require explicit ack
        if (config?.AckPolicy == ConsumerConfigAckPolicy.None)
        {
            throw new NatsPcgConfigurationException("Elastic consumer groups require explicit acknowledgment policy");
        }

        var context = new NatsPcgElasticConsumeContext<T>(
            js,
            streamName,
            consumerGroupName,
            memberName,
            groupConfig,
            messageHandler,
            serializer,
            config);

        await context.StartAsync(cancellationToken).ConfigureAwait(false);

        return context;
    }

    /// <summary>
    /// Deletes an elastic consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DeletePcgElasticAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        CancellationToken cancellationToken = default)
    {
        // Delete KV entry
        var kv = js.Connection.CreateKeyValueStoreContext();
        try
        {
            var store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, cancellationToken).ConfigureAwait(false);
            var key = GetKvKey(streamName, consumerGroupName);
            await store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            // KV bucket doesn't exist - ignore
        }

        // Delete work-queue stream
        var workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);
        try
        {
            await js.DeleteStreamAsync(workQueueStreamName, cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            // Stream already deleted - ignore
        }
    }

    /// <summary>
    /// Lists all elastic consumer groups for a stream.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consumer group names.</returns>
    public static async IAsyncEnumerable<string> ListPcgElasticAsync(
        this INatsJSContext js,
        string streamName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();

        INatsKVStore store;
        try
        {
            store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            yield break;
        }

        var prefix = $"{streamName}.";

        await foreach (var key in store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return key.Substring(prefix.Length);
            }
        }
    }

    /// <summary>
    /// Lists active members for an elastic consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active member names.</returns>
    public static async IAsyncEnumerable<string> ListPcgElasticActiveMembersAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);
        var consumerName = GetConsumerName(consumerGroupName);

        ConsumerInfo info;
        try
        {
            var consumer = await js.GetConsumerAsync(workQueueStreamName, consumerName, cancellationToken).ConfigureAwait(false);
            info = consumer.Info;
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            yield break;
        }

        if (info.PriorityGroups != null)
        {
            foreach (var group in info.PriorityGroups)
            {
                if (group.PinnedClientId != null)
                {
                    // The member name is embedded in the pinned client ID
                    // Format: {member}-{guid}
                    var dashIndex = group.PinnedClientId.LastIndexOf('-');
                    if (dashIndex > 0)
                    {
                        yield return group.PinnedClientId.Substring(0, dashIndex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds members to an elastic consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberNamesToAdd">Member names to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated member list.</returns>
    public static async Task<string[]> AddPcgElasticMembersAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string[] memberNamesToAdd,
        CancellationToken cancellationToken = default)
    {
        return await UpdateMembersAsync(js, streamName, consumerGroupName, memberNamesToAdd, add: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes members from an elastic consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberNamesToDrop">Member names to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated member list.</returns>
    public static async Task<string[]> DeletePcgElasticMembersAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string[] memberNamesToDrop,
        CancellationToken cancellationToken = default)
    {
        return await UpdateMembersAsync(js, streamName, consumerGroupName, memberNamesToDrop, add: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets custom member-to-partition mappings.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberMappings">Member-to-partition mappings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SetPcgElasticMemberMappingsAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        NatsPcgMemberMapping[] memberMappings,
        CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, cancellationToken).ConfigureAwait(false);
        var key = GetKvKey(streamName, consumerGroupName);

        // Retry loop for optimistic concurrency
        const int maxRetries = 5;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            var entry = await store.GetEntryAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value == null)
            {
                throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found");
            }

            var config = JsonSerializer.Deserialize(entry.Value, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);
            if (config == null)
            {
                throw new NatsPcgException($"Failed to deserialize config for consumer group '{consumerGroupName}'");
            }

            // Validate mappings
            foreach (var mapping in memberMappings)
            {
                foreach (var partition in mapping.Partitions)
                {
                    if (partition < 0 || partition >= config.MaxMembers)
                    {
                        throw new NatsPcgConfigurationException($"Partition {partition} is out of range [0, {config.MaxMembers})");
                    }
                }
            }

            var updatedConfig = config with
            {
                Members = null,
                MemberMappings = memberMappings,
            };

            var json = JsonSerializer.Serialize(updatedConfig, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);

            try
            {
                await store.UpdateAsync(key, json, entry.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (NatsKVWrongLastRevisionException)
            {
                // Config changed - retry
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50 * (retry + 1)), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw new NatsPcgException("Failed to update config after maximum retries");
    }

    /// <summary>
    /// Deletes member mappings, reverting to auto-distribution.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DeletePcgElasticMemberMappingsAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, cancellationToken).ConfigureAwait(false);
        var key = GetKvKey(streamName, consumerGroupName);

        // Retry loop for optimistic concurrency
        const int maxRetries = 5;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            var entry = await store.GetEntryAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value == null)
            {
                throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found");
            }

            var config = JsonSerializer.Deserialize(entry.Value, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);
            if (config == null)
            {
                throw new NatsPcgException($"Failed to deserialize config for consumer group '{consumerGroupName}'");
            }

            var updatedConfig = config with { MemberMappings = null };
            var json = JsonSerializer.Serialize(updatedConfig, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);

            try
            {
                await store.UpdateAsync(key, json, entry.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (NatsKVWrongLastRevisionException)
            {
                // Config changed - retry
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50 * (retry + 1)), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw new NatsPcgException("Failed to update config after maximum retries");
    }

    /// <summary>
    /// Checks if a member is in membership and currently active.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberName">Member name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (IsInMembership, IsActive).</returns>
    public static async Task<(bool IsInMembership, bool IsActive)> IsInPcgElasticMembershipAndActiveAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        var config = await GetPcgElasticConfigAsync(js, streamName, consumerGroupName, cancellationToken).ConfigureAwait(false);
        var isInMembership = config.IsInMembership(memberName);

        if (!isInMembership)
        {
            return (false, false);
        }

        // Check if active
        var isActive = false;
        await foreach (var activeMember in ListPcgElasticActiveMembersAsync(js, streamName, consumerGroupName, cancellationToken).ConfigureAwait(false))
        {
            if (activeMember == memberName)
            {
                isActive = true;
                break;
            }
        }

        return (isInMembership, isActive);
    }

    /// <summary>
    /// Gets partition filters for a member based on config.
    /// </summary>
    /// <param name="config">Consumer group configuration.</param>
    /// <param name="memberName">Member name.</param>
    /// <returns>Array of partition filters.</returns>
    public static string[] GetPcgElasticPartitionFilters(this NatsPcgElasticConfig config, string memberName)
    {
        return NatsPcgPartitionDistributor.GeneratePartitionFilters(
            config.Members,
            config.MaxMembers,
            config.MemberMappings,
            memberName);
    }

    /// <summary>
    /// Forces a member to step down from the consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberName">Name of the member to step down.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task PcgElasticMemberStepDownAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        var workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);
        var consumerName = GetConsumerName(consumerGroupName);
        var consumer = await js.GetConsumerAsync(workQueueStreamName, consumerName, cancellationToken).ConfigureAwait(false);

        await consumer.UnpinAsync(NatsPcgConstants.PriorityGroupName, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateConfig(uint maxNumMembers, string filter, int[] partitioningWildcards)
    {
        if (maxNumMembers == 0)
        {
            throw new NatsPcgConfigurationException("maxNumMembers must be greater than 0");
        }

        if (string.IsNullOrEmpty(filter))
        {
            throw new NatsPcgConfigurationException("filter is required for elastic consumer groups");
        }

        if (partitioningWildcards == null || partitioningWildcards.Length == 0)
        {
            throw new NatsPcgConfigurationException("partitioningWildcards must contain at least one element");
        }

        // Validate wildcard positions are valid (1-indexed)
        foreach (var pos in partitioningWildcards)
        {
            if (pos < 1)
            {
                throw new NatsPcgConfigurationException($"Wildcard position {pos} is invalid (must be >= 1)");
            }
        }
    }

    private static async Task CreateWorkQueueStreamAsync(
        INatsJSContext js,
        string streamName,
        string consumerGroupName,
        NatsPcgElasticConfig config,
        CancellationToken cancellationToken)
    {
        var workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);

        // Build subject transform: add partition prefix based on wildcards
        // Transform syntax: {{Partition(numPartitions, wildcardIndexes...)}}
        var wildcardStr = string.Join(",", config.PartitioningWildcards);
        var subjectTransform = $"{{{{Partition({config.MaxMembers},{wildcardStr})}}}}.${{subject}}";

        var sources = new List<StreamSource>
        {
            new()
            {
                Name = streamName,
                SubjectTransforms = new List<SubjectTransform>
                {
                    new()
                    {
                        Src = config.Filter,
                        Dest = subjectTransform,
                    },
                },
            },
        };

        var streamConfig = new StreamConfig
        {
            Name = workQueueStreamName,
            Retention = StreamConfigRetention.Workqueue,
            Sources = sources,
        };

        if (config.MaxBufferedMsgs.HasValue)
        {
            streamConfig.MaxMsgs = config.MaxBufferedMsgs.Value;
        }

        if (config.MaxBufferedBytes.HasValue)
        {
            streamConfig.MaxBytes = config.MaxBufferedBytes.Value;
        }

        await js.CreateStreamAsync(streamConfig, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string[]> UpdateMembersAsync(
        INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string[] memberNames,
        bool add,
        CancellationToken cancellationToken)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await kv.GetStoreAsync(NatsPcgConstants.ElasticKvBucket, cancellationToken).ConfigureAwait(false);
        var key = GetKvKey(streamName, consumerGroupName);

        // Retry loop for optimistic concurrency
        const int maxRetries = 5;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            var entry = await store.GetEntryAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value == null)
            {
                throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found");
            }

            var config = JsonSerializer.Deserialize(entry.Value, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);
            if (config == null)
            {
                throw new NatsPcgException($"Failed to deserialize config for consumer group '{consumerGroupName}'");
            }

            if (config.MemberMappings != null)
            {
                throw new NatsPcgConfigurationException("Cannot modify members when member mappings are defined");
            }

            var currentMembers = config.Members != null
                ? new HashSet<string>(config.Members, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            if (add)
            {
                foreach (var member in memberNames)
                {
                    currentMembers.Add(member);
                }
            }
            else
            {
                foreach (var member in memberNames)
                {
                    currentMembers.Remove(member);
                }
            }

            var updatedMembers = currentMembers.Count > 0 ? currentMembers.ToArray() : null;
            var updatedConfig = config with { Members = updatedMembers };
            var json = JsonSerializer.Serialize(updatedConfig, NatsPcgJsonSerializerContext.Default.NatsPcgElasticConfig);

            try
            {
                await store.UpdateAsync(key, json, entry.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
                return updatedMembers ?? Array.Empty<string>();
            }
            catch (NatsKVWrongLastRevisionException)
            {
                // Config changed - retry
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50 * (retry + 1)), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw new NatsPcgException("Failed to update config after maximum retries");
    }

    private static async Task<INatsKVStore> GetOrCreateKvStoreAsync(INatsKVContext kv, CancellationToken cancellationToken)
    {
        return await kv.CreateOrUpdateStoreAsync(new NatsKVConfig(NatsPcgConstants.ElasticKvBucket), cancellationToken).ConfigureAwait(false);
    }

    internal static string GetKvKey(string streamName, string consumerGroupName)
        => $"{streamName}.{consumerGroupName}";

    internal static string GetWorkQueueStreamName(string streamName, string consumerGroupName)
        => $"{streamName}-{consumerGroupName}";

    internal static string GetConsumerName(string consumerGroupName)
        => $"pcg-{consumerGroupName}";
}
