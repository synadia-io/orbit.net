// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups.Test;

/// <summary>
/// Unit tests for partition distribution and validation logic.
/// These tests mirror the Go TestBaseFunctions tests.
/// </summary>
public class NatsPcgPartitionDistributorTests
{
    [Fact]
    public void GeneratePartitionFilters_AutoDistributes_3Members_6Partitions()
    {
        var members = new[] { "m1", "m2", "m3" };

        // With 6 partitions and 3 members (sorted: m1, m2, m3):
        // Using contiguous block algorithm (matches Go implementation):
        // m1 (index 0) gets partitions: 0, 1
        // m2 (index 1) gets partitions: 2, 3
        // m3 (index 2) gets partitions: 4, 5
        var filtersM1 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 6, null, "m1");
        var filtersM2 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 6, null, "m2");
        var filtersM3 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 6, null, "m3");

        Assert.Equal(new[] { "0.>", "1.>" }, filtersM1);
        Assert.Equal(new[] { "2.>", "3.>" }, filtersM2);
        Assert.Equal(new[] { "4.>", "5.>" }, filtersM3);
    }

    [Fact]
    public void GeneratePartitionFilters_AutoDistributes_3Members_7Partitions()
    {
        var members = new[] { "m1", "m2", "m3" };

        // With 7 partitions and 3 members (contiguous blocks + remainder):
        // m1 gets partitions: 0, 1, 6 (2 regular + 1 remainder)
        // m2 gets partitions: 2, 3
        // m3 gets partitions: 4, 5
        var filtersM1 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 7, null, "m1");
        var filtersM2 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 7, null, "m2");
        var filtersM3 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 7, null, "m3");

        Assert.Equal(new[] { "0.>", "1.>", "6.>" }, filtersM1);
        Assert.Equal(new[] { "2.>", "3.>" }, filtersM2);
        Assert.Equal(new[] { "4.>", "5.>" }, filtersM3);
    }

    [Fact]
    public void GeneratePartitionFilters_AutoDistributes_3Members_8Partitions()
    {
        var members = new[] { "m1", "m2", "m3" };

        // With 8 partitions and 3 members (contiguous blocks + remainder):
        // m1 gets partitions: 0, 1, 6 (2 regular + 1 remainder)
        // m2 gets partitions: 2, 3, 7 (2 regular + 1 remainder)
        // m3 gets partitions: 4, 5
        var filtersM1 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 8, null, "m1");
        var filtersM2 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 8, null, "m2");
        var filtersM3 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 8, null, "m3");

        Assert.Equal(new[] { "0.>", "1.>", "6.>" }, filtersM1);
        Assert.Equal(new[] { "2.>", "3.>", "7.>" }, filtersM2);
        Assert.Equal(new[] { "4.>", "5.>" }, filtersM3);
    }

    [Fact]
    public void GeneratePartitionFilters_UsesExplicitMappings()
    {
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0, 2 }),
            new NatsPcgMemberMapping("m2", new[] { 1 }),
        };

        var filtersM1 = NatsPcgPartitionDistributor.GeneratePartitionFilters(null, 3, mappings, "m1");
        var filtersM2 = NatsPcgPartitionDistributor.GeneratePartitionFilters(null, 3, mappings, "m2");

        Assert.Equal(new[] { "0.>", "2.>" }, filtersM1);
        Assert.Equal(new[] { "1.>" }, filtersM2);
    }

    [Fact]
    public void GeneratePartitionFilters_MemberNotFound_ThrowsException()
    {
        var members = new[] { "m1", "m2" };

        var ex = Assert.Throws<NatsPcgMembershipException>(() =>
            NatsPcgPartitionDistributor.GeneratePartitionFilters(members, 2, null, "m3"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void GeneratePartitionFilters_EmptyMembership_ReturnsAllPartitions()
    {
        // When membership is null/empty, any member gets all partitions
        var filters = NatsPcgPartitionDistributor.GeneratePartitionFilters(null, 3, null, "anyMember");

        Assert.Equal(new[] { "0.>", "1.>", "2.>" }, filters);
    }

    [Fact]
    public void GeneratePartitionFilters_MembersSortedForConsistency()
    {
        // Members should be sorted to ensure consistent partition assignment
        var members1 = new[] { "c", "a", "b" };
        var members2 = new[] { "a", "b", "c" };

        var filters1 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members1, 3, null, "a");
        var filters2 = NatsPcgPartitionDistributor.GeneratePartitionFilters(members2, 3, null, "a");

        Assert.Equal(filters1, filters2);
    }

    [Fact]
    public void IsInMembership_NullMembership_ReturnsTrue()
    {
        // When membership is not defined, any member is considered in membership
        Assert.True(NatsPcgPartitionDistributor.IsInMembership(null, null, "anyMember"));
    }

    [Fact]
    public void IsInMembership_EmptyMembership_ReturnsTrue()
    {
        Assert.True(NatsPcgPartitionDistributor.IsInMembership(Array.Empty<string>(), null, "anyMember"));
    }

    [Fact]
    public void IsInMembership_MemberInList_ReturnsTrue()
    {
        var members = new[] { "m1", "m2", "m3" };
        Assert.True(NatsPcgPartitionDistributor.IsInMembership(members, null, "m2"));
    }

    [Fact]
    public void IsInMembership_MemberNotInList_ReturnsFalse()
    {
        var members = new[] { "m1", "m2", "m3" };
        Assert.False(NatsPcgPartitionDistributor.IsInMembership(members, null, "m4"));
    }

    [Fact]
    public void IsInMembership_MemberInMappings_ReturnsTrue()
    {
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0 }),
            new NatsPcgMemberMapping("m2", new[] { 1 }),
        };
        Assert.True(NatsPcgPartitionDistributor.IsInMembership(null, mappings, "m1"));
    }

    [Fact]
    public void IsInMembership_MemberNotInMappings_ReturnsFalse()
    {
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0 }),
            new NatsPcgMemberMapping("m2", new[] { 1 }),
        };
        Assert.False(NatsPcgPartitionDistributor.IsInMembership(null, mappings, "m3"));
    }
}
