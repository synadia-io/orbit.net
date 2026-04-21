// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Exception thrown when a batch publish message is missing a sequence.
/// </summary>
public class NatsJSBatchPublishMissingSeqException : NatsJSBatchPublishException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublishMissingSeqException"/> class.
    /// </summary>
    /// <param name="error">The API error returned by the server.</param>
    public NatsJSBatchPublishMissingSeqException(ApiError error)
        : base(error)
    {
    }
}
