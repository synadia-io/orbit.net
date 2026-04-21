// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when a batch publish exceeds the server's batch size limit.
/// </summary>
public class NatsJSBatchPublishExceedsLimitException : NatsJSBatchPublishException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishExceedsLimitException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSBatchPublishExceedsLimitException(ApiError error)
        : base(error)
    {
    }
}
