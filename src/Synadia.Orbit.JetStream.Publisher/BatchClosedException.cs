// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

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
