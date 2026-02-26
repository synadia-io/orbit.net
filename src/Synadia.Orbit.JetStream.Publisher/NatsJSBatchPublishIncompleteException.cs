// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when batch publish is incomplete and was abandoned.
/// </summary>
public class NatsJSBatchPublishIncompleteException : NatsJSException
{
    /// <summary>
    /// Error code for incomplete batch publish.
    /// </summary>
    public const int ErrorCode = 10176;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishIncompleteException"/> class.
    /// </summary>
    public NatsJSBatchPublishIncompleteException()
        : base("Batch publish is incomplete and was abandoned")
    {
    }
}
