// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.KeyValueStore.Extensions.Codecs;

/// <summary>
/// A no-op codec that passes keys through unchanged.
/// </summary>
public sealed class NatsNoOpKeyCodec : INatsFilterableKeyCodec
{
    private NatsNoOpKeyCodec()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="NatsNoOpKeyCodec"/>.
    /// </summary>
    public static NatsNoOpKeyCodec Instance { get; } = new();

    /// <inheritdoc/>
    public string EncodeKey(string key) => key;

    /// <inheritdoc/>
    public string DecodeKey(string key) => key;

    /// <inheritdoc/>
    public string EncodeFilter(string filter) => filter;
}
