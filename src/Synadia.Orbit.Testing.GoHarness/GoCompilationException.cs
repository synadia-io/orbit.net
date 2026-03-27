// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.Testing.GoHarness;

/// <summary>
/// Thrown when Go code fails to compile.
/// </summary>
public class GoCompilationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoCompilationException"/> class.
    /// </summary>
    /// <param name="message">The error message including compiler output.</param>
    public GoCompilationException(string message)
        : base(message)
    {
    }
}
