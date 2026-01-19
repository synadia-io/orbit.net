// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.PCGroups.Test.Elastic;

[Collection("nats-server")]
public class NatsPCElasticTests
{
    private readonly NatsServerFixture _server;

    public NatsPCElasticTests(NatsServerFixture server) => _server = server;

    [Fact]
    public async Task CreateAndGetConfig_Success()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        // Create a stream
        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "orders.>" },
        });

        try
        {
            // Create an elastic consumer group
            var groupName = $"test-group-{Guid.NewGuid():N}";
            var config = await NatsPCElastic.CreateAsync(
                js,
                streamName,
                groupName,
                maxNumMembers: 3,
                filter: "orders.*",
                partitioningWildcards: new[] { 1 });

            Assert.Equal(3u, config.MaxMembers);
            Assert.Equal("orders.*", config.Filter);
            Assert.Equal(new[] { 1 }, config.PartitioningWildcards);
            Assert.Null(config.Members);
            Assert.Null(config.MemberMappings);

            // Get the config back
            var retrieved = await NatsPCElastic.GetConfigAsync(js, streamName, groupName);
            Assert.Equal(config.MaxMembers, retrieved.MaxMembers);
            Assert.Equal(config.Filter, retrieved.Filter);
            Assert.Equal(config.PartitioningWildcards, retrieved.PartitioningWildcards);

            // Clean up
            await NatsPCElastic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task AddAndRemoveMembers_Success()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "orders.>" },
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";
            await NatsPCElastic.CreateAsync(
                js,
                streamName,
                groupName,
                maxNumMembers: 3,
                filter: "orders.*",
                partitioningWildcards: new[] { 1 });

            // Add members
            var members = await NatsPCElastic.AddMembersAsync(
                js, streamName, groupName, new[] { "member1", "member2" });
            Assert.Contains("member1", members);
            Assert.Contains("member2", members);

            // Get config and verify
            var config = await NatsPCElastic.GetConfigAsync(js, streamName, groupName);
            Assert.True(config.IsInMembership("member1"));
            Assert.True(config.IsInMembership("member2"));

            // Remove a member
            members = await NatsPCElastic.DeleteMembersAsync(
                js, streamName, groupName, new[] { "member1" });
            Assert.DoesNotContain("member1", members);
            Assert.Contains("member2", members);

            // Verify
            config = await NatsPCElastic.GetConfigAsync(js, streamName, groupName);
            Assert.False(config.IsInMembership("member1"));
            Assert.True(config.IsInMembership("member2"));

            await NatsPCElastic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task SetMemberMappings_Success()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "orders.>" },
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";
            await NatsPCElastic.CreateAsync(
                js,
                streamName,
                groupName,
                maxNumMembers: 4,
                filter: "orders.*",
                partitioningWildcards: new[] { 1 });

            // Set explicit mappings
            var mappings = new[]
            {
                new NatsPCMemberMapping("worker-a", new[] { 0, 1 }),
                new NatsPCMemberMapping("worker-b", new[] { 2, 3 }),
            };

            await NatsPCElastic.SetMemberMappingsAsync(js, streamName, groupName, mappings);

            var config = await NatsPCElastic.GetConfigAsync(js, streamName, groupName);
            Assert.NotNull(config.MemberMappings);
            Assert.Equal(2, config.MemberMappings.Length);
            Assert.True(config.IsInMembership("worker-a"));
            Assert.True(config.IsInMembership("worker-b"));
            Assert.False(config.IsInMembership("worker-c"));

            // Delete mappings
            await NatsPCElastic.DeleteMemberMappingsAsync(js, streamName, groupName);
            config = await NatsPCElastic.GetConfigAsync(js, streamName, groupName);
            Assert.Null(config.MemberMappings);

            await NatsPCElastic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task ListConsumerGroups_Success()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "orders.>" },
        });

        try
        {
            var groupName1 = $"group1-{Guid.NewGuid():N}";
            var groupName2 = $"group2-{Guid.NewGuid():N}";

            await NatsPCElastic.CreateAsync(js, streamName, groupName1, 3, "orders.*", new[] { 1 });
            await NatsPCElastic.CreateAsync(js, streamName, groupName2, 3, "orders.*", new[] { 1 });

            var groups = new List<string>();
            await foreach (var group in NatsPCElastic.ListAsync(js, streamName))
            {
                groups.Add(group);
            }

            Assert.Contains(groupName1, groups);
            Assert.Contains(groupName2, groups);

            await NatsPCElastic.DeleteAsync(js, streamName, groupName1);
            await NatsPCElastic.DeleteAsync(js, streamName, groupName2);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task GetPartitionFilters_Works()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "orders.>" },
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";
            await NatsPCElastic.CreateAsync(js, streamName, groupName, 4, "orders.*", new[] { 1 });

            // Add members
            await NatsPCElastic.AddMembersAsync(js, streamName, groupName, new[] { "a", "b" });

            var config = await NatsPCElastic.GetConfigAsync(js, streamName, groupName);

            // With 4 partitions and 2 members (a, b):
            // a gets 0, 2
            // b gets 1, 3
            var filtersA = NatsPCElastic.GetPartitionFilters(config, "a");
            var filtersB = NatsPCElastic.GetPartitionFilters(config, "b");

            Assert.Equal(new[] { "0.>", "2.>" }, filtersA);
            Assert.Equal(new[] { "1.>", "3.>" }, filtersB);

            await NatsPCElastic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task Validation_FilterRequired()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var exception = await Assert.ThrowsAsync<NatsPCConfigurationException>(() =>
            NatsPCElastic.CreateAsync(
                js,
                "stream",
                "group",
                maxNumMembers: 3,
                filter: string.Empty,
                partitioningWildcards: new[] { 1 }));

        Assert.Contains("filter is required", exception.Message);
    }

    [Fact]
    public async Task Validation_PartitioningWildcardsRequired()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var exception = await Assert.ThrowsAsync<NatsPCConfigurationException>(() =>
            NatsPCElastic.CreateAsync(
                js,
                "stream",
                "group",
                maxNumMembers: 3,
                filter: "orders.*",
                partitioningWildcards: Array.Empty<int>()));

        Assert.Contains("partitioningWildcards must contain at least one element", exception.Message);
    }

    [Fact]
    public async Task Validation_CannotModifyMembersWhenMappingsDefined()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = new[] { "orders.>" },
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";
            await NatsPCElastic.CreateAsync(js, streamName, groupName, 3, "orders.*", new[] { 1 });

            // Set mappings
            await NatsPCElastic.SetMemberMappingsAsync(js, streamName, groupName, new[]
            {
                new NatsPCMemberMapping("m1", new[] { 0, 1, 2 }),
            });

            // Try to add members - should fail
            var exception = await Assert.ThrowsAsync<NatsPCConfigurationException>(() =>
                NatsPCElastic.AddMembersAsync(js, streamName, groupName, new[] { "new-member" }));

            Assert.Contains("Cannot modify members when member mappings are defined", exception.Message);

            await NatsPCElastic.DeleteAsync(js, streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }
}
