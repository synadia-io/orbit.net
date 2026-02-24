// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Distributes partitions to members.
/// </summary>
public static class NatsPcgPartitionDistributor
{
    /// <summary>
    /// Generates partition filters for a member.
    /// </summary>
    /// <param name="members">List of member names (sorted).</param>
    /// <param name="maxMembers">Maximum number of members (equals number of partitions).</param>
    /// <param name="memberMappings">Optional explicit member-to-partition mappings.</param>
    /// <param name="memberName">The member name to generate filters for.</param>
    /// <returns>Array of filters like ["0.>", "3.>", "6.>"].</returns>
    public static string[] GeneratePartitionFilters(
        string[]? members,
        uint maxMembers,
        NatsPcgMemberMapping[]? memberMappings,
        string memberName)
    {
        int[] partitions;

        if (memberMappings is { Length: > 0 })
        {
            // Use explicit mapping
            var mapping = Array.Find(memberMappings, m => m.Member == memberName);
            if (mapping == null)
            {
                throw new NatsPcgMembershipException($"Member '{memberName}' not found in member mappings");
            }

            partitions = mapping.Partitions;
        }
        else if (members is { Length: > 0 })
        {
            // Auto-distribute partitions based on member position
            partitions = DistributePartitions(members, maxMembers, memberName);
        }
        else
        {
            // No members or mappings defined - single member gets all partitions
            partitions = new int[maxMembers];
            for (int i = 0; i < maxMembers; i++)
            {
                partitions[i] = i;
            }
        }

        // Convert partition numbers to filters
        string[] filters = new string[partitions.Length];
        for (int i = 0; i < partitions.Length; i++)
        {
            filters[i] = $"{partitions[i]}.>";
        }

        return filters;
    }

    /// <summary>
    /// Distributes partitions evenly across members using contiguous blocks.
    /// This algorithm minimizes partition redistribution when members are added/removed.
    /// </summary>
    /// <param name="members">Sorted list of member names.</param>
    /// <param name="maxMembers">Maximum number of members (equals number of partitions).</param>
    /// <param name="memberName">The member name to distribute partitions for.</param>
    /// <returns>Array of partition numbers assigned to this member.</returns>
    /// <remarks>
    /// Distribution example with 6 partitions and 3 members:
    /// - m1: [0, 1], m2: [2, 3], m3: [4, 5]
    ///
    /// With 7 partitions and 3 members (remainder goes to first members):
    /// - m1: [0, 1, 6], m2: [2, 3], m3: [4, 5]
    ///
    /// This matches the Go implementation for cross-language interoperability.
    /// </remarks>
    private static int[] DistributePartitions(string[] members, uint maxMembers, string memberName)
    {
        // Sort members to ensure consistent distribution
        string[] sortedMembers = (string[])members.Clone();
        Array.Sort(sortedMembers, StringComparer.Ordinal);

        int memberIndex = Array.IndexOf(sortedMembers, memberName);
        if (memberIndex < 0)
        {
            throw new NatsPcgMembershipException($"Member '{memberName}' not found in members list");
        }

        uint numMembers = (uint)sortedMembers.Length;
        var partitions = new List<int>();

        // Number of partitions per member (rounded down)
        uint numPer = maxMembers / numMembers;

        for (uint i = 0; i < maxMembers; i++)
        {
            if (i < numMembers * numPer)
            {
                // Regular distribution: contiguous blocks
                uint assignedMemberIndex = i / numPer;
                if (assignedMemberIndex == memberIndex)
                {
                    partitions.Add((int)i);
                }
            }
            else
            {
                // Remainder partitions: distribute to first members
                uint remainderIndex = (i - (numMembers * numPer)) % numMembers;
                if (remainderIndex == memberIndex)
                {
                    partitions.Add((int)i);
                }
            }
        }

        return partitions.ToArray();
    }

    /// <summary>
    /// Checks if a member is in the membership.
    /// </summary>
    /// <param name="members">List of member names.</param>
    /// <param name="memberMappings">Optional explicit member-to-partition mappings.</param>
    /// <param name="memberName">The member name to check.</param>
    /// <returns>True if the member is in the membership.</returns>
    public static bool IsInMembership(string[]? members, NatsPcgMemberMapping[]? memberMappings, string memberName)
    {
        if (memberMappings is { Length: > 0 })
        {
            return Array.Exists(memberMappings, m => m.Member == memberName);
        }

        if (members is { Length: > 0 })
        {
            return Array.Exists(members, m => m == memberName);
        }

        // No membership restrictions - any member can join
        return true;
    }
}
