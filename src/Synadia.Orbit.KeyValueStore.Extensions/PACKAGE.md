# KeyValueStore Extensions

Utilities that extend NATS KeyValueStore client functionality.

## Key Codecs

Key codecs provide transparent key encoding/decoding for KV stores. This is useful when you need to store keys that contain characters not allowed in NATS subjects, or when you prefer a different key format.

### Base64 Key Encoding

Encodes each key token using URL-safe Base64. Useful for keys containing special characters like `/`, `@`, or spaces.

```csharp
// dotnet add package nats.net
// dotnet add package Synadia.Orbit.KeyValueStore.Extensions --prerelease
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

await using var client = new NatsClient();
var kv = client.CreateKeyValueStoreContext();

var rawStore = await kv.CreateStoreAsync("my-bucket");
var store = rawStore.WithBase64Keys();

// Keys with special characters work transparently
await store.PutAsync("user/123@example.com", "user data");
// Stored in KV as: "dXNlci8xMjNAZXhhbXBsZQ.Y29t"
//                   ^^^^^^^^^^^^^^^^^^^^^^ ^^^^
//                   "user/123@example"     "com"
// (dots are preserved as token separators, each token is Base64 encoded)

var entry = await store.GetEntryAsync<string>("user/123@example.com");
Console.WriteLine($"Key: {entry.Key}, Value: {entry.Value}");
// Output: Key: user/123@example.com, Value: user data
```

### Path-Style Keys

Translates familiar path-style keys (using `/`) to NATS subject notation (using `.`).

```csharp
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

await using var client = new NatsClient();
var kv = client.CreateKeyValueStoreContext();

var rawStore = await kv.CreateStoreAsync("config-bucket");
var store = rawStore.WithPathKeys();

// Use familiar path-style keys
await store.PutAsync("/config/database/connection-string", "Server=localhost;...");
// Stored in KV as: "_root_.config.database.connection-string"

await store.PutAsync("/config/database/timeout", "30");
// Stored in KV as: "_root_.config.database.timeout"

await store.PutAsync("/config/logging/level", "Information");
// Stored in KV as: "_root_.config.logging.level"

// Keys are returned in path format
await foreach (var key in store.GetKeysAsync())
{
    Console.WriteLine(key);
}
// Output:
// /config/database/connection-string
// /config/database/timeout
// /config/logging/level
```

### Chaining Codecs

Multiple codecs can be chained together for complex encoding scenarios.

```csharp
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

// Chain: Path codec first, then Base64
var chain = new NatsKeyChainCodec(NatsPathKeyCodec.Instance, NatsBase64KeyCodec.Instance);
var store = rawStore.WithKeyCodec(chain);
```

### Custom Codecs

Implement `INatsKeyCodec` or `INatsFilterableKeyCodec` to create custom encoding logic.

```csharp
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

public class MyCustomCodec : INatsFilterableKeyCodec
{
    public string EncodeKey(string key) => /* your encoding logic */;
    public string DecodeKey(string key) => /* your decoding logic */;
    public string EncodeFilter(string filter) => /* preserve wildcards */;
}

var store = rawStore.WithKeyCodec(new MyCustomCodec());
```

## Available Codecs

| Codec | Description |
|-------|-------------|
| `NatsNoOpKeyCodec` | Pass-through, no encoding |
| `NatsBase64KeyCodec` | URL-safe Base64 encoding per token |
| `NatsPathKeyCodec` | Converts `/path/style` to `.subject.style` |
| `NatsKeyChainCodec` | Chains multiple codecs together |
