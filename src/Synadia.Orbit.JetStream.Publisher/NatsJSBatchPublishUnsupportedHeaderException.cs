// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when a batch publish message uses an unsupported header.
/// </summary>
public class NatsJSBatchPublishUnsupportedHeaderException : NatsJSBatchPublishException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishUnsupportedHeaderException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSBatchPublishUnsupportedHeaderException(ApiError error)
        : base(error)
    {
    }
}
