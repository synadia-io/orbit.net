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

        if (memberMappings != null && memberMappings.Length > 0)
        {
            // Use explicit mapping
            var mapping = Array.Find(memberMappings, m => m.Member == memberName);
            if (mapping == null)
            {
                throw new NatsPcgMembershipException($"Member '{memberName}' not found in member mappings");
            }

            partitions = mapping.Partitions;
        }
        else if (members != null && members.Length > 0)
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
        var filters = new string[partitions.Length];
        for (int i = 0; i < partitions.Length; i++)
        {
            filters[i] = $"{partitions[i]}.>";
        }

        return filters;
    }

    /// <summary>
    /// Distributes partitions evenly across members.
    /// </summary>
    /// <param name="members">Sorted list of member names.</param>
    /// <param name="maxMembers">Maximum number of members (equals number of partitions).</param>
    /// <param name="memberName">The member name to distribute partitions for.</param>
    /// <returns>Array of partition numbers assigned to this member.</returns>
    private static int[] DistributePartitions(string[] members, uint maxMembers, string memberName)
    {
        // Sort members to ensure consistent distribution
        var sortedMembers = (string[])members.Clone();
        Array.Sort(sortedMembers, StringComparer.Ordinal);

        var memberIndex = Array.IndexOf(sortedMembers, memberName);
        if (memberIndex < 0)
        {
            throw new NatsPcgMembershipException($"Member '{memberName}' not found in members list");
        }

        var numMembers = sortedMembers.Length;
        var partitions = new List<int>();

        // Distribute partitions: member at index i gets partitions where partition % numMembers == i
        for (int partition = 0; partition < maxMembers; partition++)
        {
            if (partition % numMembers == memberIndex)
            {
                partitions.Add(partition);
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
        if (memberMappings != null && memberMappings.Length > 0)
        {
            return Array.Exists(memberMappings, m => m.Member == memberName);
        }

        if (members != null && members.Length > 0)
        {
            return Array.Exists(members, m => m == memberName);
        }

        // No membership restrictions - any member can join
        return true;
    }
}
