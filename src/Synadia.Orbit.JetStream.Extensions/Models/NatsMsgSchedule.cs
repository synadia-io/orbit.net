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
/// <item><c>@at</c>, one-time delivery at a specific time (NATS Server 2.12+)</item>
/// <item><c>@every</c>, repeating delivery at a fixed interval (NATS Server 2.14+)</item>
/// <item>Cron expressions and predefined schedules (<c>@hourly</c>, <c>@daily</c>, ...) (NATS Server 2.14+)</item>
/// </list>
/// <para>See <see href="https://github.com/nats-io/nats-architecture-and-design/blob/main/adr/ADR-51.md">ADR-51</see>
/// for the full schedule specification.</para>
/// </remarks>
public record NatsMsgSchedule
{
    private const string NatsScheduleHeader = "Nats-Schedule";
    private const string NatsScheduleTargetHeader = "Nats-Schedule-Target";
    private const string NatsScheduleSourceHeader = "Nats-Schedule-Source";
    private const string NatsScheduleTtlHeader = "Nats-Schedule-TTL";
    private const string NatsScheduleTimeZoneHeader = "Nats-Schedule-Time-Zone";

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
    /// Accepts cron expressions, predefined schedules (<c>@hourly</c>, <c>@daily</c>, ...),
    /// or any other schedule string the server understands.
    /// </summary>
    /// <param name="schedule">The schedule expression. Supported forms: <c>@at &lt;rfc3339&gt;</c> (2.12+),
    /// <c>@every &lt;duration&gt;</c> (2.14+), 6-field cron (<c>"0 0 * * * *"</c>) (2.14+),
    /// or predefined schedules <c>@yearly</c>/<c>@annually</c>/<c>@monthly</c>/<c>@weekly</c>/<c>@daily</c>/<c>@midnight</c>/<c>@hourly</c> (2.14+).</param>
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
    /// Server-supported forms: <c>@at</c> (2.12+), <c>@every</c> (2.14+), 6-field cron expressions (2.14+),
    /// and predefined schedules <c>@yearly</c>/<c>@annually</c>/<c>@monthly</c>/<c>@weekly</c>/<c>@daily</c>/<c>@midnight</c>/<c>@hourly</c> (2.14+).
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
    /// Gets the optional time zone for cron schedules. Requires NATS Server 2.14 or later.
    /// </summary>
    /// <remarks>
    /// Accepted values are those Go's <c>time.LoadLocation()</c> understands: an IANA Time Zone database name
    /// (<c>America/New_York</c>, <c>Europe/Amsterdam</c>, <c>Asia/Tokyo</c>), the literal <c>UTC</c>
    /// (equivalent to omitting the header), or the literal <c>Local</c> (uses the server's local time zone).
    /// Fixed offsets like <c>+02:00</c> and abbreviations like <c>EST</c> are not accepted.
    /// Only allowed on cron schedules; setting it together with <c>@at</c> or <c>@every</c> causes
    /// <see cref="ToHeaders"/> to throw.
    /// </remarks>
    public string? TimeZone { get; init; }

    /// <summary>
    /// Creates a schedule from a cron expression. Requires NATS Server 2.14 or later.
    /// </summary>
    /// <param name="cron">A 6-field cron expression (seconds minutes hours day-of-month month day-of-week).
    /// Example: <c>"0 0 * * * *"</c> for the start of every hour.</param>
    /// <param name="target">The target subject for message delivery.</param>
    /// <returns>A new <see cref="NatsMsgSchedule"/> configured with the given cron expression.</returns>
    public static NatsMsgSchedule Cron(string cron, string target) => new(cron, target);

    /// <summary>Creates a schedule that fires once a year at midnight on January 1st (UTC by default). Requires NATS Server 2.14 or later.</summary>
    /// <param name="target">The target subject for message delivery.</param>
    /// <returns>A new <see cref="NatsMsgSchedule"/> configured with <c>@yearly</c>.</returns>
    public static NatsMsgSchedule Yearly(string target) => new("@yearly", target);

    /// <summary>Creates a schedule that fires once a month at midnight on the first of the month (UTC by default). Requires NATS Server 2.14 or later.</summary>
    /// <param name="target">The target subject for message delivery.</param>
    /// <returns>A new <see cref="NatsMsgSchedule"/> configured with <c>@monthly</c>.</returns>
    public static NatsMsgSchedule Monthly(string target) => new("@monthly", target);

    /// <summary>Creates a schedule that fires once a week at midnight between Saturday and Sunday (UTC by default). Requires NATS Server 2.14 or later.</summary>
    /// <param name="target">The target subject for message delivery.</param>
    /// <returns>A new <see cref="NatsMsgSchedule"/> configured with <c>@weekly</c>.</returns>
    public static NatsMsgSchedule Weekly(string target) => new("@weekly", target);

    /// <summary>Creates a schedule that fires once a day at midnight (UTC by default). Requires NATS Server 2.14 or later.</summary>
    /// <param name="target">The target subject for message delivery.</param>
    /// <returns>A new <see cref="NatsMsgSchedule"/> configured with <c>@daily</c>.</returns>
    public static NatsMsgSchedule Daily(string target) => new("@daily", target);

    /// <summary>Creates a schedule that fires at the start of every hour (UTC by default). Requires NATS Server 2.14 or later.</summary>
    /// <param name="target">The target subject for message delivery.</param>
    /// <returns>A new <see cref="NatsMsgSchedule"/> configured with <c>@hourly</c>.</returns>
    public static NatsMsgSchedule Hourly(string target) => new("@hourly", target);

    /// <summary>
    /// Converts this schedule configuration to NATS headers.
    /// </summary>
    /// <param name="existingHeaders">Optional existing headers to merge with. If null, new headers are created.</param>
    /// <returns>A <see cref="NatsHeaders"/> instance containing the scheduling headers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Ttl"/> is less than 1 second and not <see cref="TimeSpan.MaxValue"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="TimeZone"/> is set on a non-cron schedule (<c>@at</c> or <c>@every</c>).</exception>
    public NatsHeaders ToHeaders(NatsHeaders? existingHeaders = null)
    {
        var headers = existingHeaders ?? new NatsHeaders();

        headers[NatsScheduleHeader] = Schedule;
        headers[NatsScheduleTargetHeader] = Target;

        if (Source is { } source)
        {
            headers[NatsScheduleSourceHeader] = source;
        }

        if (TimeZone is { } timeZone)
        {
            if (Schedule.StartsWith("@at ", StringComparison.Ordinal) ||
                Schedule.StartsWith("@every ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Nats-Schedule-Time-Zone is only valid on cron schedules; not allowed with @at or @every.");
            }

            headers[NatsScheduleTimeZoneHeader] = timeZone;
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
