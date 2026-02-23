// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Represents an exception that occurs during operations in the JetStreamPublisher class.
/// </summary>
public class JetStreamPublisherException : Exception
{
    /// <summary>
    /// Represents a predefined exception indicating that no responders are available
    /// for a request in the JetStreamPublisher operations.
    /// </summary>
    /// <remarks>
    /// This exception is a static instance of <see cref="JetStreamPublisherException"/>
    /// created with an error code of 2001 and a description "No responders available".
    /// </remarks>
    public static readonly JetStreamPublisherException NoResponders = new(1001, "No responders available.");

    private JetStreamPublisherException(int code, string description)
    {
        Code = code;
        Description = description;
    }

    /// <summary>
    /// Gets the error code associated with the exception.
    /// </summary>
    /// <remarks>
    /// The code represents the specific error condition that occurred during the operation
    /// in the JetStreamPublisher class. Use this property to identify the type of error
    /// and to implement corresponding handling logic in your application.
    /// </remarks>
    public int Code { get; }

    /// <summary>
    /// Gets the description of the error associated with the exception.
    /// </summary>
    /// <remarks>
    /// This property provides a detailed explanation of the error that occurred during
    /// an operation in the JetStreamPublisher class. Use it to better understand the
    /// nature of the issue and to offer meaningful diagnostics in your application.
    /// </remarks>
    public string Description { get; }

    /// <summary>
    /// Throws a <see cref="JetStreamPublisherException"/> with the specified error code and description.
    /// </summary>
    /// <param name="code">The error code representing the specific failure in the JetStreamPublisher operation.</param>
    /// <param name="description">The description detailing the cause or context of the failure.</param>
    /// <exception cref="JetStreamPublisherException">Always throws this exception.</exception>
    public static void Throw(int code, string description)
    {
        throw new JetStreamPublisherException(code, description);
    }

    /// <summary>
    /// Creates a new instance of <see cref="JetStreamPublisherException"/> to indicate
    /// that the provided subject could not be parsed.
    /// </summary>
    /// <param name="subject">The subject string that failed to parse.</param>
    /// <returns>A <see cref="JetStreamPublisherException"/> with a description detailing the parsing failure.</returns>
    public static JetStreamPublisherException CannotParseSubject(string subject) => new(2001, $"Can't parse subject: {subject}");

    /// <summary>
    /// Creates a <see cref="JetStreamPublisherException"/> indicating that a status could not be found for the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier for which the status could not be found.</param>
    /// <returns>A <see cref="JetStreamPublisherException"/> with an appropriate error code and description.</returns>
    public static JetStreamPublisherException CannotFindId(long id) => new(2002, $"Can't find status for {id}");

    /// <summary>
    /// Creates a <see cref="JetStreamPublisherException"/> indicating that no data is available
    /// for the specified identifier in the JetStreamPublisher operation.
    /// </summary>
    /// <param name="id">The identifier for which no data is available.</param>
    /// <returns>A <see cref="JetStreamPublisherException"/> with an appropriate error code and message.</returns>
    public static JetStreamPublisherException NoData(long id) => new(2003, $"No data available for {id}");

    /// <summary>
    /// Creates a <see cref="JetStreamPublisherException"/> indicating that the message acknowledgment has timed out.
    /// </summary>
    /// <param name="id">The unique identifier of the message for which the acknowledgment was expected.</param>
    /// <param name="timeout">The duration that was waited before timing out.</param>
    /// <returns>An instance of <see cref="JetStreamPublisherException"/> with the appropriate error code and message.</returns>
    public static JetStreamPublisherException MessageTimeout(long id, TimeSpan timeout) => new(
        2004,
        $"Timed out waiting for acknowledgment. id:{id} timeout:{timeout.TotalMilliseconds:F0}ms");
}
