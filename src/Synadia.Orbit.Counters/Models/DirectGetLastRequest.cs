// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.Counters.Models;

/// <summary>
/// Represents a request to get the last message for a subject via direct get.
/// </summary>
internal record DirectGetLastRequest
{
    /// <summary>
    /// Gets the subject to retrieve the last message for.
    /// </summary>
    [JsonPropertyName("last_by_subj")]
    public string? LastBySubj { get; init; }
}
