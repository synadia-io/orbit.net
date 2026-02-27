// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when batch publish uses unsupported headers.
/// </summary>
public class NatsJSBatchPublishUnsupportedHeaderException : NatsJSException
{
    /// <summary>
    /// Error code for unsupported headers.
    /// </summary>
    public const int ErrorCode = 10177;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishUnsupportedHeaderException"/> class.
    /// </summary>
    public NatsJSBatchPublishUnsupportedHeaderException()
        : base("Batch publish unsupported header used (Nats-Expected-Last-Msg-Id or Nats-Msg-Id)")
    {
    }
}
