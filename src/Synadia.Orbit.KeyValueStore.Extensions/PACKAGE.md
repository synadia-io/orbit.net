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

Example: Custom Codec (ROT13 'encryption')

```csharp
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

await using var client = new NatsClient();
var kv = client.CreateKeyValueStoreContext();

var rawStore = await kv.CreateStoreAsync("secret-bucket");

// Use custom ROT13 codec for "encrypted" keys
var store = rawStore.WithKeyCodec(new Rot13KeyCodec());

// Store with readable keys - they get ROT13 encoded in storage
await store.PutAsync("secret.password", "hunter2");
await store.PutAsync("secret.api-key", "abc123");

// Keys are returned decoded
var entry = await store.GetEntryAsync<string>("secret.password");
Console.WriteLine($"Key: {entry.Key}, Value: {entry.Value}");
// Output: Key: secret.password, Value: hunter2

// But in raw storage, keys are ROT13 encoded
await foreach (string key in rawStore.GetKeysAsync())
{
    Console.WriteLine($"Raw Key: {key}");
}
// Output:
// Raw Key: frperg.cnffjbeq
// Raw Key: frperg.ncv-xrl
```

```csharp
/// <summary>
/// A custom codec that "encrypts" keys using ROT13 substitution cipher.
/// This is for demonstration purposes only - ROT13 is not secure encryption!
/// </summary>
public class Rot13KeyCodec : INatsFilterableKeyCodec
{
    public string EncodeKey(string key) => Rot13(key);

    public string DecodeKey(string key) => Rot13(key); // ROT13 is its own inverse

    public string EncodeFilter(string filter) => Rot13(filter);

    private static string Rot13(string input)
    {
        var result = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c >= 'a' && c <= 'z')
            {
                result[i] = (char)('a' + (c - 'a' + 13) % 26);
            }
            else if (c >= 'A' && c <= 'Z')
            {
                result[i] = (char)('A' + (c - 'A' + 13) % 26);
            }
            else
            {
                result[i] = c; // Non-letters pass through unchanged (including '.' and '*')
            }
        }

        return new string(result);
    }
}
```

## Available Codecs

| Codec | Description |
|-------|-------------|
| `NatsNoOpKeyCodec` | Pass-through, no encoding |
| `NatsBase64KeyCodec` | URL-safe Base64 encoding per token |
| `NatsPathKeyCodec` | Converts `/path/style` to `.subject.style` |
| `NatsKeyChainCodec` | Chains multiple codecs together |
