// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when batch publish sequence is missing.
/// </summary>
public class NatsJSBatchPublishMissingSeqException : NatsJSException
{
    /// <summary>
    /// Error code for missing batch sequence.
    /// </summary>
    public const int ErrorCode = 10175;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishMissingSeqException"/> class.
    /// </summary>
    public NatsJSBatchPublishMissingSeqException()
        : base("Batch publish sequence is missing")
    {
    }
}
