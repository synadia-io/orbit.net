// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when a batch publish is incomplete and was abandoned by the server.
/// </summary>
public class NatsJSBatchPublishIncompleteException : NatsJSBatchPublishException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishIncompleteException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSBatchPublishIncompleteException(ApiError error)
        : base(error)
    {
    }
}
