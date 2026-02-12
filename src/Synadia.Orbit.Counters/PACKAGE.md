# Synadia.Orbit.Counters

Distributed counters backed by JetStream streams. Each subject in a counter-enabled stream
is an independent counter supporting atomic increment/decrement operations with arbitrary
precision values.

## Usage

```csharp
await using var client = new NatsClient();
var js = client.CreateJetStreamContext();

// Create a counter-enabled stream
await js.CreateStreamAsync(new StreamConfig("COUNTERS", ["counter.>"])
{
    AllowMsgCounter = true,
    AllowDirect = true,
});

// Get a counter handle
var counter = await js.GetCounterAsync("COUNTERS");

// Increment
var value = await counter.AddAsync("counter.hits", 1);

// Load current value
var current = await counter.LoadAsync("counter.hits");

// Get full entry with source tracking
var entry = await counter.GetAsync("counter.hits");

// Get multiple counters at once
await foreach (var e in counter.GetManyAsync(new[] { "counter.>" }))
{
    Console.WriteLine($"{e.Subject}: {e.Value}");
}
```
