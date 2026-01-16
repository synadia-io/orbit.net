// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.KeyValueStore.Extensions.Codecs;

/// <summary>
/// Defines the interface for encoding and decoding keys in a KV bucket.
/// </summary>
public interface INatsKeyCodec
{
    /// <summary>
    /// Encodes a key for storage.
    /// </summary>
    /// <param name="key">The key to encode.</param>
    /// <returns>The encoded key.</returns>
    string EncodeKey(string key);

    /// <summary>
    /// Decodes a key retrieved from storage.
    /// </summary>
    /// <param name="key">The encoded key to decode.</param>
    /// <returns>The decoded key.</returns>
    string DecodeKey(string key);
}

/// <summary>
/// An optional interface that key codecs can implement to support wildcard filtering operations.
/// If a key codec doesn't implement this interface, filter operations where the pattern contains
/// wildcards (* or &gt;) will throw <see cref="NatsKeyCodecException"/>.
/// </summary>
public interface INatsFilterableKeyCodec : INatsKeyCodec
{
    /// <summary>
    /// Encodes a pattern that may contain wildcards (* or &gt;).
    /// Unlike <see cref="INatsKeyCodec.EncodeKey"/>, this must preserve wildcards in the result.
    /// </summary>
    /// <param name="filter">The filter pattern to encode.</param>
    /// <returns>The encoded filter pattern with wildcards preserved.</returns>
    string EncodeFilter(string filter);
}
