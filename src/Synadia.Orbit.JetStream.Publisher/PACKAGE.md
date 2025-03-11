# JetStream Publisher

JetStream Publisher is a new API designed to make publishing to JetStream more efficient and
provide better control to applications.

```csharp
// dotnet add package nats.net
// dotnet add package synadia.orbit.jetstream.publisher
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

Console.WriteLine("Creat stream");
var stream = await js.CreateStreamAsync(new StreamConfig("TEST_STREAM", ["test.>"]));
await stream.PurgeAsync(new StreamPurgeRequest { Keep = 0 });

Console.WriteLine("Creat publisher");
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

Console.WriteLine("Start publishing");
for (int i = 0; i < numberOfMessages; i++)
{
    await publisher.PublishAsync($"test.msg{i}", $"Test message {i}");
}

await sub;

stopwatch.Stop();

Console.WriteLine("Received all acks");
Console.WriteLine($"Took {stopwatch.Elapsed} at {numberOfMessages / stopwatch.Elapsed.TotalSeconds:N0} msgs/sec");
```
