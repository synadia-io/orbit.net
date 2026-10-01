// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Reports a terminal ack for a fast-ingest batch that arrived with no commit in flight.
/// </summary>
/// <remarks>
/// Surfaced via <see cref="NatsJSFastPublisherOpts.ErrorHandler"/>. Usually the server ended the
/// batch itself after a gap or a per-message error in "fail" mode; it can also be a commit ack
/// that landed after the local commit wait timed out. Either way the gap and per-message reports
/// are informational, so <see cref="Ack"/> is the only authoritative statement of how much of
/// the batch was stored.
/// </remarks>
public class NatsJSFastPublishBatchEndedException : NatsJSException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSFastPublishBatchEndedException"/> class.
    /// </summary>
    /// <param name="ack">The terminal ack sent by the server.</param>
    public NatsJSFastPublishBatchEndedException(NatsJSBatchAck ack)
        : base($"fast batch ended with {ack.BatchSize} message(s) stored")
    {
        Ack = ack;
    }

    /// <summary>
    /// Gets the terminal ack sent by the server, reporting what was stored.
    /// </summary>
    public NatsJSBatchAck Ack { get; }
}
