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
## Single Scheduled Message

A stream can be configured to allow scheduled messages. A scheduled message is a message published
to a subject that will in turn publish the message to a target subject at a specified time in the future.
See also [ADR-51: Single scheduled message](https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-51.md#single-scheduled-message)
for more details. This feature requires NATS Server 2.12 or later.

```csharp
// dotnet add package nats.net
// dotnet add package synadia.orbit.jetstream.extensions --prerelease
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

Console.WriteLine("Create stream");
var stream = await js.CreateStreamAsync(new StreamConfig("SCHEDULING_STREAM", ["scheduling.>", "events.>"])
{
    AllowMsgSchedules = true,
    AllowMsgTTL = true,
});

// Schedule a message for 10 seconds from now
var scheduleAt = DateTimeOffset.UtcNow.AddSeconds(10);
var schedule = new NatsMsgSchedule(scheduleAt, "events.it_is_time")
{
    Ttl = TimeSpan.FromSeconds(15), // Optional
};

Console.WriteLine($"Scheduling message for: {scheduleAt:yyyy-MM-ddTHH:mm:ss}Z");

var ack = await js.PublishScheduledAsync(
    subject: "scheduling.check_later",
    data: $"message for later {scheduleAt}",
    schedule: schedule);

ack.EnsureSuccess();
Console.WriteLine($"Published scheduled message, seq={ack.Seq}");
```

