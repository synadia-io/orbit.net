// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

// dotnet add package nats.net
// dotnet add package Synadia.Orbit.KeyValueStore.Extensions --prerelease
using NATS.Net;
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

namespace DocsExamples;

public class ExampleKV
{
    public static async Task Run()
    {
        string hr = new('-', 50);

        Console.WriteLine(hr);
        Console.WriteLine("Example: Using Base64 Key Encoding in NATS Key-Value Store");
        {
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

            await foreach (string key in rawStore.GetKeysAsync())
            {
                Console.WriteLine($"Raw Key: {key}");
                // Output: Raw Key: dXNlci8xMjNAZXhhbXBsZQ.Y29t
            }
        }

        Console.WriteLine(hr);
        Console.WriteLine("Example: Using Path-Style Keys in NATS Key-Value Store");
        {
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
            await foreach (string key in store.GetKeysAsync())
            {
                Console.WriteLine(key);
            }
            // Output:
            // /config/database/connection-string
            // /config/database/timeout
            // /config/logging/level

            await foreach (string key in rawStore.GetKeysAsync())
            {
                Console.WriteLine($"Raw Key: {key}");
            }
            // Output:
            // Raw Key: _root_.config.database.connection-string
            // Raw Key: _root_.config.database.timeout
            // Raw Key: _root_.config.logging.level
        }

        Console.WriteLine(hr);
        Console.WriteLine("Example: Combining Path and Base64 Key Codecs in NATS Key-Value Store");
        {
            await using var client = new NatsClient();
            var kv = client.CreateKeyValueStoreContext();

            var rawStore = await kv.CreateStoreAsync("chain-bucket");

            // Chain: Path codec first, then Base64
            var chain = new NatsKeyChainCodec(NatsPathKeyCodec.Instance, NatsBase64KeyCodec.Instance);
            var store = rawStore.WithKeyCodec(chain);

            // Use path-style key with special characters
            await store.PutAsync("/@dmin/user+1/profile", "admin profile data");
            // Stored in KV as: "_root_.QGRtaW4.YXVzZXIrMQ.cHJvZmlsZQ"
            //                   ^^^^^^^^^^^^^ ^^^^^^^^^^^ ^^^^^^^^^^^^^
            //                   "/@dmin"      "user+1"     "profile"
            // (each token is Base64 encoded after path translation)

            var entry = await store.GetEntryAsync<string>("/@dmin/user+1/profile");
            Console.WriteLine($"Key: {entry.Key}, Value: {entry.Value}");
            // Output: Key: /@dmin/user+1/profile, Value: admin profile data

            await foreach (string key in rawStore.GetKeysAsync())
            {
                Console.WriteLine($"Raw Key: {key}");
                // Output: Raw Key: X3Jvb3Rf.QGRtaW4.dXNlcisx.cHJvZmlsZQ
            }
        }

        Console.WriteLine(hr);
        Console.WriteLine("Example: Custom Codec (ROT13 'encryption')");
        {
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
        }
    }
}

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
