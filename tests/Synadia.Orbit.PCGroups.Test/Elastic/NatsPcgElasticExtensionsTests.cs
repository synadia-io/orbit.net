// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable SuggestVarOrType_BuiltInTypes
using NATS.Client.Core;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.PCGroups.Test.Elastic;

[Collection("nats-server")]
public class NatsPcgElasticExtensionsTests
{
    private readonly NatsServerFixture _server;

    public NatsPcgElasticExtensionsTests(NatsServerFixture server) => _server = server;

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
            var config = await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 3,
                filter: "orders.*",
                partitioningWildcards: [1]);

            Assert.Equal(3u, config.MaxMembers);
            Assert.Equal("orders.*", config.Filter);
            Assert.Equal([1], config.PartitioningWildcards);
            Assert.Null(config.Members);
            Assert.Null(config.MemberMappings);

            // Get the config back
            var retrieved = await js.GetPcgElasticConfigAsync(streamName, groupName);
            Assert.Equal(config.MaxMembers, retrieved.MaxMembers);
            Assert.Equal(config.Filter, retrieved.Filter);
            Assert.Equal(config.PartitioningWildcards, retrieved.PartitioningWildcards);

            // Clean up
            await js.DeletePcgElasticAsync(streamName, groupName);
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
            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 3,
                filter: "orders.*",
                partitioningWildcards: [1]);

            // Add members
            var members = await js.AddPcgElasticMembersAsync(streamName, groupName, ["member1", "member2"]);
            Assert.Contains("member1", members);
            Assert.Contains("member2", members);

            // Get config and verify
            var config = await js.GetPcgElasticConfigAsync(streamName, groupName);
            Assert.True(config.IsInMembership("member1"));
            Assert.True(config.IsInMembership("member2"));

            // Remove a member
            members = await js.DeletePcgElasticMembersAsync(streamName, groupName, ["member1"]);
            Assert.DoesNotContain("member1", members);
            Assert.Contains("member2", members);

            // Verify
            config = await js.GetPcgElasticConfigAsync(streamName, groupName);
            Assert.False(config.IsInMembership("member1"));
            Assert.True(config.IsInMembership("member2"));

            await js.DeletePcgElasticAsync(streamName, groupName);
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
            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 4,
                filter: "orders.*",
                partitioningWildcards: [1]);

            // Set explicit mappings
            var mappings = new[]
            {
                new NatsPcgMemberMapping("worker-a", [0, 1]),
                new NatsPcgMemberMapping("worker-b", [2, 3]),
            };

            await js.SetPcgElasticMemberMappingsAsync(streamName, groupName, mappings);

            var config = await js.GetPcgElasticConfigAsync(streamName, groupName);
            Assert.NotNull(config.MemberMappings);
            Assert.Equal(2, config.MemberMappings.Length);
            Assert.True(config.IsInMembership("worker-a"));
            Assert.True(config.IsInMembership("worker-b"));
            Assert.False(config.IsInMembership("worker-c"));

            // Delete mappings
            await js.DeletePcgElasticMemberMappingsAsync(streamName, groupName);
            config = await js.GetPcgElasticConfigAsync(streamName, groupName);
            Assert.Null(config.MemberMappings);

            await js.DeletePcgElasticAsync(streamName, groupName);
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

            await js.CreatePcgElasticAsync(streamName, groupName1, 3, "orders.*", [1]);
            await js.CreatePcgElasticAsync(streamName, groupName2, 3, "orders.*", [1]);

            var groups = new List<string>();
            await foreach (var group in js.ListPcgElasticAsync(streamName))
            {
                groups.Add(group);
            }

            Assert.Contains(groupName1, groups);
            Assert.Contains(groupName2, groups);

            await js.DeletePcgElasticAsync(streamName, groupName1);
            await js.DeletePcgElasticAsync(streamName, groupName2);
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
            await js.CreatePcgElasticAsync(streamName, groupName, 4, "orders.*", [1]);

            // Add members
            await js.AddPcgElasticMembersAsync(streamName, groupName, ["a", "b"]);

            var config = await js.GetPcgElasticConfigAsync(streamName, groupName);

            // With 4 partitions and 2 members (a, b):
            // a gets 0, 2
            // b gets 1, 3
            var filtersA = config.GetPcgElasticPartitionFilters("a");
            var filtersB = config.GetPcgElasticPartitionFilters("b");

            Assert.Equal(new[] { "0.>", "2.>" }, filtersA);
            Assert.Equal(new[] { "1.>", "3.>" }, filtersB);

            await js.DeletePcgElasticAsync(streamName, groupName);
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

        var exception = await Assert.ThrowsAsync<NatsPcgConfigurationException>(() =>
            js.CreatePcgElasticAsync(
                "stream",
                "group",
                maxNumMembers: 3,
                filter: string.Empty,
                partitioningWildcards: [1]));

        Assert.Contains("filter is required", exception.Message);
    }

    [Fact]
    public async Task Validation_PartitioningWildcardsRequired()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var exception = await Assert.ThrowsAsync<NatsPcgConfigurationException>(() =>
            js.CreatePcgElasticAsync(
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
            await js.CreatePcgElasticAsync(streamName, groupName, 3, "orders.*", [1]);

            // Set mappings
            await js.SetPcgElasticMemberMappingsAsync(streamName, groupName, [
                new NatsPcgMemberMapping("m1", [0, 1, 2])
            ]);

            // Try to add members - should fail
            var exception = await Assert.ThrowsAsync<NatsPcgConfigurationException>(() =>
                js.AddPcgElasticMembersAsync(streamName, groupName, ["new-member"]));

            Assert.Contains("Cannot modify members when member mappings are defined", exception.Message);

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }
}
