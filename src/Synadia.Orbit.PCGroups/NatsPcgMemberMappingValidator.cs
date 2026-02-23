// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Validates member mappings for partitioned consumer groups.
/// </summary>
public static class NatsPcgMemberMappingValidator
{
    /// <summary>
    /// Validates member mappings for consistency and completeness.
    /// </summary>
    /// <param name="memberMappings">The member mappings to validate.</param>
    /// <param name="maxMembers">The maximum number of members (partitions).</param>
    /// <param name="requireAllPartitions">If true, requires all partitions to be assigned.</param>
    /// <exception cref="NatsPcgConfigurationException">Thrown when validation fails.</exception>
    public static void Validate(NatsPcgMemberMapping[]? memberMappings, uint maxMembers, bool requireAllPartitions = true)
    {
        if (memberMappings == null || memberMappings.Length == 0)
        {
            return;
        }

        // Check member mappings count bounds
        if (memberMappings.Length > maxMembers)
        {
            throw new NatsPcgConfigurationException(
                $"The number of member mappings ({memberMappings.Length}) cannot exceed the max number of members ({maxMembers})");
        }

        var seenMembers = new HashSet<string>(StringComparer.Ordinal);
        var seenPartitions = new HashSet<int>();

        foreach (var mapping in memberMappings)
        {
            // Validate member name uniqueness
            if (!seenMembers.Add(mapping.Member))
            {
                throw new NatsPcgConfigurationException(
                    $"Duplicate member name '{mapping.Member}' in member mappings");
            }

            // Validate partitions within this mapping
            var mappingPartitions = new HashSet<int>();
            foreach (int partition in mapping.Partitions)
            {
                // Validate partition range
                if (partition < 0 || partition >= maxMembers)
                {
                    throw new NatsPcgConfigurationException(
                        $"Partition {partition} for member '{mapping.Member}' is out of range [0, {maxMembers})");
                }

                // Check for duplicate partition within the same mapping
                if (!mappingPartitions.Add(partition))
                {
                    throw new NatsPcgConfigurationException(
                        $"Duplicate partition {partition} in mapping for member '{mapping.Member}'");
                }

                // Check for partition overlap across mappings
                if (!seenPartitions.Add(partition))
                {
                    throw new NatsPcgConfigurationException(
                        $"Partition {partition} is assigned to multiple members");
                }
            }
        }

        // Validate all partitions are covered if required
        if (requireAllPartitions && seenPartitions.Count != maxMembers)
        {
            throw new NatsPcgConfigurationException(
                $"Member mappings must cover all {maxMembers} partitions, but only {seenPartitions.Count} are assigned");
        }
    }

    /// <summary>
    /// Validates filter and partitioning wildcards for elastic consumer groups.
    /// </summary>
    /// <param name="filter">The subject filter.</param>
    /// <param name="partitioningWildcards">The partitioning wildcard positions (1-indexed).</param>
    /// <exception cref="NatsPcgConfigurationException">Thrown when validation fails.</exception>
    public static void ValidateFilterAndWildcards(string filter, int[] partitioningWildcards)
    {
        if (string.IsNullOrEmpty(filter))
        {
            throw new NatsPcgConfigurationException("Filter is required for elastic consumer groups");
        }

        // Count wildcards in filter
        var filterTokens = filter.Split('.');
        int numWildcards = 0;
        foreach (var token in filterTokens)
        {
            if (token == "*")
            {
                numWildcards++;
            }
        }

        if (numWildcards < 1)
        {
            throw new NatsPcgConfigurationException("Filter must contain at least one '*' wildcard");
        }

        if (partitioningWildcards == null || partitioningWildcards.Length == 0)
        {
            throw new NatsPcgConfigurationException("PartitioningWildcards must contain at least one element");
        }

        if (partitioningWildcards.Length > numWildcards)
        {
            throw new NatsPcgConfigurationException(
                $"The number of partitioning wildcards ({partitioningWildcards.Length}) cannot exceed the number of '*' wildcards in the filter ({numWildcards})");
        }

        // Validate wildcard positions are unique and in range
        var seenPositions = new HashSet<int>();
        foreach (int pos in partitioningWildcards)
        {
            if (pos < 1 || pos > numWildcards)
            {
                throw new NatsPcgConfigurationException(
                    $"Partitioning wildcard position {pos} is out of range [1, {numWildcards}]");
            }

            if (!seenPositions.Add(pos))
            {
                throw new NatsPcgConfigurationException(
                    $"Duplicate partitioning wildcard position {pos}");
            }
        }
    }
}
