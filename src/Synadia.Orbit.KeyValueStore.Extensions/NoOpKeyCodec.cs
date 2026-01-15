// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.KeyValueStore.Extensions;

/// <summary>
/// A no-op codec that passes keys through unchanged.
/// </summary>
public sealed class NoOpKeyCodec : IFilterableKeyCodec
{
    private NoOpKeyCodec()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="NoOpKeyCodec"/>.
    /// </summary>
    public static NoOpKeyCodec Instance { get; } = new();

    /// <inheritdoc/>
    public string EncodeKey(string key) => key;

    /// <inheritdoc/>
    public string DecodeKey(string key) => key;

    /// <inheritdoc/>
    public string EncodeFilter(string filter) => filter;
}
