# JetStream Extensions

These are utilities extends NATS JetStream client functionality.

## Direct batch
Direct batch is beta. It only works with the 2.11.x NATS Server.

The direct batch functionality leverages the direct message capabilities
introduced in NATS Server 2.11 The functionality is described in [ADR-31](https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-31.md).

```csharp
// dotnet add package nats.net
// dotnet add package Synadia.Orbit.JetStream.Extensions --prerelease
await using var client = new NatsClient();
INatsJSContext js = client.CreateJetStreamContext();

await js.CreateStreamAsync(new StreamConfig(name, [suject]) { AllowDirect = true }, ct);

for (int i = 0; i < 10; i++)
{
    await js.PublishAsync(subject: suject, i, cancellationToken: ct);
}

StreamMsgBatchGetRequest request = new()
{
    Batch = 8,
    Seq = 1,
};

int count = 0;
await foreach (NatsMsg<int> msg in js.GetBatchDirectAsync<int>(name, request, cancellationToken: ct))
{
    Assert.Equal(count++, msg.Data);
    Console.WriteLine($"GetBatchDirectAsync: {msg.Data}");
}

Assert.Equal(8, count);
```
## Scheduled Messages

A stream can be configured to allow scheduled messages. A scheduled message is a message published
to a subject that will in turn publish the message to a target subject at a specified time or on a
repeating interval. See [ADR-51](https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-51.md)
for the full specification. The stream must have `AllowMsgSchedules = true`.

> **Server requirement:** Features marked 2.14+ are not yet released. To test them, install the
> server from main (requires [Go](https://go.dev/dl/)):
> ```bash
> go install github.com/nats-io/nats-server/v2@main
> ```

| Use case | Constructor | Source | Server version |
|----------|-------------|--------|----------------|
| Delayed publish | `NatsMsgSchedule(DateTimeOffset, target)` | null | 2.12+ |
| Recurring publish | `NatsMsgSchedule(TimeSpan, target)` | null | 2.14+ |
| Data sampling | `NatsMsgSchedule(TimeSpan, target) { Source = ... }` | set | 2.14+ |

> **Note:** Cron expressions and timezone support are defined in ADR-51 but not yet implemented
> in the server. The `NatsMsgSchedule(string, string)` raw constructor is available for forward
> compatibility with future schedule types.

### Delayed Publish (NATS Server 2.12+)

Use `@at` to deliver a message once at a future time:

```csharp
// dotnet add package nats.net
// dotnet add package synadia.orbit.jetstream.extensions --prerelease
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

var stream = await js.CreateStreamAsync(new StreamConfig("SCHEDULING_STREAM", ["scheduling.>", "events.>"])
{
    AllowMsgSchedules = true,
    AllowMsgTTL = true,
});

// Schedule a message for 10 seconds from now
var scheduleAt = DateTimeOffset.UtcNow.AddSeconds(10);
var schedule = new NatsMsgSchedule(scheduleAt, "events.it_is_time")
{
    Ttl = TimeSpan.FromSeconds(15), // Optional: TTL on the produced message
};

var ack = await js.PublishScheduledAsync(
    subject: "scheduling.check_later",
    data: $"message for later {scheduleAt}",
    schedule: schedule);

ack.EnsureSuccess();
```

### Recurring Publish (NATS Server 2.14+)

Use the `TimeSpan` constructor for repeating schedules (minimum interval is 1 second):

```csharp
var schedule = new NatsMsgSchedule(TimeSpan.FromMinutes(5), "events.periodic_check");

var ack = await js.PublishScheduledAsync(
    subject: "scheduling.repeater",
    data: "periodic payload",
    schedule: schedule);

ack.EnsureSuccess();
```

### Data Sampling with Source (NATS Server 2.14+)

Combine a repeating schedule with a source subject to periodically republish the latest message
from one subject to another. When the schedule fires, it sources the latest message's data and
headers from the source subject and publishes them to the target.

```csharp
var stream = await js.CreateStreamAsync(new StreamConfig("SENSORS", ["sensors.*"])
{
    AllowMsgSchedules = true,
    AllowMsgTTL = true,
});

// Sensor data is published to sensors.raw by some producer
// ...

// Sample the latest sensor reading every 5 minutes
var schedule = new NatsMsgSchedule(TimeSpan.FromMinutes(5), "sensors.sampled")
{
    Source = "sensors.raw",
    Ttl = TimeSpan.FromMinutes(6),
};

var ack = await js.PublishScheduledAsync("sensors.schedule", (byte[]?)null, schedule);

ack.EnsureSuccess();
// Every 5 minutes the server will:
//   1. Load the latest message from sensors.raw
//   2. Publish its data + headers to sensors.sampled
//   3. Add Nats-Scheduler and Nats-Schedule-Next headers
```

The source subject must be a literal (no wildcards) and must not match the schedule or target
subjects. If no message exists on the source subject when the schedule fires, the schedule is
removed without producing a message.

Source also works with one-shot `@at` schedules for a single delayed republish:

```csharp
var schedule = new NatsMsgSchedule(DateTimeOffset.UtcNow.AddMinutes(10), "sensors.snapshot")
{
    Source = "sensors.raw",
};
```

### TTL Options

- Minimum TTL is 1 second
- Use `TimeSpan.MaxValue` to indicate the produced message should never expire (`"never"`)
- The stream must have `AllowMsgTTL = true` when using TTL

