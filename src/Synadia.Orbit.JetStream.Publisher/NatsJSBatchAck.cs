// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// The acknowledgment for a batch publish operation.
/// </summary>
public record NatsJSBatchAck
{
    /// <summary>
    /// Gets the stream name the message was published to.
    /// </summary>
    public string Stream { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stream sequence number of the message.
    /// </summary>
    public ulong Sequence { get; init; }

    /// <summary>
    /// Gets the domain the message was published to.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets the unique identifier for the batch.
    /// </summary>
    public string BatchId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the counter value if the stream has AllowMsgCounter enabled.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets the number of messages in the batch.
    /// </summary>
    public int BatchSize { get; init; }
}
