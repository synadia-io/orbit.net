// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.KeyValueStore;

namespace Synadia.Orbit.KeyValueStore.Extensions;

/// <summary>
/// Extension methods for <see cref="INatsKVStore"/>.
/// </summary>
public static class NatsKVStoreExtensions
{
    /// <summary>
    /// Wraps the KV store with a key codec for transparent key encoding/decoding.
    /// </summary>
    /// <param name="store">The KV store to wrap.</param>
    /// <param name="keyCodec">The codec to use for key encoding/decoding.</param>
    /// <returns>A new <see cref="INatsKVStore"/> that applies the codec to all key operations.</returns>
    public static INatsKVStore WithKeyCodec(this INatsKVStore store, IKeyCodec keyCodec)
    {
        return new NatsKVCodecStore(store, keyCodec);
    }

    /// <summary>
    /// Wraps the KV store with Base64 key encoding.
    /// Each key token (separated by '.') is encoded separately using URL-safe Base64.
    /// </summary>
    /// <param name="store">The KV store to wrap.</param>
    /// <returns>A new <see cref="INatsKVStore"/> that Base64 encodes all keys.</returns>
    public static INatsKVStore WithBase64Keys(this INatsKVStore store)
    {
        return new NatsKVCodecStore(store, Base64KeyCodec.Instance);
    }

    /// <summary>
    /// Wraps the KV store with path-style key encoding.
    /// Keys using '/' separators are translated to NATS subject notation using '.'.
    /// </summary>
    /// <param name="store">The KV store to wrap.</param>
    /// <returns>A new <see cref="INatsKVStore"/> that translates path-style keys.</returns>
    /// <remarks>
    /// Example: "/users/123/profile" becomes "_root_.users.123.profile".
    /// </remarks>
    public static INatsKVStore WithPathKeys(this INatsKVStore store)
    {
        return new NatsKVCodecStore(store, PathKeyCodec.Instance);
    }
}
