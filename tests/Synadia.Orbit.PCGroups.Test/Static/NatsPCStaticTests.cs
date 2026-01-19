// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups;
using Synadia.Orbit.PCGroups.Static;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.PCGroups.Test.Static;

[Collection("nats-server")]
public class NatsPCStaticTests
{
    private readonly NatsServerFixture _server;

    public NatsPCStaticTests(NatsServerFixture server) => _server = server;

    [Fact]
    public async Task CreateAndGetConfig_Success()
    {
        await using var nats = new NatsClient(_server.Url);
        var js = nats.CreateJetStreamContext();

        // Create a stream
        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "test.>" },
        });

        try
        {
            // Create a static consumer group
            var groupName = $"test-group-{Guid.NewGuid():N}";
            var config = await NatsPCStatic.CreateAsync(
                js,
                streamName,
                groupName,
                maxNumMembers: 3,
                filter: "test.>");

            Assert.Equal(3u, config.MaxMembers);
            Assert.Equal("test.>", config.Filter);
            Assert.Null(config.Members);
            Assert.Null(config.MemberMappings);

            // Get the config back
            var retrieved = await NatsPCStatic.GetConfigAsync(js, streamName, groupName);
            Assert.Equal(config.MaxMembers, retrieved.MaxMembers);
            Assert.Equal(config.Filter, retrieved.Filter);

            // Clean up
            await NatsPCStatic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task CreateWithMembers_Success()
    {
        await using var nats = new NatsClient(_server.Url);
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "test.>" },
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";
            var members = new[] { "member1", "member2" };

            var config = await NatsPCStatic.CreateAsync(
                js,
                streamName,
                groupName,
                maxNumMembers: 3,
                members: members);

            Assert.NotNull(config.Members);
            Assert.Equal(members, config.Members);

            // Verify membership check
            Assert.True(config.IsInMembership("member1"));
            Assert.True(config.IsInMembership("member2"));
            Assert.False(config.IsInMembership("member3"));

            await NatsPCStatic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task CreateWithMemberMappings_Success()
    {
        await using var nats = new NatsClient(_server.Url);
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "test.>" },
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";
            var mappings = new[]
            {
                new NatsPCMemberMapping("member1", new[] { 0, 1 }),
                new NatsPCMemberMapping("member2", new[] { 2 }),
            };

            var config = await NatsPCStatic.CreateAsync(
                js,
                streamName,
                groupName,
                maxNumMembers: 3,
                memberMappings: mappings);

            Assert.NotNull(config.MemberMappings);
            Assert.Equal(2, config.MemberMappings.Length);

            Assert.True(config.IsInMembership("member1"));
            Assert.True(config.IsInMembership("member2"));
            Assert.False(config.IsInMembership("member3"));

            await NatsPCStatic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task ListConsumerGroups_Success()
    {
        await using var nats = new NatsClient(_server.Url);
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "test.>" },
        });

        try
        {
            var groupName1 = $"group1-{Guid.NewGuid():N}";
            var groupName2 = $"group2-{Guid.NewGuid():N}";

            await NatsPCStatic.CreateAsync(js, streamName, groupName1, maxNumMembers: 3);
            await NatsPCStatic.CreateAsync(js, streamName, groupName2, maxNumMembers: 3);

            var groups = new List<string>();
            await foreach (var group in NatsPCStatic.ListAsync(js, streamName))
            {
                groups.Add(group);
            }

            Assert.Contains(groupName1, groups);
            Assert.Contains(groupName2, groups);

            await NatsPCStatic.DeleteAsync(js, streamName, groupName1);
            await NatsPCStatic.DeleteAsync(js, streamName, groupName2);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public void PartitionDistributor_AutoDistributes()
    {
        var members = new[] { "a", "b", "c" };

        var filtersA = NatsPCPartitionDistributor.GeneratePartitionFilters(members, 6, null, "a");
        var filtersB = NatsPCPartitionDistributor.GeneratePartitionFilters(members, 6, null, "b");
        var filtersC = NatsPCPartitionDistributor.GeneratePartitionFilters(members, 6, null, "c");

        // With 6 partitions and 3 members (sorted: a, b, c):
        // a (index 0) gets partitions where i % 3 == 0: 0, 3
        // b (index 1) gets partitions where i % 3 == 1: 1, 4
        // c (index 2) gets partitions where i % 3 == 2: 2, 5
        Assert.Equal(new[] { "0.>", "3.>" }, filtersA);
        Assert.Equal(new[] { "1.>", "4.>" }, filtersB);
        Assert.Equal(new[] { "2.>", "5.>" }, filtersC);
    }

    [Fact]
    public void PartitionDistributor_UsesExplicitMappings()
    {
        var mappings = new[]
        {
            new NatsPCMemberMapping("member1", new[] { 0, 2, 4 }),
            new NatsPCMemberMapping("member2", new[] { 1, 3, 5 }),
        };

        var filters1 = NatsPCPartitionDistributor.GeneratePartitionFilters(null, 6, mappings, "member1");
        var filters2 = NatsPCPartitionDistributor.GeneratePartitionFilters(null, 6, mappings, "member2");

        Assert.Equal(new[] { "0.>", "2.>", "4.>" }, filters1);
        Assert.Equal(new[] { "1.>", "3.>", "5.>" }, filters2);
    }

    [Fact]
    public async Task Validation_MembersAndMappingsMutuallyExclusive()
    {
        await using var nats = new NatsClient(_server.Url);
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "test.>" },
        });

        try
        {
            var exception = await Assert.ThrowsAsync<NatsPCConfigurationException>(() =>
                NatsPCStatic.CreateAsync(
                    js,
                    streamName,
                    "test-group",
                    maxNumMembers: 3,
                    members: new[] { "member1" },
                    memberMappings: new[] { new NatsPCMemberMapping("member1", new[] { 0 }) }));

            Assert.Contains("Cannot specify both", exception.Message);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task Validation_MaxMembersMustBePositive()
    {
        await using var nats = new NatsClient(_server.Url);
        var js = nats.CreateJetStreamContext();

        var exception = await Assert.ThrowsAsync<NatsPCConfigurationException>(() =>
            NatsPCStatic.CreateAsync(
                js,
                "stream",
                "group",
                maxNumMembers: 0));

        Assert.Contains("must be greater than 0", exception.Message);
    }
}
