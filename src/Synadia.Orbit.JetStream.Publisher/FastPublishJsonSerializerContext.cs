// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable SA1600 // Elements should be documented (internal types)

using System.Text.Json.Serialization;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Source-generated JSON serialization context for fast-ingest batch publish response types.
/// </summary>
[JsonSerializable(typeof(FastPublishFlowAckResponse))]
[JsonSerializable(typeof(FastPublishGapResponse))]
[JsonSerializable(typeof(FastPublishErrResponse))]
internal partial class FastPublishJsonSerializerContext : JsonSerializerContext;

#pragma warning disable SA1402

internal record FastPublishFlowAckResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }

    [JsonPropertyName("msgs")]
    public ushort Messages { get; init; }
}

internal record FastPublishGapResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("last_seq")]
    public ulong LastSeq { get; init; }

    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }
}

internal record FastPublishErrResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }

    [JsonPropertyName("error")]
    public BatchPublishErrorResponse? Error { get; init; }
}
