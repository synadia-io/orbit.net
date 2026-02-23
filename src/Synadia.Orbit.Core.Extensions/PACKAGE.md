# Synadia.Orbit.Core.Extensions

Extension methods for NATS.Client.Core providing additional functionality.

## Features

### RequestMany with Custom Sentinel

Extends `RequestManyAsync` with support for custom sentinel functions, allowing you to define when to stop receiving messages based on message content.

```csharp
// Stop when a message header indicates completion
await foreach (var msg in nats.RequestManyWithSentinelAsync<Request, Response>(
    subject: "service.request",
    data: new Request { Id = 1 },
    sentinel: msg => msg.Headers?["X-Done"].ToString() == "true",
    cancellationToken: ct))
{
    Console.WriteLine($"Got response: {msg.Data}");
}

// Stop when response data indicates completion
await foreach (var msg in nats.RequestManyWithSentinelAsync<int, Response>(
    subject: "service.request",
    data: 42,
    sentinel: msg => msg.Data?.IsLast == true,
    cancellationToken: ct))
{
    Console.WriteLine($"Got response: {msg.Data}");
}
```

## Documentation

For more information, see the [orbit.net documentation](https://github.com/synadia-io/orbit.net).
