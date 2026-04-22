// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when the server returns an API error for a batch publish operation.
/// </summary>
public class NatsJSBatchPublishException : NatsJSApiException
{
    /// <summary>
    /// Error code indicating batch publish is not enabled on the stream.
    /// </summary>
    public const int ErrCodeNotEnabled = 10174;

    /// <summary>
    /// Error code indicating batch publish sequence is missing.
    /// </summary>
    public const int ErrCodeMissingSeq = 10175;

    /// <summary>
    /// Error code indicating batch publish is incomplete and was abandoned.
    /// </summary>
    public const int ErrCodeIncomplete = 10176;

    /// <summary>
    /// Error code indicating batch publish uses unsupported headers.
    /// </summary>
    public const int ErrCodeUnsupportedHeader = 10177;

    /// <summary>
    /// Error code indicating batch publish sequence exceeds server limit.
    /// </summary>
    public const int ErrCodeExceedsLimit = 10199;

    /// <summary>
    /// Error code indicating the server has too many in-flight atomic publish batches.
    /// </summary>
    public const int ErrCodeTooManyInflight = 10210;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSBatchPublishException(ApiError error)
        : base(error)
    {
    }
}
