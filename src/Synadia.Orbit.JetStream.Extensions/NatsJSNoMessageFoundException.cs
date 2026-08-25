// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Extensions;

/// <summary>
/// The exception that is thrown when a JetStream message is not found.
/// </summary>
public class NatsJSNoMessageFoundException : NatsJSException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSNoMessageFoundException"/> class.
    /// </summary>
    public NatsJSNoMessageFoundException()
        : base("Message not found")
    {
    }
}
