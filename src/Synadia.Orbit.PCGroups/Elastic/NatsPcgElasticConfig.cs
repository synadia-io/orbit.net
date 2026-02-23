// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.PCGroups.Elastic;

/// <summary>
/// Configuration for an elastic partitioned consumer group.
/// </summary>
public sealed record NatsPcgElasticConfig
{
    /// <summary>
    /// Gets the maximum number of members (also the number of partitions).
    /// </summary>
    [JsonPropertyName("max_members")]
    public required uint MaxMembers { get; init; }

    /// <summary>
    /// Gets the subject filter for the consumer group.
    /// </summary>
    [JsonPropertyName("filter")]
    public required string Filter { get; init; }

    /// <summary>
    /// Gets the wildcard positions used for partitioning (1-indexed).
    /// </summary>
    [JsonPropertyName("partitioning_wildcards")]
    public required int[] PartitioningWildcards { get; init; }

    /// <summary>
    /// Gets the optional maximum number of buffered messages.
    /// </summary>
    [JsonPropertyName("max_buffered_msg")]
    public long? MaxBufferedMsgs { get; init; }

    /// <summary>
    /// Gets the optional maximum bytes of buffered messages.
    /// </summary>
    [JsonPropertyName("max_buffered_bytes")]
    public long? MaxBufferedBytes { get; init; }

    /// <summary>
    /// Gets the optional list of allowed member names.
    /// </summary>
    [JsonPropertyName("members")]
    public string[]? Members { get; init; }

    /// <summary>
    /// Gets the optional explicit member-to-partition mappings.
    /// </summary>
    [JsonPropertyName("member_mappings")]
    public NatsPcgMemberMapping[]? MemberMappings { get; init; }

    /// <summary>
    /// Gets the revision of this configuration from KV store.
    /// </summary>
    [JsonIgnore]
    internal ulong Revision { get; init; }

    /// <summary>
    /// Checks if a member is in the membership.
    /// </summary>
    /// <param name="name">The member name to check.</param>
    /// <returns>True if the member is in the membership.</returns>
    public bool IsInMembership(string name)
        => NatsPcgPartitionDistributor.IsInMembership(Members, MemberMappings, name);
}
