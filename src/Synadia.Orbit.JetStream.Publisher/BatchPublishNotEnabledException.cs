// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when batch publish is not enabled on the stream.
/// </summary>
public class BatchPublishNotEnabledException : NatsJSException
{
    /// <summary>
    /// Error code for batch publish not enabled.
    /// </summary>
    public const int ErrorCode = 10174;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchPublishNotEnabledException"/> class.
    /// </summary>
    public BatchPublishNotEnabledException()
        : base("Batch publish not enabled on stream")
    {
    }
}
