// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace Synadia.Orbit.Counters;

/// <summary>
/// Represents a counter's current state with full source history.
/// </summary>
public sealed record CounterEntry
{
    /// <summary>
    /// Gets the counter's subject name.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Gets the current counter value.
    /// </summary>
    public required BigInteger Value { get; init; }

    /// <summary>
    /// Gets the source contributions map, where the outer key is the source stream name
    /// and the inner dictionary maps subjects to their contributed values.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, BigInteger>>? Sources { get; init; }

    /// <summary>
    /// Gets the most recent increment value for this entry.
    /// </summary>
    public BigInteger? Increment { get; init; }
}
