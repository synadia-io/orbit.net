// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when batch publish is not enabled on the stream.
/// </summary>
public class NatsJSBatchPublishNotEnabledException : NatsJSBatchPublishException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishNotEnabledException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSBatchPublishNotEnabledException(ApiError error)
        : base(error)
    {
    }
}
