// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when the server returns an API error for a fast-ingest batch publish.
/// </summary>
public class NatsJSFastPublishException : NatsJSApiException
{
    /// <summary>
    /// Error code indicating fast batch publish is not enabled on the stream.
    /// </summary>
    public const int ErrCodeNotEnabled = 10203;

    /// <summary>
    /// Error code indicating an invalid reply subject pattern was used.
    /// </summary>
    public const int ErrCodeInvalidPattern = 10204;

    /// <summary>
    /// Error code indicating the batch ID is invalid (exceeds 64 characters).
    /// </summary>
    public const int ErrCodeInvalidId = 10205;

    /// <summary>
    /// Error code indicating the batch ID is unknown to the server.
    /// </summary>
    public const int ErrCodeUnknownId = 10206;

    /// <summary>
    /// Error code indicating the server has too many in-flight fast batch publishes.
    /// </summary>
    public const int ErrCodeTooManyInflight = 10211;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSFastPublishException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSFastPublishException(ApiError error)
        : base(error)
    {
    }
}
