// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// The acknowledgment returned by <see cref="INatsJSFastPublisher.AddAsync{T}"/> and
/// <see cref="INatsJSFastPublisher.AddMsgAsync{T}"/>.
/// </summary>
public record NatsJSFastPubAck
{
    /// <summary>
    /// Gets the sequence of this message within the batch.
    /// </summary>
    public long BatchSequence { get; init; }

    /// <summary>
    /// Gets the highest batch sequence that the server has acknowledged so far.
    /// With <see cref="NatsJSFastPublisherOpts.ContinueOnGap"/> = false, all messages up to and
    /// including this sequence were persisted. With <see cref="NatsJSFastPublisherOpts.ContinueOnGap"/>
    /// = true, persistence may be incomplete (gaps reported via the error handler).
    /// </summary>
    public long AckSequence { get; init; }
}
