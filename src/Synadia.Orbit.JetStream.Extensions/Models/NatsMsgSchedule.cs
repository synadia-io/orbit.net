// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Extensions.Models;

/// <summary>
/// Represents a scheduled message configuration for JetStream message scheduling.
/// </summary>
/// <remarks>
/// <para>This record is used to specify parameters for scheduling a message to be delivered
/// at a future time to a target subject within the same stream.</para>
/// <para>Supported schedule types:</para>
/// <list type="bullet">
/// <item><c>@at</c> — one-time delivery at a specific time (NATS Server 2.12+)</item>
/// <item><c>@every</c> — repeating delivery at a fixed interval (NATS Server 2.14+)</item>
/// </list>
/// <para>Cron expressions are defined in <see href="https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-51.md">ADR-51</see>
/// but not yet implemented in the server. Use the <see cref="NatsMsgSchedule(string, string)"/> constructor
/// for forward compatibility with future schedule types.</para>
/// </remarks>
public record NatsMsgSchedule
{
    private const string NatsScheduleHeader = "Nats-Schedule";
    private const string NatsScheduleTargetHeader = "Nats-Schedule-Target";
    private const string NatsScheduleSourceHeader = "Nats-Schedule-Source";
    private const string NatsScheduleTtlHeader = "Nats-Schedule-TTL";

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMsgSchedule"/> class with a one-time <c>@at</c> schedule.
    /// Requires NATS Server 2.12 or later.
    /// </summary>
    /// <param name="scheduleAt">The scheduled delivery time. Will be converted to UTC and truncated to whole seconds
    /// (the server only supports second-level precision for <c>@at</c> schedules).</param>
    /// <param name="target">The target subject for message delivery.</param>
    /// <exception cref="ArgumentException">Thrown when target is null or whitespace.</exception>
    public NatsMsgSchedule(DateTimeOffset scheduleAt, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target subject cannot be null or whitespace.", nameof(target));
        }

        // Truncate to whole seconds — the server parses @at with time.RFC3339 (no fractional seconds).
        var utc = scheduleAt.ToUniversalTime();
        ScheduleAt = utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerSecond));
        Schedule = $"@at {ScheduleAt.Value.UtcDateTime:yyyy-MM-ddTHH:mm:ss}Z";
        Target = target;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMsgSchedule"/> class with a repeating <c>@every</c> interval.
    /// Requires NATS Server 2.14 or later.
    /// </summary>
    /// <param name="interval">The interval between firings. Must be at least 1 second with no sub-second component.</param>
    /// <param name="target">The target subject for message delivery.</param>
    /// <exception cref="ArgumentException">Thrown when target is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when interval is less than 1 second or has a sub-second component.</exception>
    public NatsMsgSchedule(TimeSpan interval, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target subject cannot be null or whitespace.", nameof(target));
        }

        if (interval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be at least 1 second.");
        }

        if (interval.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be a whole number of seconds.");
        }

        Interval = interval;
        Schedule = $"@every {FormatGoDuration(interval)}";
        Target = target;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMsgSchedule"/> class with a raw schedule expression.
    /// Use this constructor for forward compatibility with future schedule types (e.g. cron expressions).
    /// </summary>
    /// <param name="schedule">The schedule expression. Currently the server supports <c>@at</c> (2.12+)
    /// and <c>@every</c> (2.14+). Cron expressions are planned but not yet implemented server-side.</param>
    /// <param name="target">The target subject for message delivery.</param>
    /// <exception cref="ArgumentException">Thrown when schedule or target is null or whitespace.</exception>
    public NatsMsgSchedule(string schedule, string target)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            throw new ArgumentException("Schedule expression cannot be null or whitespace.", nameof(schedule));
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target subject cannot be null or whitespace.", nameof(target));
        }

        Schedule = schedule;
        Target = target;
    }

    /// <summary>
    /// Gets the scheduled delivery time in UTC, if this schedule was created with a <see cref="DateTimeOffset"/>.
    /// </summary>
    public DateTimeOffset? ScheduleAt { get; }

    /// <summary>
    /// Gets the repeating interval, if this schedule was created with a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan? Interval { get; }

    /// <summary>
    /// Gets the raw schedule expression.
    /// </summary>
    /// <remarks>
    /// Currently supported by the server: <c>@at</c> (2.12+) and <c>@every</c> (2.14+).
    /// Cron expressions are planned in ADR-51 but not yet implemented server-side.
    /// </remarks>
    public string Schedule { get; }

    /// <summary>
    /// Gets the target subject where the message will be delivered when the schedule fires.
    /// </summary>
    /// <remarks>
    /// The target subject must be within the stream's subject filter and must differ from
    /// the subject the scheduled message is published to.
    /// </remarks>
    public string Target { get; }

    /// <summary>
    /// Gets the optional TTL (Time To Live) for the message produced by the schedule.
    /// </summary>
    /// <remarks>
    /// Minimum value is 1 second. Use <see cref="TimeSpan.MaxValue"/> to indicate the message should never expire.
    /// Requires the stream to have <c>AllowMsgTTL</c> enabled.
    /// </remarks>
    public TimeSpan? Ttl { get; init; }

    /// <summary>
    /// Gets the optional source subject from which to source the last message's data and headers
    /// when the schedule fires. Requires NATS Server 2.14 or later.
    /// </summary>
    /// <remarks>
    /// The source subject must be a literal (no wildcards), and must not match the schedule or target subjects.
    /// If no message exists on the source subject when the schedule fires, the schedule is removed.
    /// Requires the stream to have <c>AllowMsgSchedules</c> enabled.
    /// </remarks>
    public string? Source { get; init; }

    /// <summary>
    /// Converts this schedule configuration to NATS headers.
    /// </summary>
    /// <param name="existingHeaders">Optional existing headers to merge with. If null, new headers are created.</param>
    /// <returns>A <see cref="NatsHeaders"/> instance containing the scheduling headers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Ttl"/> is less than 1 second and not <see cref="TimeSpan.MaxValue"/>.</exception>
    public NatsHeaders ToHeaders(NatsHeaders? existingHeaders = null)
    {
        var headers = existingHeaders ?? new NatsHeaders();

        headers[NatsScheduleHeader] = Schedule;
        headers[NatsScheduleTargetHeader] = Target;

        if (Source is { } source)
        {
            headers[NatsScheduleSourceHeader] = source;
        }

        if (Ttl is { } ttl)
        {
            if (ttl != TimeSpan.MaxValue && ttl < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(Ttl), "ScheduleTTL must be at least 1 second or TimeSpan.MaxValue.");
            }

            headers[NatsScheduleTtlHeader] = ttl == TimeSpan.MaxValue
                ? "never"
                : $"{(long)ttl.TotalSeconds}s";
        }

        return headers;
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a Go-compatible duration string (e.g. <c>"1h30m"</c>, <c>"5m"</c>, <c>"90s"</c>).
    /// </summary>
    private static string FormatGoDuration(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;

        // Build composite duration: "1h30m10s", "5m", "90s", etc.
        var result = string.Empty;
        if (hours > 0)
        {
            result += $"{hours}h";
        }

        if (minutes > 0)
        {
            result += $"{minutes}m";
        }

        if (seconds > 0 || result.Length == 0)
        {
            result += $"{seconds}s";
        }

        return result;
    }
}
