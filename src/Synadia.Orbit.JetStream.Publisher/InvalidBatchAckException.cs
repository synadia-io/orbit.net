// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when JetStream ack from batch publish is invalid.
/// </summary>
public class InvalidBatchAckException : NatsJSException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidBatchAckException"/> class.
    /// </summary>
    public InvalidBatchAckException()
        : base("Invalid JetStream batch publish response")
    {
    }
}
