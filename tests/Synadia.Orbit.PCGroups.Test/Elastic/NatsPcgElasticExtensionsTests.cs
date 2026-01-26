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

        Assert.Contains("required", exception.Message);
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

        Assert.Contains("at least one element", exception.Message);
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

    [Fact]
    public async Task ConsumeElastic_ReceivesAllMessages()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";

        // Create source stream (elastic creates work-queue stream with transforms)
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = ["events.*"],
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";

            // Create elastic consumer group
            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 10,
                filter: "events.*",
                partitioningWildcards: [1]);

            // Add members
            await js.AddPcgElasticMembersAsync(streamName, groupName, ["w1", "w2", "w3"]);

            // Publish test messages
            const int messageCount = 10;
            for (int i = 0; i < messageCount; i++)
            {
                await js.PublishAsync($"events.user{i}", $"payload-{i}");
            }

            // Consume with all members concurrently using a channel
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();

            var consumerTasks = new[] { "w1", "w2", "w3" }.Select(worker => Task.Run(async () =>
            {
                try
                {
                    await foreach (var msg in js.ConsumePcgElasticAsync<string>(streamName, groupName, worker, cancellationToken: cts.Token))
                    {
                        receivedMessages.Add($"{worker}:{msg.Subject}");
                        await msg.AckAsync();
                        if (receivedMessages.Count >= messageCount)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            })).ToArray();

            // Wait for all messages or timeout
            while (receivedMessages.Count < messageCount && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(100, cts.Token);
            }

            cts.Cancel();
            await Task.WhenAll(consumerTasks);

            // Verify all messages received
            Assert.Equal(messageCount, receivedMessages.Count);

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task ConsumeElastic_StripsPartitionPrefix()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";

        // Create source stream
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = ["test.*"],
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";

            // Create elastic consumer group
            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 10,
                filter: "test.*",
                partitioningWildcards: [1]);

            // Add single member (gets all partitions)
            await js.AddPcgElasticMembersAsync(streamName, groupName, ["worker"]);

            // Publish a message
            await js.PublishAsync("test.mykey", "payload");

            // Consume and verify subject is stripped
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await foreach (var msg in js.ConsumePcgElasticAsync<string>(streamName, groupName, "worker", cancellationToken: cts.Token))
            {
                // Subject should be "test.mykey", not "{partition}.test.mykey"
                Assert.Equal("test.mykey", msg.Subject);
                await msg.AckAsync();
                break;
            }

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task ConsumeElastic_EachMemberGetsOwnConsumer()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var streamName = $"test-stream-{Guid.NewGuid():N}";

        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = ["events.*"],
        });

        try
        {
            var groupName = $"test-group-{Guid.NewGuid():N}";

            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 4,
                filter: "events.*",
                partitioningWildcards: [1]);

            // Add two members
            await js.AddPcgElasticMembersAsync(streamName, groupName, ["memberA", "memberB"]);

            // Publish messages that will go to different partitions
            await js.PublishAsync("events.key1", "payload1");
            await js.PublishAsync("events.key2", "payload2");
            await js.PublishAsync("events.key3", "payload3");
            await js.PublishAsync("events.key4", "payload4");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var memberAMessages = new List<string>();
            var memberBMessages = new List<string>();

            var taskA = Task.Run(async () =>
            {
                try
                {
                    await foreach (var msg in js.ConsumePcgElasticAsync<string>(streamName, groupName, "memberA", cancellationToken: cts.Token))
                    {
                        memberAMessages.Add(msg.Subject);
                        await msg.AckAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            var taskB = Task.Run(async () =>
            {
                try
                {
                    await foreach (var msg in js.ConsumePcgElasticAsync<string>(streamName, groupName, "memberB", cancellationToken: cts.Token))
                    {
                        memberBMessages.Add(msg.Subject);
                        await msg.AckAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            // Wait for all 4 messages
            while (memberAMessages.Count + memberBMessages.Count < 4 && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(100, cts.Token);
            }

            cts.Cancel();
            await Task.WhenAll(taskA, taskB);

            // Both members should have received messages (partitions are distributed)
            Assert.True(memberAMessages.Count > 0, "memberA should receive messages");
            Assert.True(memberBMessages.Count > 0, "memberB should receive messages");
            Assert.Equal(4, memberAMessages.Count + memberBMessages.Count);

            // Messages should not overlap (each partition goes to exactly one member)
            var allMessages = memberAMessages.Concat(memberBMessages).ToList();
            Assert.Equal(allMessages.Distinct().Count(), allMessages.Count);

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }
}
