// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.Counters.Models;

/// <summary>
/// Represents a request to get the last messages for multiple subjects via direct get.
/// </summary>
internal record DirectGetMultiRequest
{
    /// <summary>
    /// Gets the subjects to retrieve messages for.
    /// </summary>
    [JsonPropertyName("multi_last")]
    public string[]? MultiLast { get; init; }
}
