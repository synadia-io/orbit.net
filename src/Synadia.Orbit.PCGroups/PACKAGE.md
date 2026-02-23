# Synadia.Orbit.PCGroups

Partitioned Consumer Groups (PCGroups) for NATS JetStream. This library enables horizontal scaling of message processing by automatically distributing partitions across consumer group members.

**Requirements:** NATS Server 2.11+ (for Priority Groups/Pinning support)

## Features

- **Static Consumer Groups**: Fixed membership defined at creation time
- **Elastic Consumer Groups**: Dynamic membership changes at runtime
- **Automatic Partition Distribution**: Partitions are automatically balanced across members
- **Custom Partition Mappings**: Explicit control over which partitions each member handles
- **Self-Healing**: Consumers automatically recover from failures
- **KV-based Configuration**: Group configurations stored in NATS KV for coordination

## Installation

```bash
dotnet add package Synadia.Orbit.PCGroups
```

## Quick Start

### Static Consumer Groups

Static groups have a fixed membership that cannot change after creation. The stream must be configured with a subject transform to add partition prefixes.

```csharp
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups.Static;

await using var nats = new NatsClient();
var js = nats.CreateJetStreamContext();

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

// Publish messages - they get transformed to {partition}.orders.{id}
await js.PublishAsync("orders.123", new Order("ORD-123", "CUST-1", 99.99m));

// Start consuming using async enumerable
await foreach (var msg in js.ConsumePcgStaticAsync<Order>(
    streamName: "orders",
    consumerGroupName: "order-processors",
    memberName: "worker-1"))
{
    Console.WriteLine($"Processing order: {msg.Subject} - {msg.Data}");
    await msg.AckAsync();
}

record Order(string OrderId, string CustomerId, decimal Amount);
```

### Elastic Consumer Groups

Elastic groups allow dynamic membership changes at runtime. The library automatically creates a work-queue stream with the appropriate transforms.

```csharp
using System.Threading.Channels;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups;
using Synadia.Orbit.PCGroups.Elastic;

await using var nats = new NatsClient();
var js = nats.CreateJetStreamContext();

// Create the source stream (no transform needed - elastic creates work-queue stream)
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

// Publish some messages
await js.PublishAsync("events.user123", new Event("EVT-1", "user123", "click"));

// Use a channel to aggregate messages from multiple workers
var channel = Channel.CreateUnbounded<(string Worker, NatsPcgMsg<Event> Msg)>();
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

// Process messages from all workers
await foreach (var (worker, msg) in channel.Reader.ReadAllAsync(cts.Token))
{
    Console.WriteLine($"[{worker}] Processing event: {msg.Subject} - {msg.Data}");
    await msg.AckAsync();
}

record Event(string EventId, string UserId, string Type);
```

## Static vs Elastic Comparison

| Feature | Static | Elastic |
|---------|--------|---------|
| Membership | Fixed at creation | Dynamic at runtime |
| Use Case | Stable workloads | Scaling workloads |
| Stream Setup | Requires SubjectTransform | Auto-creates work-queue stream |
| Configuration | Simpler | More flexible |

## Custom Partition Mappings

For fine-grained control over partition distribution:

```csharp
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups;
using Synadia.Orbit.PCGroups.Static;
using Synadia.Orbit.PCGroups.Elastic;

await using var nats = new NatsClient();
var js = nats.CreateJetStreamContext();

// Define explicit member-to-partition mappings
var mappings = new[]
{
    new NatsPcgMemberMapping("high-priority-worker", [0, 1, 2]),
    new NatsPcgMemberMapping("low-priority-worker", [3, 4, 5]),
};

// For static groups - stream needs subject transform
await js.CreateStreamAsync(new StreamConfig("orders", ["orders.*"])
{
    SubjectTransform = new SubjectTransform
    {
        Src = "orders.*",
        Dest = "{{partition(6,1)}}.orders.{{wildcard(1)}}",
    },
});

await js.CreatePcgStaticAsync("orders", "processors", maxNumMembers: 6,
    filter: "orders.*", memberMappings: mappings);

// For elastic groups - no transform needed on source stream
await js.CreateStreamAsync(new StreamConfig("events", ["events.*"]));

await js.CreatePcgElasticAsync("events", "processors", maxNumMembers: 6,
    filter: "events.*", partitioningWildcards: [1]);
await js.SetPcgElasticMemberMappingsAsync("events", "processors", mappings);
```

## Subject Transform Syntax

For static consumer groups, the stream must use subject transforms to add partition prefixes:

- `{{partition(N,wildcards)}}` - Computes partition (0 to N-1) based on specified wildcard positions
- `{{wildcard(N)}}` - References the Nth wildcard token from the source subject (1-indexed)

Example: For `orders.*` with transform `{{partition(3,1)}}.orders.{{wildcard(1)}}`:
- `orders.ABC` becomes `0.orders.ABC`, `1.orders.ABC`, or `2.orders.ABC` (based on hash of "ABC")

## API Reference

### Static Consumer Groups (Extension methods on `INatsJSContext`)

- `CreatePcgStaticAsync` - Create a new static consumer group
- `GetPcgStaticConfigAsync` - Get configuration for an existing group
- `ConsumePcgStaticAsync` - Start consuming messages (returns `IAsyncEnumerable<NatsPcgMsg<T>>`)
- `DeletePcgStaticAsync` - Delete a consumer group
- `ListPcgStaticAsync` - List all consumer groups for a stream
- `ListPcgStaticActiveMembersAsync` - List active members
- `PcgStaticMemberStepDownAsync` - Force a member to step down

### Elastic Consumer Groups (Extension methods on `INatsJSContext`)

- `CreatePcgElasticAsync` - Create a new elastic consumer group
- `GetPcgElasticConfigAsync` - Get configuration for an existing group
- `ConsumePcgElasticAsync` - Start consuming messages (returns `IAsyncEnumerable<NatsPcgMsg<T>>`)
- `DeletePcgElasticAsync` - Delete a consumer group and its work-queue stream
- `ListPcgElasticAsync` - List all consumer groups for a stream
- `ListPcgElasticActiveMembersAsync` - List active members
- `AddPcgElasticMembersAsync` - Add members to the group
- `DeletePcgElasticMembersAsync` - Remove members from the group
- `SetPcgElasticMemberMappingsAsync` - Set explicit partition mappings
- `DeletePcgElasticMemberMappingsAsync` - Remove mappings (revert to auto-distribution)
- `IsInPcgElasticMembershipAndActiveAsync` - Check if a member is in the group and active
- `GetPcgElasticPartitionFilters` - Get partition filters for a member (extension on `NatsPcgElasticConfig`)
- `PcgElasticMemberStepDownAsync` - Force a member to step down

### Validation (Static class `NatsPcgMemberMappingValidator`)

- `Validate` - Validate member mappings (checks for duplicates, overlaps, out-of-range partitions)
- `ValidateFilterAndWildcards` - Validate filter and partitioning wildcards for elastic groups

## How It Works

1. **Partitioning**: Messages are assigned to partitions (0 to maxMembers-1) based on subject content
2. **Distribution**: Partitions are distributed across active members
3. **Pinning**: Each member "pins" to its assigned partitions using priority groups
4. **Coordination**: Configuration stored in NATS KV enables coordination
5. **Self-healing**: Members watch for configuration changes and automatically adjust

## License

Apache License 2.0
