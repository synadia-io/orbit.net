// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

// dotnet add package nats.net
// dotnet add package Synadia.Orbit.PCGroups --prerelease
using System.Threading.Channels;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups;
using Synadia.Orbit.PCGroups.Static;
using Synadia.Orbit.PCGroups.Elastic;

namespace DocsExamples;

public class ExamplePCGroups
{
    public static async Task Run()
    {
        string hr = new('-', 50);

        Console.WriteLine(hr);
        Console.WriteLine("Example: Static Consumer Groups");
        {
            await using var nats = new NatsClient();
            var js = nats.CreateJetStreamContext();

            // Cleanup any previous runs
            await TryDeleteAsync(() => js.DeletePcgStaticAsync("orders", "order-processors"));
            await TryDeleteAsync(() => js.DeleteStreamAsync("orders"));

            try
            {
                // Create the stream with subject transform for partitioning
                // The transform adds a partition prefix based on the wildcard token
                await js.CreateStreamAsync(new StreamConfig("orders", ["orders.*"])
                {
                    SubjectTransform = new SubjectTransform
                    {
                        Src = "orders.*",
                        Dest = "{{partition(3,1)}}.orders.{{wildcard(1)}}",
                    },
                });

                // Create a static consumer group with 3 partitions
                await js.CreatePcgStaticAsync(
                    streamName: "orders",
                    consumerGroupName: "order-processors",
                    maxNumMembers: 3,
                    filter: "orders.*");

                // Publish some test messages - they get transformed to {partition}.orders.{id}
                for (int i = 0; i < 5; i++)
                {
                    await js.PublishAsync($"orders.{i}", new Order($"ORD-{i}", $"CUST-{i}", 100m + i));
                }

                Console.WriteLine("Published 5 orders, consuming...");

                // Start consuming using async enumerable
                int count = 0;
                await foreach (var msg in js.ConsumePcgStaticAsync<Order>(
                    streamName: "orders",
                    consumerGroupName: "order-processors",
                    memberName: "worker-1"))
                {
                    Console.WriteLine($"Processing order: {msg.Subject} - {msg.Data}");
                    await msg.AckAsync();
                    if (++count >= 5) break;
                }

                Console.WriteLine("Static example completed.");
            }
            finally
            {
                await TryDeleteAsync(() => js.DeletePcgStaticAsync("orders", "order-processors"));
                await TryDeleteAsync(() => js.DeleteStreamAsync("orders"));
            }
        }

        Console.WriteLine(hr);
        Console.WriteLine("Example: Elastic Consumer Groups");
        {
            await using var nats = new NatsClient();
            var js = nats.CreateJetStreamContext();

            // Cleanup any previous runs
            await TryDeleteAsync(() => js.DeletePcgElasticAsync("events", "event-processors"));
            await TryDeleteAsync(() => js.DeleteStreamAsync("events"));

            try
            {
                // Create the stream first
                await js.CreateStreamAsync(new StreamConfig("events", ["events.*"]));

                // Create an elastic consumer group
                // Partitioning is based on the first wildcard token in the subject
                await js.CreatePcgElasticAsync(
                    streamName: "events",
                    consumerGroupName: "event-processors",
                    maxNumMembers: 10,
                    filter: "events.*",           // e.g., events.user123, events.user456
                    partitioningWildcards: [1]);  // Partition by the first wildcard (user ID)

                // Add members dynamically - partitions will be distributed across them
                string[] members = ["worker-1", "worker-2", "worker-3"];
                await js.AddPcgElasticMembersAsync("events", "event-processors", members);

                // Publish some test messages
                for (int i = 0; i < 5; i++)
                {
                    await js.PublishAsync($"events.user{i}", new Event($"EVT-{i}", $"user{i}", "click", $"payload-{i}"));
                }

                Console.WriteLine("Published 5 events, consuming with 3 workers...");

                // Use a channel to aggregate messages from all workers
                var channel = Channel.CreateUnbounded<(string Worker, INatsJSMsg<Event> Msg)>();
                using var cts = new CancellationTokenSource();

                // Start a consumer task for each worker
                var consumerTasks = members.Select(worker => Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var msg in js.ConsumePcgElasticAsync<Event>(
                            streamName: "events",
                            consumerGroupName: "event-processors",
                            memberName: worker,
                            cancellationToken: cts.Token))
                        {
                            await channel.Writer.WriteAsync((worker, msg), cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelled
                    }
                })).ToArray();

                // Read from channel until we have all messages
                int count = 0;
                await foreach (var (worker, msg) in channel.Reader.ReadAllAsync(cts.Token))
                {
                    Console.WriteLine($"[{worker}] Processing event: {msg.Subject} - {msg.Data}");
                    await msg.AckAsync();
                    if (++count >= 5)
                    {
                        cts.Cancel();
                        break;
                    }
                }

                // Wait for consumer tasks to complete
                await Task.WhenAll(consumerTasks);

                Console.WriteLine("Elastic example completed.");
            }
            finally
            {
                await TryDeleteAsync(() => js.DeletePcgElasticAsync("events", "event-processors"));
                await TryDeleteAsync(() => js.DeleteStreamAsync("events"));
            }
        }

        Console.WriteLine(hr);
        Console.WriteLine("Example: Custom Partition Mappings");
        {
            await using var nats = new NatsClient();
            var js = nats.CreateJetStreamContext();

            // Cleanup any previous runs
            await TryDeleteAsync(() => js.DeletePcgStaticAsync("orders2", "processors"));
            await TryDeleteAsync(() => js.DeletePcgElasticAsync("events2", "processors"));
            await TryDeleteAsync(() => js.DeleteStreamAsync("orders2"));
            await TryDeleteAsync(() => js.DeleteStreamAsync("events2"));

            try
            {
                // Create streams first - static stream needs subject transform
                await js.CreateStreamAsync(new StreamConfig("orders2", ["orders2.*"])
                {
                    SubjectTransform = new SubjectTransform
                    {
                        Src = "orders2.*",
                        Dest = "{{partition(6,1)}}.orders2.{{wildcard(1)}}",
                    },
                });
                await js.CreateStreamAsync(new StreamConfig("events2", ["events2.*"]));

                // Define explicit member-to-partition mappings
                var mappings = new[]
                {
                    new NatsPcgMemberMapping("high-priority-worker", [0, 1, 2]),
                    new NatsPcgMemberMapping("low-priority-worker", [3, 4, 5]),
                };

                await js.CreatePcgStaticAsync("orders2", "processors", maxNumMembers: 6,
                    filter: "orders2.*", memberMappings: mappings);

                // For elastic groups
                await js.CreatePcgElasticAsync("events2", "processors", maxNumMembers: 6,
                    filter: "events2.*", partitioningWildcards: [1]);
                await js.SetPcgElasticMemberMappingsAsync("events2", "processors", mappings);

                Console.WriteLine("Created consumer groups with custom mappings.");
                Console.WriteLine("Custom mappings example completed.");
            }
            finally
            {
                await TryDeleteAsync(() => js.DeletePcgStaticAsync("orders2", "processors"));
                await TryDeleteAsync(() => js.DeletePcgElasticAsync("events2", "processors"));
                await TryDeleteAsync(() => js.DeleteStreamAsync("orders2"));
                await TryDeleteAsync(() => js.DeleteStreamAsync("events2"));
            }
        }
    }

    private static async Task TryDeleteAsync(Func<ValueTask<bool>> deleteAction)
    {
        try
        {
            await deleteAction();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private static async Task TryDeleteAsync(Func<Task> deleteAction)
    {
        try
        {
            await deleteAction();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }
}

// Sample types for the examples
public record Order(string OrderId, string CustomerId, decimal Amount);

public record Event(string EventId, string UserId, string Type, string Payload);
