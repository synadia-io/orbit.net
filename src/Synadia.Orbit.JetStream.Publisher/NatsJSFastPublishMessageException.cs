// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Reports a per-message error returned by the server during fast-ingest batch publish.
/// </summary>
/// <remarks>
/// Surfaced via <see cref="NatsJSFastPublisherOpts.ErrorHandler"/>. Note that fast-ingest
/// does not guarantee whether messages up to this sequence were persisted.
/// </remarks>
public class NatsJSFastPublishMessageException : NatsJSApiException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSFastPublishMessageException"/> class.
    /// </summary>
    /// <param name="sequence">The sequence at which the error occurred.</param>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSFastPublishMessageException(long sequence, ApiError error)
        : base(error)
    {
        Sequence = sequence;
    }

    /// <summary>
    /// Gets the sequence at which the error occurred.
    /// </summary>
    public long Sequence { get; }
}
