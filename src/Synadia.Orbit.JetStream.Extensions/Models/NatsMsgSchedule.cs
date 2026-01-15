// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Extensions.Models;

/// <summary>
/// Represents a scheduled message configuration for JetStream message scheduling.
/// </summary>
/// <remarks>
/// This record is used to specify parameters for scheduling a message to be delivered
/// at a future time to a target subject within the same stream.
/// </remarks>
public record NatsMsgSchedule
{
    private const string NatsScheduleHeader = "Nats-Schedule";
    private const string NatsScheduleTargetHeader = "Nats-Schedule-Target";
    private const string NatsScheduleTtlHeader = "Nats-Schedule-TTL";

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMsgSchedule"/> class.
    /// </summary>
    /// <param name="scheduleAt">The scheduled delivery time. Will be converted to UTC.</param>
    /// <param name="target">The target subject for message delivery.</param>
    /// <exception cref="ArgumentException">Thrown when target is null or whitespace.</exception>
    public NatsMsgSchedule(DateTimeOffset scheduleAt, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target subject cannot be null or whitespace.", nameof(target));
        }

        ScheduleAt = scheduleAt.ToUniversalTime();
        Target = target;
    }

    /// <summary>
    /// Gets the scheduled delivery time in UTC.
    /// </summary>
    public DateTimeOffset ScheduleAt { get; }

    /// <summary>
    /// Gets the target subject where the message will be delivered when the schedule fires.
    /// </summary>
    /// <remarks>
    /// The target subject must be within the stream's subject filter and must differ from
    /// the subject the scheduled message is published to.
    /// </remarks>
    public string Target { get; }

    /// <summary>
    /// Gets the optional TTL (Time To Live) in seconds for the message.
    /// </summary>
    /// <remarks>
    /// This is the TTL that gets placed on the resulting message, not on the schedule itself.
    /// </remarks>
    public TimeSpan? Ttl { get; init; }

    /// <summary>
    /// Converts this schedule configuration to NATS headers.
    /// </summary>
    /// <param name="existingHeaders">Optional existing headers to merge with. If null, new headers are created.</param>
    /// <returns>A <see cref="NatsHeaders"/> instance containing the scheduling headers.</returns>
    public NatsHeaders ToHeaders(NatsHeaders? existingHeaders = null)
    {
        var headers = existingHeaders ?? new NatsHeaders();

        // Format: @at 2025-11-25T10:00:00Z
        headers[NatsScheduleHeader] = $"@at {ScheduleAt.UtcDateTime:yyyy-MM-ddTHH:mm:ss}Z";
        headers[NatsScheduleTargetHeader] = Target;

        if (Ttl is { } ttl)
        {
            headers[NatsScheduleTtlHeader] = $"{(long)ttl.TotalSeconds}s";
        }

        return headers;
    }
}
