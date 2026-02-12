// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.Counters;

/// <summary>
/// Represents an exception that occurs during counter operations.
/// </summary>
public class NatsCounterException : Exception
{
    /// <summary>
    /// The stream is not configured for counters (AllowMsgCounter must be true).
    /// </summary>
    public static readonly NatsCounterException CounterNotEnabled = new(1001, "Stream is not configured for counters (AllowMsgCounter must be true).");

    /// <summary>
    /// The stream must be configured for direct access (AllowDirect must be true).
    /// </summary>
    public static readonly NatsCounterException DirectAccessRequired = new(1002, "Stream must be configured for direct access (AllowDirect must be true).");

    /// <summary>
    /// The counter stream was not found.
    /// </summary>
    public static readonly NatsCounterException CounterNotFound = new(1003, "Counter not found.");

    private NatsCounterException(int code, string description)
        : base(description)
    {
        Code = code;
        Description = description;
    }

    /// <summary>
    /// Gets the error code associated with the exception.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// Gets the description of the error.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Creates an exception indicating that a counter has not been initialized for the given subject.
    /// </summary>
    /// <param name="subject">The subject that was not found.</param>
    /// <returns>A new <see cref="NatsCounterException"/> instance.</returns>
    public static NatsCounterException NoCounterForSubject(string subject) => new(2001, $"Counter not initialized for subject: {subject}");

    /// <summary>
    /// Creates an exception indicating that a counter value is invalid.
    /// </summary>
    /// <param name="value">The invalid value string.</param>
    /// <returns>A new <see cref="NatsCounterException"/> instance.</returns>
    public static NatsCounterException InvalidCounterValue(string value) => new(2002, $"Invalid counter value: {value}");

    /// <summary>
    /// Creates an exception indicating that the counter increment response is missing a value.
    /// </summary>
    /// <returns>A new <see cref="NatsCounterException"/> instance.</returns>
    public static NatsCounterException MissingCounterValue() => new(2003, "Counter increment response missing value.");
}
