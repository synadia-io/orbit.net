// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.PCGroups.Static;

/// <summary>
/// Configuration for a static partitioned consumer group.
/// </summary>
public sealed record NatsPcgStaticConfig
{
    /// <summary>
    /// Gets the maximum number of members (also the number of partitions).
    /// </summary>
    [JsonPropertyName("max_members")]
    public required uint MaxMembers { get; init; }

    /// <summary>
    /// Gets the optional filter for the consumer group.
    /// </summary>
    [JsonPropertyName("filter")]
    public string? Filter { get; init; }

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
