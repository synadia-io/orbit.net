// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.KeyValueStore.Extensions;

/// <summary>
/// Exception thrown when a key codec operation fails.
/// </summary>
public class KeyCodecException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyCodecException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public KeyCodecException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyCodecException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public KeyCodecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
