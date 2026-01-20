// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

namespace Synadia.Orbit.PCGroups.Static;

/// <summary>
/// Static partitioned consumer group operations.
/// Static groups have a fixed membership that is defined at creation time.
/// </summary>
public static class NatsPcgStaticExtensions
{
    /// <summary>
    /// Creates a static consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream to consume from.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="maxNumMembers">Maximum number of members (also the number of partitions).</param>
    /// <param name="filter">Optional subject filter.</param>
    /// <param name="members">Optional list of allowed member names.</param>
    /// <param name="memberMappings">Optional explicit member-to-partition mappings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created configuration.</returns>
    public static async Task<NatsPcgStaticConfig> CreatePcgStaticAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        uint maxNumMembers,
        string? filter = null,
        string[]? members = null,
        NatsPcgMemberMapping[]? memberMappings = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfig(maxNumMembers, members, memberMappings);

        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await GetOrCreateKvStoreAsync(kv, cancellationToken).ConfigureAwait(false);

        var config = new NatsPcgStaticConfig
        {
            MaxMembers = maxNumMembers,
            Filter = filter,
            Members = members,
            MemberMappings = memberMappings,
        };

        var key = GetKvKey(streamName, consumerGroupName);
        var json = JsonSerializer.Serialize(config, NatsPcgJsonSerializerContext.Default.NatsPcgStaticConfig);

        var revision = await store.CreateAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);

        return config with { Revision = revision };
    }

    /// <summary>
    /// Gets the configuration for a static consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configuration.</returns>
    public static async Task<NatsPcgStaticConfig> GetPcgStaticConfigAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await kv.GetStoreAsync(NatsPcgConstants.StaticKvBucket, cancellationToken).ConfigureAwait(false);

        var key = GetKvKey(streamName, consumerGroupName);
        var entry = await store.GetEntryAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (entry.Value == null)
        {
            throw new NatsPcgException($"Consumer group '{consumerGroupName}' not found for stream '{streamName}'");
        }

        var config = JsonSerializer.Deserialize(entry.Value, NatsPcgJsonSerializerContext.Default.NatsPcgStaticConfig);
        if (config == null)
        {
            throw new NatsPcgException($"Failed to deserialize config for consumer group '{consumerGroupName}'");
        }

        return config with { Revision = entry.Revision };
    }

    /// <summary>
    /// Starts consuming messages from a static consumer group.
    /// </summary>
    /// <typeparam name="T">Message data type.</typeparam>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberName">Name of this member.</param>
    /// <param name="messageHandler">Handler for received messages.</param>
    /// <param name="serializer">Optional deserializer for message data.</param>
    /// <param name="config">Optional consumer configuration overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A consume context for controlling the consumer.</returns>
    public static async Task<INatsPcgConsumeContext> ConsumePcgStaticAsync<T>(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        Func<NatsPcgMsg<T>, CancellationToken, ValueTask> messageHandler,
        INatsDeserialize<T>? serializer = null,
        ConsumerConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var groupConfig = await GetPcgStaticConfigAsync(js, streamName, consumerGroupName, cancellationToken).ConfigureAwait(false);

        if (!groupConfig.IsInMembership(memberName))
        {
            throw new NatsPcgMembershipException($"Member '{memberName}' is not in membership for consumer group '{consumerGroupName}'");
        }

        var context = new NatsPcgStaticConsumeContext<T>(
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
    /// Deletes a static consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DeletePcgStaticAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();
        var store = await kv.GetStoreAsync(NatsPcgConstants.StaticKvBucket, cancellationToken).ConfigureAwait(false);

        var key = GetKvKey(streamName, consumerGroupName);
        await store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all static consumer groups for a stream.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consumer group names.</returns>
    public static async IAsyncEnumerable<string> ListPcgStaticAsync(
        this INatsJSContext js,
        string streamName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kv = js.Connection.CreateKeyValueStoreContext();

        INatsKVStore store;
        try
        {
            store = await kv.GetStoreAsync(NatsPcgConstants.StaticKvBucket, cancellationToken).ConfigureAwait(false);
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
    /// Lists active members for a static consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active member names.</returns>
    public static async IAsyncEnumerable<string> ListPcgStaticActiveMembersAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var consumerName = GetConsumerName(consumerGroupName);

        ConsumerInfo info;
        try
        {
            var consumer = await js.GetConsumerAsync(streamName, consumerName, cancellationToken).ConfigureAwait(false);
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
    /// Forces a member to step down from the consumer group.
    /// </summary>
    /// <param name="js">JetStream context.</param>
    /// <param name="streamName">Name of the stream.</param>
    /// <param name="consumerGroupName">Name of the consumer group.</param>
    /// <param name="memberName">Name of the member to step down.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task PcgStaticMemberStepDownAsync(
        this INatsJSContext js,
        string streamName,
        string consumerGroupName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        var consumerName = GetConsumerName(consumerGroupName);
        var consumer = await js.GetConsumerAsync(streamName, consumerName, cancellationToken).ConfigureAwait(false);

        // Find the priority group for this member and unpin
        await consumer.UnpinAsync(NatsPcgConstants.PriorityGroupName, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateConfig(uint maxNumMembers, string[]? members, NatsPcgMemberMapping[]? memberMappings)
    {
        if (maxNumMembers == 0)
        {
            throw new NatsPcgConfigurationException("maxNumMembers must be greater than 0");
        }

        if (members != null && memberMappings != null)
        {
            throw new NatsPcgConfigurationException("Cannot specify both members and memberMappings");
        }

        if (memberMappings != null)
        {
            foreach (var mapping in memberMappings)
            {
                foreach (var partition in mapping.Partitions)
                {
                    if (partition < 0 || partition >= maxNumMembers)
                    {
                        throw new NatsPcgConfigurationException($"Partition {partition} is out of range [0, {maxNumMembers})");
                    }
                }
            }
        }
    }

    private static async Task<INatsKVStore> GetOrCreateKvStoreAsync(INatsKVContext kv, CancellationToken cancellationToken)
    {
        return await kv.CreateOrUpdateStoreAsync(new NatsKVConfig(NatsPcgConstants.StaticKvBucket), cancellationToken).ConfigureAwait(false);
    }

    internal static string GetKvKey(string streamName, string consumerGroupName)
        => $"{streamName}.{consumerGroupName}";

    internal static string GetConsumerName(string consumerGroupName)
        => $"pcg-{consumerGroupName}";
}
