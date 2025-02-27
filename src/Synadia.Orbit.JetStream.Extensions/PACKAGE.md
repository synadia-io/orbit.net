# JetStream Extensions

These are utilities extends NATS JetStream client functionality.

## Direct batch
Direct batch is beta. It only works with the 2.11.x NATS Server.

The direct batch functionality leverages the direct message capabilities
introduced in NATS Server 2.11 The functionality is described in [ADR-31](https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-31.md).

```csharp
// dotnet add package Synadia.Orbit.JetStream.Extensions
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


