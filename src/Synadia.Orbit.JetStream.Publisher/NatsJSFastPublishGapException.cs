// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Reports a gap detected by the server during fast-ingest batch publish.
/// </summary>
/// <remarks>
/// Surfaced via <see cref="NatsJSFastPublisherOpts.ErrorHandler"/> when
/// <see cref="NatsJSFastPublisherOpts.ContinueOnGap"/> is enabled.
/// </remarks>
public class NatsJSFastPublishGapException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSFastPublishGapException"/> class.
    /// </summary>
    /// <param name="expectedLastSequence">The sequence the server expected to receive next.</param>
    /// <param name="currentSequence">The sequence that triggered the gap.</param>
    public NatsJSFastPublishGapException(long expectedLastSequence, long currentSequence)
        : base($"fast batch gap detected: expected last sequence {expectedLastSequence}, current sequence {currentSequence}")
    {
        ExpectedLastSequence = expectedLastSequence;
        CurrentSequence = currentSequence;
    }

    /// <summary>
    /// Gets the sequence the server expected to receive next.
    /// </summary>
    public long ExpectedLastSequence { get; }

    /// <summary>
    /// Gets the sequence that triggered the gap.
    /// </summary>
    public long CurrentSequence { get; }
}
