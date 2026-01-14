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

/// <summary>
/// Exception thrown when batch publish is incomplete and was abandoned.
/// </summary>
public class BatchPublishIncompleteException : NatsJSException
{
    /// <summary>
    /// Error code for incomplete batch publish.
    /// </summary>
    public const int ErrorCode = 10176;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchPublishIncompleteException"/> class.
    /// </summary>
    public BatchPublishIncompleteException()
        : base("Batch publish is incomplete and was abandoned")
    {
    }
}

/// <summary>
/// Exception thrown when batch publish sequence is missing.
/// </summary>
public class BatchPublishMissingSeqException : NatsJSException
{
    /// <summary>
    /// Error code for missing batch sequence.
    /// </summary>
    public const int ErrorCode = 10175;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchPublishMissingSeqException"/> class.
    /// </summary>
    public BatchPublishMissingSeqException()
        : base("Batch publish sequence is missing")
    {
    }
}

/// <summary>
/// Exception thrown when batch publish uses unsupported headers.
/// </summary>
public class BatchPublishUnsupportedHeaderException : NatsJSException
{
    /// <summary>
    /// Error code for unsupported headers.
    /// </summary>
    public const int ErrorCode = 10177;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchPublishUnsupportedHeaderException"/> class.
    /// </summary>
    public BatchPublishUnsupportedHeaderException()
        : base("Batch publish unsupported header used (Nats-Expected-Last-Msg-Id or Nats-Msg-Id)")
    {
    }
}

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

/// <summary>
/// Exception thrown when attempting to use a batch that has been closed.
/// </summary>
public class BatchClosedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BatchClosedException"/> class.
    /// </summary>
    public BatchClosedException()
        : base("Batch publisher closed")
    {
    }
}

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
