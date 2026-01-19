// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Represents a mapping of a member to specific partitions.
/// </summary>
public sealed record NatsPCMemberMapping
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCMemberMapping"/> class.
    /// </summary>
    /// <param name="member">The member name.</param>
    /// <param name="partitions">The partitions assigned to this member.</param>
    [JsonConstructor]
    public NatsPCMemberMapping(string member, int[] partitions)
    {
        Member = member;
        Partitions = partitions;
    }

    /// <summary>
    /// Gets the member name.
    /// </summary>
    [JsonPropertyName("member")]
    public string Member { get; }

    /// <summary>
    /// Gets the partitions assigned to this member.
    /// </summary>
    [JsonPropertyName("partitions")]
    public int[] Partitions { get; }
}
