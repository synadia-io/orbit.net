// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

// ReSharper disable InvokeAsExtensionMember
// ReSharper disable ConvertToExtensionBlock
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

        string key = GetKvKey(streamName, consumerGroupName);

        ulong revision = await store.CreateAsync(key, config, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);

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

        string key = GetKvKey(streamName, consumerGroupName);
        var entry = await store.GetEntryAsync(key, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (entry.Value == null)
        {
            throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found for stream '{streamName}'");
        }

        return entry.Value with { Revision = entry.Revision };
    }

    /// <summary>
    /// Starts consuming messages from an elastic consumer group.
    /// </summary>
    /// <typeparam name="T">Message data type.</typeparam>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the source stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberName">Name of this member.</param>
    /// <param name="serializer">Optional deserializer for message data.</param>
    /// <param name="config">Optional consumer configuration overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of messages from the consumer group.</returns>
    public static async IAsyncEnumerable<INatsJSMsg<T>> ConsumePcgElasticAsync<T>(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        INatsDeserialize<T>? serializer = null,
        ConsumerConfig? config = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            serializer,
            config);

        try
        {
            await context.StartAsync(cancellationToken).ConfigureAwait(false);

            await foreach (var msg in context.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return msg;
            }
        }
        finally
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }
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
            string key = GetKvKey(streamName, consumerGroupName);
            await store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            // KV bucket doesn't exist - ignore
        }

        // Delete work-queue stream
        string workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);
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

        string prefix = $"{streamName}.";

        await foreach (string? key in store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
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
        string workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);
        string consumerName = GetConsumerName(consumerGroupName);

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
                    int dashIndex = group.PinnedClientId.LastIndexOf('-');
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
        string key = GetKvKey(streamName, consumerGroupName);

        // Retry loop for optimistic concurrency
        const int maxRetries = 5;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            var entry = await store.GetEntryAsync(key, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value == null)
            {
                throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found");
            }

            var config = entry.Value;

            // Validate mappings - elastic groups require all partitions to be covered
            NatsPcgMemberMappingValidator.Validate(memberMappings, config.MaxMembers, requireAllPartitions: true);

            var updatedConfig = config with
            {
                Members = null,
                MemberMappings = memberMappings,
            };

            try
            {
                await store.UpdateAsync(key, updatedConfig, entry.Revision, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
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
            var entry = await store.GetEntryAsync(key, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value == null)
            {
                throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found");
            }

            var updatedConfig = entry.Value with { MemberMappings = null };

            try
            {
                await store.UpdateAsync(key, updatedConfig, entry.Revision, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
    private static void ValidateConfig(uint maxNumMembers, string filter, int[] partitioningWildcards)
    {
        // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local
        if (maxNumMembers == 0)
        {
            throw new NatsPcgConfigurationException("maxNumMembers must be greater than 0");
        }

        NatsPcgMemberMappingValidator.ValidateFilterAndWildcards(filter, partitioningWildcards);
    }

    private static async Task CreateWorkQueueStreamAsync(
        INatsJSContext js,
        string streamName,
        string consumerGroupName,
        NatsPcgElasticConfig config,
        CancellationToken cancellationToken)
    {
        string workQueueStreamName = GetWorkQueueStreamName(streamName, consumerGroupName);

        // Build subject transform: add partition prefix based on wildcards
        // Transform syntax: {{Partition(numPartitions, wildcardIndexes...)}}
        // Replace * wildcards with {{wildcard(N)}} in the filter
        string wildcardStr = string.Join(",", config.PartitioningWildcards);
        var filterTokens = config.Filter.Split('.');
        int wildcardIndex = 1;
        for (int i = 0; i < filterTokens.Length; i++)
        {
            if (filterTokens[i] == "*")
            {
                filterTokens[i] = $"{{{{wildcard({wildcardIndex})}}}}";
                wildcardIndex++;
            }
        }

        string destFromFilter = string.Join(".", filterTokens);
        string subjectTransform = $"{{{{Partition({config.MaxMembers},{wildcardStr})}}}}.{destFromFilter}";

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
        string key = GetKvKey(streamName, consumerGroupName);

        // Retry loop for optimistic concurrency
        const int maxRetries = 5;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            var entry = await store.GetEntryAsync(key, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (entry.Value == null)
            {
                throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found");
            }

            var config = entry.Value;

            if (config.MemberMappings != null)
            {
                throw new NatsPcgConfigurationException("Cannot modify members when member mappings are defined");
            }

            var currentMembers = config.Members != null
                ? new HashSet<string>(config.Members, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            if (add)
            {
                foreach (string member in memberNames)
                {
                    currentMembers.Add(member);
                }
            }
            else
            {
                foreach (string member in memberNames)
                {
                    currentMembers.Remove(member);
                }
            }

            string[]? updatedMembers = currentMembers.Count > 0 ? currentMembers.ToArray() : null;
            var updatedConfig = config with { Members = updatedMembers };

            try
            {
                await store.UpdateAsync(key, updatedConfig, entry.Revision, serializer: NatsPcgJsonSerializer<NatsPcgElasticConfig>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
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
