// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.NatsContext;

/// <summary>
/// Exception thrown when a NATS context operation fails.
/// </summary>
public class NatsContextException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsContextException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsContextException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsContextException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsContextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
