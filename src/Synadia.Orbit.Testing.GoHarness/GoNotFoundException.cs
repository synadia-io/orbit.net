// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.Testing.GoHarness;

/// <summary>
/// Thrown when the Go toolchain is not found on PATH.
/// </summary>
public class GoNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public GoNotFoundException(string message)
        : base(message)
    {
    }
}
