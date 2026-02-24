// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.Counters.Models;

/// <summary>
/// Represents the JSON payload for a counter value response.
/// </summary>
internal record CounterValuePayload
{
    /// <summary>
    /// Gets the counter value as a string.
    /// </summary>
    [JsonPropertyName("val")]
    public string? Val { get; init; }
}
