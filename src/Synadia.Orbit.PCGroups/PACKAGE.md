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

Static groups have a fixed membership that cannot change after creation.

```csharp
using Synadia.Orbit.PCGroups.Static;
using NATS.Net;

await using var nats = new NatsClient("nats://localhost:4222");
var js = nats.CreateJetStreamContext();

// Create a static consumer group with 3 partitions
await NatsPCStatic.CreateAsync(
    js,
    streamName: "orders",
    consumerGroupName: "order-processors",
    maxNumMembers: 3,
    filter: "orders.>");

// Start consuming
var ctx = await NatsPCStatic.ConsumeAsync<Order>(
    js,
    streamName: "orders",
    consumerGroupName: "order-processors",
    memberName: "worker-1",
    messageHandler: async (msg, ct) =>
    {
        Console.WriteLine($"Processing order: {msg.Subject}");
        await msg.AckAsync(cancellationToken: ct);
    });

// Wait for completion or stop
ctx.Stop();
await ctx.WaitAsync();
```

### Elastic Consumer Groups

Elastic groups allow dynamic membership changes at runtime.

```csharp
using Synadia.Orbit.PCGroups.Elastic;
using NATS.Net;

await using var nats = new NatsClient("nats://localhost:4222");
var js = nats.CreateJetStreamContext();

// Create an elastic consumer group
// Partitioning is based on the first wildcard token in the subject
await NatsPCElastic.CreateAsync(
    js,
    streamName: "events",
    consumerGroupName: "event-processors",
    maxNumMembers: 10,
    filter: "events.*",           // e.g., events.user123, events.user456
    partitioningWildcards: [1]);  // Partition by the first wildcard (user ID)

// Add members dynamically
await NatsPCElastic.AddMembersAsync(
    js, "events", "event-processors",
    new[] { "worker-1", "worker-2", "worker-3" });

// Start consuming
var ctx = await NatsPCElastic.ConsumeAsync<Event>(
    js,
    streamName: "events",
    consumerGroupName: "event-processors",
    memberName: "worker-1",
    messageHandler: async (msg, ct) =>
    {
        Console.WriteLine($"Processing event: {msg.Data}");
        await msg.AckAsync(cancellationToken: ct);
    });

// Membership can be modified at runtime
await NatsPCElastic.AddMembersAsync(js, "events", "event-processors", new[] { "worker-4" });
await NatsPCElastic.DeleteMembersAsync(js, "events", "event-processors", new[] { "worker-2" });

ctx.Stop();
await ctx.WaitAsync();
```

## Static vs Elastic Comparison

| Feature | Static | Elastic |
|---------|--------|---------|
| Membership | Fixed at creation | Dynamic at runtime |
| Use Case | Stable workloads | Scaling workloads |
| Stream Type | Original stream | Work-queue stream (auto-created) |
| Configuration | Simple | More complex |

## Custom Partition Mappings

For fine-grained control over partition distribution:

```csharp
// Define explicit member-to-partition mappings
var mappings = new[]
{
    new NatsPCMemberMapping("high-priority-worker", new[] { 0, 1, 2 }),
    new NatsPCMemberMapping("low-priority-worker", new[] { 3, 4, 5 }),
};

await NatsPCStatic.CreateAsync(
    js, "orders", "processors", maxNumMembers: 6,
    memberMappings: mappings);

// For elastic groups
await NatsPCElastic.SetMemberMappingsAsync(
    js, "events", "processors", mappings);
```

## API Reference

### Static Consumer Groups (`NatsPCStatic`)

- `CreateAsync` - Create a new static consumer group
- `GetConfigAsync` - Get configuration for an existing group
- `ConsumeAsync` - Start consuming messages
- `DeleteAsync` - Delete a consumer group
- `ListAsync` - List all consumer groups for a stream
- `ListActiveMembersAsync` - List active members
- `MemberStepDownAsync` - Force a member to step down

### Elastic Consumer Groups (`NatsPCElastic`)

- `CreateAsync` - Create a new elastic consumer group
- `GetConfigAsync` - Get configuration for an existing group
- `ConsumeAsync` - Start consuming messages
- `DeleteAsync` - Delete a consumer group and its work-queue stream
- `ListAsync` - List all consumer groups for a stream
- `ListActiveMembersAsync` - List active members
- `AddMembersAsync` - Add members to the group
- `DeleteMembersAsync` - Remove members from the group
- `SetMemberMappingsAsync` - Set explicit partition mappings
- `DeleteMemberMappingsAsync` - Remove mappings (revert to auto-distribution)
- `IsInMembershipAndActiveAsync` - Check if a member is in the group and active
- `GetPartitionFilters` - Get partition filters for a member
- `MemberStepDownAsync` - Force a member to step down

## How It Works

1. **Partitioning**: Messages are assigned to partitions (0 to maxMembers-1) based on subject content
2. **Distribution**: Partitions are distributed across active members
3. **Pinning**: Each member "pins" to its assigned partitions using priority groups
4. **Coordination**: Configuration stored in NATS KV enables coordination
5. **Self-healing**: Members watch for configuration changes and automatically adjust

## License

Apache License 2.0
