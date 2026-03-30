// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable SA1600 // Elements should be documented (internal types)

using System.Text.Json.Serialization;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Source-generated JSON serialization context for batch publish response types.
/// </summary>
[JsonSerializable(typeof(BatchPublishApiResponse))]
[JsonSerializable(typeof(BatchPublishAckResponse))]
internal partial class BatchPublishJsonSerializerContext : JsonSerializerContext;

#pragma warning disable SA1402

internal record BatchPublishApiResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("error")]
    public BatchPublishErrorResponse? Error { get; init; }
}

internal record BatchPublishAckResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("error")]
    public BatchPublishErrorResponse? Error { get; init; }

    [JsonPropertyName("stream")]
    public string? Stream { get; init; }

    [JsonPropertyName("seq")]
    public ulong Seq { get; init; }

    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    [JsonPropertyName("val")]
    public string? Value { get; init; }

    [JsonPropertyName("batch")]
    public string? BatchId { get; init; }

    [JsonPropertyName("count")]
    public int BatchSize { get; init; }
}

internal record BatchPublishErrorResponse
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("err_code")]
    public int ErrCode { get; init; }
}
