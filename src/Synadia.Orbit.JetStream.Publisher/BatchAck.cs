// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// The acknowledgment for a batch publish operation.
/// </summary>
public record BatchAck
{
    /// <summary>
    /// Gets the stream name the message was published to.
    /// </summary>
    [JsonPropertyName("stream")]
    public string Stream { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stream sequence number of the message.
    /// </summary>
    [JsonPropertyName("seq")]
    public ulong Sequence { get; init; }

    /// <summary>
    /// Gets the domain the message was published to.
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    /// <summary>
    /// Gets the unique identifier for the batch.
    /// </summary>
    [JsonPropertyName("batch")]
    public string BatchId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the counter value if the stream has AllowMsgCounter enabled.
    /// </summary>
    [JsonPropertyName("val")]
    public string? Value { get; init; }

    /// <summary>
    /// Gets the number of messages in the batch.
    /// </summary>
    [JsonPropertyName("count")]
    public int BatchSize { get; init; }
}
