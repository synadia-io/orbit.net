// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.KeyValueStore;
using NATS.Net;
using Synadia.Orbit.KeyValueStore.Extensions.Codecs;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.KeyValueStore.Extensions.Test.Codecs;

[Collection("nats-server")]
public class CodecTests
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public CodecTests(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public void NatsNoOpKeyCodec_passes_through_unchanged()
    {
        var codec = NatsNoOpKeyCodec.Instance;

        Assert.Equal("test.key", codec.EncodeKey("test.key"));
        Assert.Equal("test.key", codec.DecodeKey("test.key"));
        Assert.Equal("test.*", codec.EncodeFilter("test.*"));
        Assert.Equal("test.>", codec.EncodeFilter("test.>"));
    }

    [Fact]
    public void NatsBase64KeyCodec_encodes_each_token_separately()
    {
        var codec = NatsBase64KeyCodec.Instance;

        // "hello" in base64url is "aGVsbG8"
        // "world" in base64url is "d29ybGQ"
        var encoded = codec.EncodeKey("hello.world");
        Assert.Equal("aGVsbG8.d29ybGQ", encoded);

        var decoded = codec.DecodeKey(encoded);
        Assert.Equal("hello.world", decoded);
    }

    [Fact]
    public void NatsBase64KeyCodec_handles_special_characters()
    {
        var codec = NatsBase64KeyCodec.Instance;

        // Test with characters that would be invalid in NATS subjects
        var key = "user/123.profile@test";
        var encoded = codec.EncodeKey(key);
        var decoded = codec.DecodeKey(encoded);

        Assert.Equal(key, decoded);
        _output.WriteLine($"Original: {key}");
        _output.WriteLine($"Encoded: {encoded}");
    }

    [Fact]
    public void NatsBase64KeyCodec_preserves_dots_as_token_separators()
    {
        var codec = NatsBase64KeyCodec.Instance;

        // "user/123@example.com" has a dot, so it splits into two tokens:
        // "user/123@example" and "com"
        var key = "user/123@example.com";
        var encoded = codec.EncodeKey(key);
        var decoded = codec.DecodeKey(encoded);

        Assert.Equal(key, decoded);
        Assert.Equal("dXNlci8xMjNAZXhhbXBsZQ.Y29t", encoded);

        // "user/123@example" -> "dXNlci8xMjNAZXhhbXBsZQ"
        // "com" -> "Y29t"
        _output.WriteLine($"Original: {key}");
        _output.WriteLine($"Encoded: {encoded}");
    }

    [Fact]
    public void NatsBase64KeyCodec_encodes_key_without_dots_as_single_token()
    {
        var codec = NatsBase64KeyCodec.Instance;

        // Key without any dots is encoded as a single base64 token
        var key = "user/123@test";
        var encoded = codec.EncodeKey(key);
        var decoded = codec.DecodeKey(encoded);

        Assert.Equal(key, decoded);
        Assert.DoesNotContain(".", encoded); // No dots since input has no dots
        _output.WriteLine($"Original: {key}");
        _output.WriteLine($"Encoded: {encoded}");
    }

    [Fact]
    public void NatsBase64KeyCodec_preserves_wildcards_in_filter()
    {
        var codec = NatsBase64KeyCodec.Instance;

        var filter = "users.*.profile";
        var encoded = codec.EncodeFilter(filter);

        // "users" and "profile" should be encoded, but "*" should be preserved
        Assert.Contains("*", encoded);
        Assert.Equal("dXNlcnM.*.cHJvZmlsZQ", encoded);
    }

    [Fact]
    public void NatsBase64KeyCodec_preserves_gt_wildcard_in_filter()
    {
        var codec = NatsBase64KeyCodec.Instance;

        var filter = "users.>";
        var encoded = codec.EncodeFilter(filter);

        Assert.EndsWith(".>", encoded);
        Assert.Equal("dXNlcnM.>", encoded);
    }

    [Fact]
    public void NatsPathKeyCodec_converts_slashes_to_dots()
    {
        var codec = NatsPathKeyCodec.Instance;

        // Without leading slash
        Assert.Equal("users.123.profile", codec.EncodeKey("users/123/profile"));
        Assert.Equal("users/123/profile", codec.DecodeKey("users.123.profile"));
    }

    [Fact]
    public void NatsPathKeyCodec_handles_leading_slash()
    {
        var codec = NatsPathKeyCodec.Instance;

        // With leading slash - should use _root_ prefix
        var encoded = codec.EncodeKey("/users/123/profile");
        Assert.Equal("_root_.users.123.profile", encoded);

        var decoded = codec.DecodeKey(encoded);
        Assert.Equal("/users/123/profile", decoded);
    }

    [Fact]
    public void NatsPathKeyCodec_handles_root_only()
    {
        var codec = NatsPathKeyCodec.Instance;

        Assert.Equal("_root_", codec.EncodeKey("/"));
        Assert.Equal("/", codec.DecodeKey("_root_"));
    }

    [Fact]
    public void NatsPathKeyCodec_trims_trailing_slash()
    {
        var codec = NatsPathKeyCodec.Instance;

        Assert.Equal("users.123", codec.EncodeKey("users/123/"));
    }

    [Fact]
    public void NatsPathKeyCodec_encodes_config_style_paths()
    {
        var codec = NatsPathKeyCodec.Instance;

        // Examples from PACKAGE.md documentation
        Assert.Equal("_root_.config.database.connection-string", codec.EncodeKey("/config/database/connection-string"));
        Assert.Equal("_root_.config.database.timeout", codec.EncodeKey("/config/database/timeout"));
        Assert.Equal("_root_.config.logging.level", codec.EncodeKey("/config/logging/level"));

        // Verify roundtrip
        Assert.Equal("/config/database/connection-string", codec.DecodeKey("_root_.config.database.connection-string"));
        Assert.Equal("/config/database/timeout", codec.DecodeKey("_root_.config.database.timeout"));
        Assert.Equal("/config/logging/level", codec.DecodeKey("_root_.config.logging.level"));
    }

    [Fact]
    public void NatsKeyChainCodec_applies_codecs_in_order()
    {
        // Chain: Path -> Base64
        // Input: "/users/123" -> "_root_.users.123" -> "X3Jvb3Rf.dXNlcnM.MTIz"
        var chain = new NatsKeyChainCodec(NatsPathKeyCodec.Instance, NatsBase64KeyCodec.Instance);

        var encoded = chain.EncodeKey("/users/123");
        _output.WriteLine($"Encoded: {encoded}");

        // Verify it roundtrips
        var decoded = chain.DecodeKey(encoded);
        Assert.Equal("/users/123", decoded);
    }

    [Fact]
    public void NatsKeyChainCodec_requires_at_least_one_codec()
    {
        Assert.Throws<ArgumentException>(() => new NatsKeyChainCodec());
        Assert.Throws<ArgumentException>(() => new NatsKeyChainCodec(Array.Empty<INatsKeyCodec>()));
    }

    [Fact]
    public void NatsKeyChainCodec_filter_requires_all_filterable()
    {
        // NoOp and Base64 are filterable, so this should work
        var chain = new NatsKeyChainCodec(NatsNoOpKeyCodec.Instance, NatsBase64KeyCodec.Instance);
        var result = chain.EncodeFilter("test.*");
        Assert.Contains("*", result);
    }

    [Fact]
    public async Task NatsKVCodecStore_put_and_get_with_base64()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_codec_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(bucketName, ct);
        var store = rawStore.WithBase64Keys();

        // Put with a key that has special characters
        var key = "user/123";
        await store.PutAsync(key, "test-value", cancellationToken: ct);

        // Get should return the original key
        var entry = await store.GetEntryAsync<string>(key, cancellationToken: ct);
        Assert.Equal(key, entry.Key);
        Assert.Equal("test-value", entry.Value);

        _output.WriteLine($"Key: {entry.Key}, Value: {entry.Value}");
    }

    [Fact]
    public async Task NatsKVCodecStore_put_and_get_with_path()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_path_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(bucketName, ct);
        var store = rawStore.WithPathKeys();

        // Put with path-style key
        var key = "/users/123/profile";
        await store.PutAsync(key, "profile-data", cancellationToken: ct);

        // Get should return the original path-style key
        var entry = await store.GetEntryAsync<string>(key, cancellationToken: ct);
        Assert.Equal(key, entry.Key);
        Assert.Equal("profile-data", entry.Value);

        _output.WriteLine($"Key: {entry.Key}, Value: {entry.Value}");
    }

    [Fact]
    public async Task NatsKVCodecStore_get_keys_decodes_keys()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_keys_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(bucketName, ct);
        var store = rawStore.WithPathKeys();

        // Put multiple path-style keys
        await store.PutAsync("/users/1", "user1", cancellationToken: ct);
        await store.PutAsync("/users/2", "user2", cancellationToken: ct);
        await store.PutAsync("/users/3", "user3", cancellationToken: ct);

        // Get keys should return decoded path-style keys
        var keys = new List<string>();
        await foreach (var key in store.GetKeysAsync(cancellationToken: ct))
        {
            keys.Add(key);
            _output.WriteLine($"Key: {key}");
        }

        Assert.Contains("/users/1", keys);
        Assert.Contains("/users/2", keys);
        Assert.Contains("/users/3", keys);
    }

    [Fact]
    public async Task NatsKVCodecStore_watch_decodes_keys()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_watch_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(bucketName, ct);
        var store = rawStore.WithPathKeys();

        // Put a value first
        await store.PutAsync("/config/setting1", "value1", cancellationToken: ct);

        // Watch should return decoded keys
        var entries = new List<NatsKVEntry<string>>();
        await foreach (var entry in store.WatchAsync<string>(cancellationToken: ct))
        {
            entries.Add(entry);
            _output.WriteLine($"Watch entry: Key={entry.Key}, Value={entry.Value}");

            // Stop after getting our entry (watch includes initial values then waits)
            if (entries.Count >= 1)
            {
                break;
            }
        }

        Assert.Single(entries);
        Assert.Equal("/config/setting1", entries[0].Key);
    }

    [Fact]
    public async Task NatsKVCodecStore_history_decodes_keys()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_history_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(new NatsKVConfig(bucketName) { History = 5 }, ct);
        var store = rawStore.WithPathKeys();

        var key = "/data/item";

        // Put multiple revisions
        await store.PutAsync(key, "v1", cancellationToken: ct);
        await store.PutAsync(key, "v2", cancellationToken: ct);
        await store.PutAsync(key, "v3", cancellationToken: ct);

        // History should return decoded keys
        var history = new List<NatsKVEntry<string>>();
        await foreach (var entry in store.HistoryAsync<string>(key, cancellationToken: ct))
        {
            history.Add(entry);
            _output.WriteLine($"History: Key={entry.Key}, Value={entry.Value}, Revision={entry.Revision}");
        }

        Assert.Equal(3, history.Count);
        Assert.All(history, e => Assert.Equal(key, e.Key));
    }

    [Fact]
    public async Task NatsKVCodecStore_delete_with_encoded_key()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_delete_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(bucketName, ct);
        var store = rawStore.WithBase64Keys();

        var key = "test/key";
        await store.PutAsync(key, "value", cancellationToken: ct);

        // Delete using the original key
        await store.DeleteAsync(key, cancellationToken: ct);

        // Verify deletion
        var result = await store.TryGetEntryAsync<string>(key, cancellationToken: ct);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task NatsKVCodecStore_create_and_update()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var kv = connection.CreateKeyValueStoreContext();
        string prefix = _server.GetNextId();
        string bucketName = $"{prefix}_create_update_test";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var rawStore = await kv.CreateStoreAsync(bucketName, ct);
        var store = rawStore.WithPathKeys();

        var key = "/items/new";

        // Create
        var revision = await store.CreateAsync(key, "initial", cancellationToken: ct);
        Assert.True(revision > 0);

        // Update with revision
        var newRevision = await store.UpdateAsync(key, "updated", revision, cancellationToken: ct);
        Assert.True(newRevision > revision);

        // Verify
        var entry = await store.GetEntryAsync<string>(key, cancellationToken: ct);
        Assert.Equal("updated", entry.Value);
        Assert.Equal(key, entry.Key);
    }
}
