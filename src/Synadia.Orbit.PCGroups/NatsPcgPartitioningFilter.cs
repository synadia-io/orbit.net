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

    /// <inheritdoc />
    public bool Equals(NatsPcgPartitioningFilter? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        if (!string.Equals(Filter, other.Filter, StringComparison.Ordinal))
        {
            return false;
        }

        var leftWildcards = PartitioningWildcards ?? Array.Empty<int>();
        var rightWildcards = other.PartitioningWildcards ?? Array.Empty<int>();

        if (leftWildcards.Length != rightWildcards.Length)
        {
            return false;
        }

        for (int i = 0; i < leftWildcards.Length; i++)
        {
            if (leftWildcards[i] != rightWildcards[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(Filter ?? string.Empty);
            var wildcards = PartitioningWildcards ?? Array.Empty<int>();
            for (int i = 0; i < wildcards.Length; i++)
            {
                hash = (hash * 397) ^ wildcards[i];
            }

            return hash;
        }
    }
}

internal sealed class PartitioningFilterComparer : IComparer<NatsPcgPartitioningFilter>
{
    internal static readonly PartitioningFilterComparer Instance = new();

    public int Compare(NatsPcgPartitioningFilter? x, NatsPcgPartitioningFilter? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int filterCompare = string.Compare(x.Filter, y.Filter, StringComparison.Ordinal);
        if (filterCompare != 0)
        {
            return filterCompare;
        }

        var xWildcards = x.PartitioningWildcards ?? Array.Empty<int>();
        var yWildcards = y.PartitioningWildcards ?? Array.Empty<int>();

        int minLength = Math.Min(xWildcards.Length, yWildcards.Length);
        for (int i = 0; i < minLength; i++)
        {
            int wildcardCompare = xWildcards[i].CompareTo(yWildcards[i]);
            if (wildcardCompare != 0)
            {
                return wildcardCompare;
            }
        }

        return xWildcards.Length.CompareTo(yWildcards.Length);
    }
}
