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

### Single Scheduled Message (NATS Server 2.12+)

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

### Repeating Interval Schedule (NATS Server 2.12+)

Use the `TimeSpan` constructor for repeating schedules (minimum interval is 1 second):

```csharp
var schedule = new NatsMsgSchedule(TimeSpan.FromMinutes(5), "events.periodic_check");

var ack = await js.PublishScheduledAsync(
    subject: "scheduling.repeater",
    data: "periodic payload",
    schedule: schedule);

ack.EnsureSuccess();
```

### Schedule with Source Subject (NATS Server 2.14+)

When a schedule fires, instead of using the schedule message's own data, it can source the latest
message from another subject in the same stream. The source subject must be a literal (no wildcards)
and must not match the schedule or target subjects.

```csharp
var stream = await js.CreateStreamAsync(new StreamConfig("SCHEDULING_STREAM", ["foo.*"])
{
    AllowMsgSchedules = true,
    AllowMsgTTL = true,
    AllowDirect = true,
});

// Publish data that will be sourced by the schedule
await js.PublishAsync("foo.data", "latest sensor reading",
    headers: new NatsHeaders { { "Sensor", "A1" } });

// Schedule sources from foo.data and publishes to foo.output
var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", "foo.output")
{
    Source = "foo.data",
    Ttl = TimeSpan.FromMinutes(5), // Optional: requires AllowMsgTTL on stream
};

var ack = await js.PublishScheduledAsync("foo.schedule", (byte[]?)null, schedule);

ack.EnsureSuccess();
// When the schedule fires, the produced message on foo.output will have:
//   - Body: "latest sensor reading" (from the source subject)
//   - Header "Sensor": "A1" (from the source subject)
//   - Header "Nats-Scheduler": "foo.schedule"
//   - Header "Nats-Schedule-Next": "purge" (for @at schedules)
```

If no message exists on the source subject when the schedule fires, the schedule is removed
without producing a message.

### TTL Options

- Minimum TTL is 1 second
- Use `TimeSpan.MaxValue` to indicate the produced message should never expire (`"never"`)
- The stream must have `AllowMsgTTL = true` when using TTL

