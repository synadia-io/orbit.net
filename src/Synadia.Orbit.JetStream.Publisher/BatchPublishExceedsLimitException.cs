// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when batch publish sequence exceeds server limit.
/// </summary>
public class BatchPublishExceedsLimitException : NatsJSException
{
    /// <summary>
    /// Error code for exceeding batch limit.
    /// </summary>
    public const int ErrorCode = 10199;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchPublishExceedsLimitException"/> class.
    /// </summary>
    public BatchPublishExceedsLimitException()
        : base("Batch publish sequence exceeds server limit (default 1000)")
    {
    }
}
