# JetStream Publisher

Higher-level publishers for NATS JetStream:

- **Backpressure publisher** (`JetStreamPublisher<T>`) — high-throughput publishing with
  an async ack channel so producers can pipeline without awaiting every ack inline.
- **Atomic batch publisher** (`NatsJSBatchPublisher`) — stage a group of messages and
  commit them atomically (all-or-nothing), with optional flow control. Requires NATS
  Server 2.12+ and `AllowAtomicPublish = true` on the stream. See
  [ADR-50](https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-50.md).
- **Fast-ingest publisher** (`NatsJSFastPublisher`) — high-throughput batch publishing
  without atomicity. Server-driven flow control, optional per-message gap reporting.
  Requires NATS Server 2.14+ and `AllowBatchPublish = true` on the stream.

## Backpressure Publisher

```csharp
// dotnet add package nats.net
// dotnet add package Synadia.Orbit.JetStream.Publisher
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

var stream = await js.CreateStreamAsync(new StreamConfig("TEST_STREAM", ["test.>"]));
await stream.PurgeAsync(new StreamPurgeRequest { Keep = 0 });

JetStreamPublisher<string> publisher = client.CreateOrbitJetStreamPublisher<string>();

await publisher.StartAsync();

int numberOfMessages = 1_000_000;

var sub = Task.Run(
    async () =>
    {
        int count = 0;
        await foreach (var status in publisher.SubscribeAsync())
        {
            count++;

            if (!status.Acknowledged)
            {
                Console.WriteLine($"Error publishing message: {status.Subject}: {status.Error.GetType().Name}");
            }

            if (count == numberOfMessages)
            {
                break;
            }
        }
    });

Stopwatch stopwatch = Stopwatch.StartNew();

for (int i = 0; i < numberOfMessages; i++)
{
    await publisher.PublishAsync($"test.msg{i}", $"Test message {i}");
}

await sub;

stopwatch.Stop();

Console.WriteLine($"Took {stopwatch.Elapsed} at {numberOfMessages / stopwatch.Elapsed.TotalSeconds:N0} msgs/sec");
```

## Atomic Batch Publishing

Stage messages with `AddAsync` / `AddMsgAsync` and finalize with `CommitAsync` /
`CommitMsgAsync`. The server persists the batch atomically: either every staged message
is written, or none are. Call `Discard()` to abandon a batch without committing. The
default server-side limit is 1000 messages per batch.

```csharp
// dotnet add package nats.net
// dotnet add package Synadia.Orbit.JetStream.Publisher
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

await js.CreateStreamAsync(new StreamConfig("ORDERS", ["orders.>"])
{
    AllowAtomicPublish = true,
});

await using var batch = new NatsJSBatchPublisher(js);

await batch.AddAsync("orders.1", "first"u8.ToArray());
await batch.AddAsync("orders.2", "second"u8.ToArray());

NatsJSBatchAck ack = await batch.CommitAsync("orders.3", "third"u8.ToArray());

Console.WriteLine($"Committed {ack.BatchSize} messages as batch {ack.BatchId} to {ack.Stream}");
```

For a one-shot batch of messages already in memory, use the `PublishMsgBatchAsync`
extension on `INatsJSContext`:

```csharp
var messages = new[]
{
    new NatsMsg<byte[]> { Subject = "orders.a", Data = "a"u8.ToArray() },
    new NatsMsg<byte[]> { Subject = "orders.b", Data = "b"u8.ToArray() },
    new NatsMsg<byte[]> { Subject = "orders.c", Data = "c"u8.ToArray() },
};

NatsJSBatchAck ack = await js.PublishMsgBatchAsync(messages);
```

### Per-message options

Pass `NatsJSBatchMsgOpts` to set per-message TTL and server-side expectations:

```csharp
await batch.AddAsync(
    "orders.1",
    "first"u8.ToArray(),
    new NatsJSBatchMsgOpts
    {
        Stream = "ORDERS",
        LastSeq = 42,
        Ttl = TimeSpan.FromMinutes(5),
    });
```

Supported options: `Ttl`, `Stream`, `LastSeq`, `LastSubjectSeq`, `LastSubject`.

### Flow control

Pass `NatsJSBatchFlowControl` to the publisher to wait for intermediate acks and avoid
overrunning the server:

```csharp
await using var batch = new NatsJSBatchPublisher(
    js,
    new NatsJSBatchFlowControl
    {
        AckFirst = true,                      // wait for ack on the first message
        AckEvery = 100,                       // then wait every Nth message
        AckTimeout = TimeSpan.FromSeconds(5),
    });
```

### Error handling

All batch publish errors derive from `NatsJSBatchPublishException`. Catch the base type
or a specific subtype per server error code:

| Exception | Error code |
|-----------|------------|
| `NatsJSBatchPublishNotEnabledException` | 10174 |
| `NatsJSBatchPublishMissingSeqException` | 10175 |
| `NatsJSBatchPublishIncompleteException` | 10176 |
| `NatsJSBatchPublishUnsupportedHeaderException` | 10177 |
| `NatsJSBatchPublishExceedsLimitException` | 10199 |

`NatsJSBatchClosedException` is thrown when using a publisher after commit or discard.
`NatsJSInvalidBatchAckException` is thrown when the server's batch ack response does not
match the committed batch.

## Fast-Ingest Batch Publishing

`NatsJSFastPublisher` delivers high-throughput batch publishing without atomicity.
Individual messages may be lost (gaps) and a partial batch may persist. The server
drives flow control: `AddAsync` / `AddMsgAsync` stalls when the configured outstanding
ack window is exceeded. Stream message-count limit per batch is unbounded.

```csharp
// dotnet add package nats.net
// dotnet add package Synadia.Orbit.JetStream.Publisher
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

await js.CreateStreamAsync(new StreamConfig("METRICS", ["metrics.>"])
{
    AllowBatchPublish = true,
});

await using var batch = js.CreateOrbitFastPublisher();

for (int i = 0; i < 100_000; i++)
{
    await batch.AddAsync($"metrics.{i % 16}", $"value-{i}"u8.ToArray());
}

NatsJSBatchAck ack = await batch.CloseAsync();
Console.WriteLine($"Persisted {ack.BatchSize} messages to {ack.Stream}");
```

Use `CommitAsync` / `CommitMsgAsync` to publish a final message and commit, or
`CloseAsync` to commit without storing a final message (end-of-batch).

### Flow control and gap handling

```csharp
await using var batch = js.CreateOrbitFastPublisher(new NatsJSFastPublisherOpts
{
    ContinueOnGap = true,                            // server reports gaps but keeps going
    ErrorHandler = ex => Console.WriteLine(ex),       // gaps and per-message errors
    FlowControl = new NatsJSFastPublishFlowControl
    {
        Flow = 200,
        MaxOutstandingAcks = 4,
        AckTimeout = TimeSpan.FromSeconds(5),
    },
});
```

When `ContinueOnGap` is false (default) the server abandons the batch on the first
gap. When true, gaps surface via `ErrorHandler` as `NatsJSFastPublishGapException`
and the batch continues. Per-message server errors arrive as
`NatsJSFastPublishMessageException`.
