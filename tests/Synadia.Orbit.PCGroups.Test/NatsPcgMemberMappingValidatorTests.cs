// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups.Test;

/// <summary>
/// Unit tests for member mapping validation logic.
/// These tests mirror the Go TestBaseFunctions validation tests.
/// </summary>
public class NatsPcgMemberMappingValidatorTests
{
    [Fact]
    public void Validate_ValidMappings_NoException()
    {
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0 }),
            new NatsPcgMemberMapping("m2", new[] { 1 }),
        };

        // Should not throw
        NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: true);
    }

    [Fact]
    public void Validate_NullMappings_NoException()
    {
        // Null mappings are valid (auto-distribution)
        NatsPcgMemberMappingValidator.Validate(null, maxMembers: 2, requireAllPartitions: true);
    }

    [Fact]
    public void Validate_EmptyMappings_NoException()
    {
        // Empty mappings are valid (auto-distribution)
        NatsPcgMemberMappingValidator.Validate(Array.Empty<NatsPcgMemberMapping>(), maxMembers: 2, requireAllPartitions: true);
    }

    [Fact]
    public void Validate_DuplicatePartitionInSameMapping_ThrowsException()
    {
        // Duplicate partition within same member mapping
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 1, 1 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false));

        Assert.Contains("Duplicate partition", ex.Message);
    }

    [Fact]
    public void Validate_PartitionOverlapBetweenMembers_ThrowsException()
    {
        // Same partition assigned to multiple members
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0, 1 }),
            new NatsPcgMemberMapping("m2", new[] { 0, 1 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false));

        Assert.Contains("assigned to multiple members", ex.Message);
    }

    [Fact]
    public void Validate_PartialOverlapBetweenMembers_ThrowsException()
    {
        // Partial overlap - only partition 2 overlaps
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0, 2 }),
            new NatsPcgMemberMapping("m2", new[] { 1, 2 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 3, requireAllPartitions: false));

        Assert.Contains("assigned to multiple members", ex.Message);
    }

    [Fact]
    public void Validate_PartitionOutOfRange_ThrowsException()
    {
        // Partition 2 is out of range for maxMembers=2
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0, 2 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false));

        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void Validate_NegativePartition_ThrowsException()
    {
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { -1, 0 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false));

        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void Validate_DuplicateMemberName_ThrowsException()
    {
        // Same member name appears twice
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0, 1 }),
            new NatsPcgMemberMapping("m1", new[] { 0, 1 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false));

        Assert.Contains("Duplicate member name", ex.Message);
    }

    [Fact]
    public void Validate_NotAllPartitionsCovered_WhenRequired_ThrowsException()
    {
        // Only 1 of 2 partitions covered
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0 }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: true));

        Assert.Contains("must cover all", ex.Message);
    }

    [Fact]
    public void Validate_NotAllPartitionsCovered_WhenNotRequired_NoException()
    {
        // Only 1 of 2 partitions covered, but not required
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0 }),
        };

        // Should not throw when requireAllPartitions is false
        NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false);
    }

    [Fact]
    public void Validate_TooManyMappings_ThrowsException()
    {
        // 3 mappings for maxMembers=2
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0 }),
            new NatsPcgMemberMapping("m2", new[] { 1 }),
            new NatsPcgMemberMapping("m3", new int[] { }),
        };

        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 2, requireAllPartitions: false));

        Assert.Contains("cannot exceed", ex.Message);
    }

    [Fact]
    public void Validate_ComplexValidMapping_NoException()
    {
        // Valid complex mapping with 3 partitions
        var mappings = new[]
        {
            new NatsPcgMemberMapping("m1", new[] { 0, 2 }),
            new NatsPcgMemberMapping("m2", new[] { 1 }),
        };

        // Should not throw
        NatsPcgMemberMappingValidator.Validate(mappings, maxMembers: 3, requireAllPartitions: true);
    }

    [Fact]
    public void ValidateFilterAndWildcards_ValidConfig_NoException()
    {
        // Should not throw
        NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*", new[] { 1 });
    }

    [Fact]
    public void ValidateFilterAndWildcards_MultipleWildcards_NoException()
    {
        // Should not throw
        NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*.*.>", new[] { 1, 2 });
    }

    [Fact]
    public void ValidateFilterAndWildcards_NullFilter_ThrowsException()
    {
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards(null!, new[] { 1 }));

        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_EmptyFilter_ThrowsException()
    {
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards(string.Empty, new[] { 1 }));

        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_NoWildcardsInFilter_ThrowsException()
    {
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.bar.baz", new[] { 1 }));

        Assert.Contains("at least one '*' wildcard", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_NullPartitioningWildcards_ThrowsException()
    {
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*", null!));

        Assert.Contains("must not be null", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_EmptyPartitioningWildcards_FullSubject_NoException()
    {
        // Empty array means partition by full subject
        NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*", Array.Empty<int>());
    }

    [Fact]
    public void ValidateFilterAndWildcards_TooManyPartitioningWildcards_ThrowsException()
    {
        // Filter has 1 wildcard, but partitioningWildcards has 2
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*", new[] { 1, 2 }));

        Assert.Contains("cannot exceed", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_WildcardPositionZero_ThrowsException()
    {
        // Positions are 1-indexed
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*", new[] { 0 }));

        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_WildcardPositionOutOfRange_ThrowsException()
    {
        // Filter has only 1 wildcard, position 2 is out of range
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*", new[] { 2 }));

        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_DuplicateWildcardPosition_ThrowsException()
    {
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*.*", new[] { 1, 1 }));

        Assert.Contains("Duplicate", ex.Message);
    }

    [Fact]
    public void ValidateFilterAndWildcards_ValidMultipleWildcardsWithSubset_NoException()
    {
        // Filter has 3 wildcards, using only 2 for partitioning
        NatsPcgMemberMappingValidator.ValidateFilterAndWildcards("foo.*.*.*.>", new[] { 1, 3 });
    }

    [Fact]
    public void ValidatePartitioningFilters_ValidMultipleFilters_NoException()
    {
        NatsPcgMemberMappingValidator.ValidatePartitioningFilters(new[]
        {
            new NatsPcgPartitioningFilter("orders.*", new[] { 1 }),
            new NatsPcgPartitioningFilter("refunds.*", new[] { 1 }),
        });
    }

    [Fact]
    public void ValidatePartitioningFilters_NullFilters_NoException()
    {
        NatsPcgMemberMappingValidator.ValidatePartitioningFilters(null!);
    }

    [Fact]
    public void ValidatePartitioningFilters_EmptyFilters_NoException()
    {
        NatsPcgMemberMappingValidator.ValidatePartitioningFilters(Array.Empty<NatsPcgPartitioningFilter>());
    }

    [Fact]
    public void ValidatePartitioningFilters_FilterWithoutWildcard_ThrowsException()
    {
        var ex = Assert.Throws<NatsPcgConfigurationException>(() =>
            NatsPcgMemberMappingValidator.ValidatePartitioningFilters(new[]
            {
                new NatsPcgPartitioningFilter("orders.*", new[] { 1 }),
                new NatsPcgPartitioningFilter("refunds.bar", new[] { 1 }),
            }));

        Assert.Contains("at least one '*' wildcard", ex.Message);
    }

    [Fact]
    public void ValidatePartitioningFilters_EmptyWildcards_FullSubject_NoException()
    {
        NatsPcgMemberMappingValidator.ValidatePartitioningFilters(new[]
        {
            new NatsPcgPartitioningFilter("orders.*", Array.Empty<int>()),
            new NatsPcgPartitioningFilter("refunds.*", Array.Empty<int>()),
        });
    }

    [Fact]
    public void IsPartitionByFullSubject_EmptyArray_ReturnsTrue()
    {
        Assert.True(NatsPcgMemberMappingValidator.IsPartitionByFullSubject(Array.Empty<int>()));
    }

    [Fact]
    public void IsPartitionByFullSubject_ExplicitPositions_ReturnsFalse()
    {
        Assert.False(NatsPcgMemberMappingValidator.IsPartitionByFullSubject(new[] { 1 }));
    }

    [Fact]
    public void IsPartitionByFullSubject_NullArray_ReturnsFalse()
    {
        Assert.False(NatsPcgMemberMappingValidator.IsPartitionByFullSubject(null!));
    }
}
