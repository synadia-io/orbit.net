// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable SA1600 // Elements should be documented (internal types)
#pragma warning disable SA1402 // File may only contain a single type

using System.Text.Json.Serialization;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Source-generated JSON serialization context for fast-ingest batch publish response types.
/// </summary>
[JsonSerializable(typeof(FastPublishFlowAckResponse))]
[JsonSerializable(typeof(FastPublishGapResponse))]
[JsonSerializable(typeof(FastPublishErrResponse))]
internal partial class FastPublishJsonSerializerContext : JsonSerializerContext;

internal record FastPublishFlowAckResponse
{
    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }

    [JsonPropertyName("msgs")]
    public ushort Messages { get; init; }
}

internal record FastPublishGapResponse
{
    [JsonPropertyName("last_seq")]
    public ulong LastSeq { get; init; }

    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }
}

internal record FastPublishErrResponse
{
    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }

    [JsonPropertyName("error")]
    public BatchPublishErrorResponse? Error { get; init; }
}
