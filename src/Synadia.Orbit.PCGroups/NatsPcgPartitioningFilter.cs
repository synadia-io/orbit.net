// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// A filter with its associated partitioning wildcard positions for elastic consumer groups.
/// </summary>
public sealed record NatsPcgPartitioningFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgPartitioningFilter"/> class.
    /// </summary>
    /// <param name="filter">The subject filter (must contain at least one '*' wildcard).</param>
    /// <param name="partitioningWildcards">The wildcard positions used for partitioning (1-indexed). Empty array means partition by full subject.</param>
    public NatsPcgPartitioningFilter(string filter, int[] partitioningWildcards)
    {
        Filter = filter;
        PartitioningWildcards = partitioningWildcards;
    }

    /// <summary>
    /// Gets the subject filter.
    /// </summary>
    [JsonPropertyName("filter")]
    public string Filter { get; init; }

    /// <summary>
    /// Gets the wildcard positions used for partitioning (1-indexed).
    /// An empty array means partition by the entire subject string (server hashes the full subject).
    /// </summary>
    [JsonPropertyName("partitioning_wildcards")]
    public int[] PartitioningWildcards { get; init; }
}
