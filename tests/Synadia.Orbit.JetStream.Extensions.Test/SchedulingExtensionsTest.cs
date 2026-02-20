// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Extensions.Models;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Extensions.Test;

[Collection("nats-server")]
public class SchedulingExtensionsTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public SchedulingExtensionsTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public void NatsMsgSchedule_ToHeaders_formats_headers_correctly()
    {
        // Arrange
        var scheduleAt = new DateTimeOffset(2025, 12, 25, 10, 30, 0, TimeSpan.Zero);
        var schedule = new NatsMsgSchedule(scheduleAt, "events.target")
        {
            Ttl = TimeSpan.FromSeconds(120),
        };

        // Act
        var headers = schedule.ToHeaders();

        // Assert
        Assert.Equal("@at 2025-12-25T10:30:00Z", headers["Nats-Schedule"]);
        Assert.Equal("events.target", headers["Nats-Schedule-Target"]);
        Assert.Equal("120s", headers["Nats-Schedule-TTL"]);
    }

    [Fact]
    public void NatsMsgSchedule_ToHeaders_calculates_default_ttl()
    {
        // Arrange
        var scheduleAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var schedule = new NatsMsgSchedule(scheduleAt, "events.target");

        // Act
        var headers = schedule.ToHeaders();

        // Assert - No default TTL set
        Assert.Empty(headers["Nats-Schedule-TTL"].ToArray());
    }

    [Fact]
    public void NatsMsgSchedule_ToHeaders_ensures_minimum_ttl_for_past_schedule()
    {
        // Arrange - schedule in the past
        var scheduleAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var schedule = new NatsMsgSchedule(scheduleAt, "events.target");

        // Act
        var headers = schedule.ToHeaders();

        // Assert - default TTL should not be set
        Assert.Empty(headers["Nats-Schedule-TTL"].ToArray());
    }

    [Fact]
    public void NatsMsgSchedule_ToHeaders_merges_with_existing_headers()
    {
        // Arrange
        var scheduleAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var schedule = new NatsMsgSchedule(scheduleAt, "events.target") { Ttl = TimeSpan.FromSeconds(100) };
        var existingHeaders = new NatsHeaders
        {
            ["Custom-Header"] = "custom-value",
        };

        // Act
        var headers = schedule.ToHeaders(existingHeaders);

        // Assert
        Assert.Equal("custom-value", headers["Custom-Header"]);
        Assert.Equal("events.target", headers["Nats-Schedule-Target"]);
    }

    [Fact]
    public void NatsMsgSchedule_throws_on_empty_target()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new NatsMsgSchedule(DateTimeOffset.UtcNow, string.Empty));
        Assert.Throws<ArgumentException>(() => new NatsMsgSchedule(DateTimeOffset.UtcNow, "   "));
    }

    [Fact]
    public void NatsMsgSchedule_string_constructor_throws_on_empty_schedule()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new NatsMsgSchedule(string.Empty, "events.target"));
        Assert.Throws<ArgumentException>(() => new NatsMsgSchedule("   ", "events.target"));
    }

    [Fact]
    public void NatsMsgSchedule_string_constructor_throws_on_empty_target()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new NatsMsgSchedule("@every 5m", string.Empty));
        Assert.Throws<ArgumentException>(() => new NatsMsgSchedule("@every 5m", "   "));
    }

    [Fact]
    public void NatsMsgSchedule_interval_constructor_formats_minutes()
    {
        var schedule = new NatsMsgSchedule(TimeSpan.FromMinutes(5), "events.target");

        var headers = schedule.ToHeaders();

        Assert.Equal("@every 5m", headers["Nats-Schedule"]);
        Assert.Equal(TimeSpan.FromMinutes(5), schedule.Interval);
        Assert.Null(schedule.ScheduleAt);
    }

    [Fact]
    public void NatsMsgSchedule_interval_constructor_formats_composite()
    {
        var schedule = new NatsMsgSchedule(TimeSpan.FromSeconds(3661), "events.target");

        var headers = schedule.ToHeaders();

        Assert.Equal("@every 1h1m1s", headers["Nats-Schedule"]);
    }

    [Fact]
    public void NatsMsgSchedule_interval_constructor_formats_hours_only()
    {
        var schedule = new NatsMsgSchedule(TimeSpan.FromHours(2), "events.target");

        var headers = schedule.ToHeaders();

        Assert.Equal("@every 2h", headers["Nats-Schedule"]);
    }

    [Fact]
    public void NatsMsgSchedule_interval_constructor_formats_seconds_only()
    {
        var schedule = new NatsMsgSchedule(TimeSpan.FromSeconds(30), "events.target");

        var headers = schedule.ToHeaders();

        Assert.Equal("@every 30s", headers["Nats-Schedule"]);
    }

    [Fact]
    public void NatsMsgSchedule_interval_constructor_throws_on_subsecond()
    {
        // Below minimum
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NatsMsgSchedule(TimeSpan.FromMilliseconds(500), "events.target"));

        // Above minimum but fractional — would silently lose the 500ms
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NatsMsgSchedule(TimeSpan.FromMilliseconds(1500), "events.target"));

        // Sub-millisecond ticks
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NatsMsgSchedule(TimeSpan.FromTicks(TimeSpan.TicksPerSecond + 1), "events.target"));
    }

    [Fact]
    public void NatsMsgSchedule_interval_constructor_throws_on_empty_target()
    {
        Assert.Throws<ArgumentException>(() =>
            new NatsMsgSchedule(TimeSpan.FromMinutes(5), string.Empty));
    }

    [Fact]
    public void NatsMsgSchedule_converts_to_utc()
    {
        // Arrange - use a non-UTC timezone
        var localTime = new DateTimeOffset(2025, 12, 25, 10, 30, 0, TimeSpan.FromHours(5));
        var schedule = new NatsMsgSchedule(localTime, "events.target") { Ttl = TimeSpan.FromSeconds(60) };

        // Act
        var headers = schedule.ToHeaders();

        // Assert - should be converted to UTC (10:30 + 5 hours offset = 05:30 UTC)
        Assert.Equal("@at 2025-12-25T05:30:00Z", headers["Nats-Schedule"]);
    }

    [Fact]
    public void NatsMsgSchedule_string_constructor_formats_raw_schedule()
    {
        // Arrange
        var schedule = new NatsMsgSchedule("@every 5m", "events.target");

        // Act
        var headers = schedule.ToHeaders();

        // Assert
        Assert.Equal("@every 5m", headers["Nats-Schedule"]);
        Assert.Equal("events.target", headers["Nats-Schedule-Target"]);
        Assert.Null(schedule.ScheduleAt);
    }

    [Fact]
    public void NatsMsgSchedule_source_header_is_set()
    {
        // Arrange
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", "events.target")
        {
            Source = "events.data",
        };

        // Act
        var headers = schedule.ToHeaders();

        // Assert
        Assert.Equal("events.data", headers["Nats-Schedule-Source"]);
    }

    [Fact]
    public void NatsMsgSchedule_source_header_is_not_set_when_null()
    {
        // Arrange
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", "events.target");

        // Act
        var headers = schedule.ToHeaders();

        // Assert
        Assert.Empty(headers["Nats-Schedule-Source"].ToArray());
    }

    [Fact]
    public void NatsMsgSchedule_ttl_max_value_formats_as_never()
    {
        // Arrange
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", "events.target")
        {
            Ttl = TimeSpan.MaxValue,
        };

        // Act
        var headers = schedule.ToHeaders();

        // Assert
        Assert.Equal("never", headers["Nats-Schedule-TTL"]);
    }

    [Fact]
    public void NatsMsgSchedule_ttl_below_one_second_throws()
    {
        // Arrange
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", "events.target")
        {
            Ttl = TimeSpan.FromMilliseconds(500),
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.ToHeaders());
    }

    [Fact]
    public async Task PublishScheduledAsync_publishes_message_with_schedule_headers()
    {
        // Arrange
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        if (!connection.HasMinServerVersion(2, 12))
        {
            _output.WriteLine($"Skipping test - server version {connection.ServerInfo?.Version} does not support scheduling (requires 2.12+)");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();
        string streamName = $"{prefix}SCHED";
        string scheduleSubject = $"{prefix}scheduling.input";
        string targetSubject = $"{prefix}events.output";

        CancellationToken ct = TestContext.Current.CancellationToken;

        // Create stream with scheduling enabled
        await js.CreateStreamAsync(
            new StreamConfig(streamName, [scheduleSubject, targetSubject])
            {
                AllowMsgSchedules = true,
                AllowMsgTTL = true,
            },
            ct);

        // Act
        var scheduleAt = DateTimeOffset.UtcNow.AddSeconds(2);
        var schedule = new NatsMsgSchedule(scheduleAt, targetSubject);

        var ack = await js.PublishScheduledAsync(
            subject: scheduleSubject,
            data: "scheduled message",
            schedule: schedule,
            cancellationToken: ct);

        // Assert
        ack.EnsureSuccess();
        Assert.True(ack.Seq > 0);
        _output.WriteLine($"Published scheduled message, seq={ack.Seq}");
    }

    [Fact]
    public async Task Schedule_source_should_publish_sourced_data_to_target()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        if (!connection.HasMinServerVersion(2, 14))
        {
            _output.WriteLine($"Skipping test - server version {connection.ServerInfo?.Version} does not support schedule source (requires 2.14+)");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();

        CancellationToken ct = TestContext.Current.CancellationToken;

        // Create a stream with AllowMsgSchedules and AllowMsgTTL enabled
        var streamConfig = new StreamConfig($"{prefix}s1", [$"{prefix}foo.*"])
        {
            AllowMsgSchedules = true,
            AllowMsgTTL = true,
            AllowDirect = true,
        };

        await js.CreateStreamAsync(streamConfig, ct);

        // Publish a data message with headers
        var dataHeaders = new NatsHeaders { { "Header", "Value" } };
        await js.PublishAsync($"{prefix}foo.data", "data", headers: dataHeaders, cancellationToken: ct);

        // Publish a scheduled message with source and TTL
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Source = $"{prefix}foo.data",
            Ttl = TimeSpan.FromMinutes(5),
        };

        await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        // Wait for the scheduled message to be published and schedule purged
        var stream = await js.GetStreamAsync($"{prefix}s1", cancellationToken: ct);
        await WaitUntilAsync(
            async () =>
            {
                await stream.RefreshAsync(ct).ConfigureAwait(false);
                return stream.Info.State.LastSeq == 3 && stream.Info.State.Messages == 2;
            },
            TimeSpan.FromSeconds(10),
            ct);

        // Verify the sourced message has the correct data and headers
        var msg = await stream.GetDirectAsync<string>(
            new StreamMsgGetRequest { LastBySubj = $"{prefix}foo.publish" },
            cancellationToken: ct);

        Assert.Equal("data", msg.Data);
        Assert.NotNull(msg.Headers);
        Assert.Equal($"{prefix}foo.schedule", msg.Headers["Nats-Scheduler"].ToString());
        Assert.Equal("purge", msg.Headers["Nats-Schedule-Next"].ToString());
        Assert.Equal("300s", msg.Headers["Nats-TTL"].ToString());
        Assert.Equal("Value", msg.Headers["Header"].ToString());

        // Schedule headers should be stripped from the produced message
        Assert.False(msg.Headers.ContainsKey("Nats-Schedule"));
        Assert.False(msg.Headers.ContainsKey("Nats-Schedule-Target"));
        Assert.False(msg.Headers.ContainsKey("Nats-Schedule-Source"));
        Assert.False(msg.Headers.ContainsKey("Nats-Schedule-TTL"));
    }

    [Fact]
    public async Task Schedule_ttl_without_allow_msg_ttl_should_return_error()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        if (!connection.HasMinServerVersion(2, 14))
        {
            _output.WriteLine($"Skipping test - server version {connection.ServerInfo?.Version} does not support schedule source (requires 2.14+)");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();

        CancellationToken ct = TestContext.Current.CancellationToken;

        // Create stream with schedules but WITHOUT AllowMsgTTL
        var streamConfig = new StreamConfig($"{prefix}s1", [$"{prefix}foo.*"])
        {
            AllowMsgSchedules = true,
        };

        await js.CreateStreamAsync(streamConfig, ct);

        // Publishing a scheduled message with TTL should fail when AllowMsgTTL is disabled
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Ttl = TimeSpan.FromSeconds(30),
        };

        var ack = await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        Assert.NotNull(ack.Error);
        Assert.Equal(400, ack.Error.Code);
        Assert.Equal(10166, ack.Error.ErrCode); // per-message TTL is disabled
    }

    [Fact]
    public async Task Schedule_source_invalid_should_return_error()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        if (!connection.HasMinServerVersion(2, 14))
        {
            _output.WriteLine($"Skipping test - server version {connection.ServerInfo?.Version} does not support schedule source (requires 2.14+)");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();

        CancellationToken ct = TestContext.Current.CancellationToken;

        var streamConfig = new StreamConfig($"{prefix}s1", [$"{prefix}foo.*"])
        {
            AllowMsgSchedules = true,
        };

        await js.CreateStreamAsync(streamConfig, ct);

        // Source matching the schedule subject should be invalid
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Source = $"{prefix}foo.schedule",
        };

        var ack = await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        Assert.NotNull(ack.Error);
        Assert.Equal(400, ack.Error.Code);
        Assert.Equal(10203, ack.Error.ErrCode);

        // Source matching the target subject should be invalid
        schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Source = $"{prefix}foo.publish",
        };

        ack = await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        Assert.NotNull(ack.Error);
        Assert.Equal(400, ack.Error.Code);
        Assert.Equal(10203, ack.Error.ErrCode);

        // Wildcard source (*) should be invalid
        schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Source = $"{prefix}foo.*",
        };

        ack = await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        Assert.NotNull(ack.Error);
        Assert.Equal(400, ack.Error.Code);
        Assert.Equal(10203, ack.Error.ErrCode);

        // Wildcard source (>) should be invalid
        schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Source = $"{prefix}foo.>",
        };

        ack = await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        Assert.NotNull(ack.Error);
        Assert.Equal(400, ack.Error.Code);
        Assert.Equal(10203, ack.Error.ErrCode);
    }

    [Fact]
    public async Task Schedule_without_source_should_publish_to_target()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        if (!connection.HasMinServerVersion(2, 14))
        {
            _output.WriteLine($"Skipping test - server version {connection.ServerInfo?.Version} does not support schedule source (requires 2.14+)");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();

        CancellationToken ct = TestContext.Current.CancellationToken;

        var streamConfig = new StreamConfig($"{prefix}s1", [$"{prefix}foo.*"])
        {
            AllowMsgSchedules = true,
            AllowDirect = true,
        };

        await js.CreateStreamAsync(streamConfig, ct);

        // Publish a scheduled message without source - the schedule message's own data is used
        var schedHeaders = new NatsHeaders { { "Custom", "MyValue" } };
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish");

        await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            "scheduled-payload",
            schedule,
            headers: schedHeaders,
            cancellationToken: ct);

        var stream = await js.GetStreamAsync($"{prefix}s1", cancellationToken: ct);
        await WaitUntilAsync(
            async () =>
            {
                await stream.RefreshAsync(ct).ConfigureAwait(false);
                return stream.Info.State.LastSeq == 2 && stream.Info.State.Messages == 1;
            },
            TimeSpan.FromSeconds(10),
            ct);

        // Verify the produced message has the schedule message's own data and custom headers
        var msg = await stream.GetDirectAsync<string>(
            new StreamMsgGetRequest { LastBySubj = $"{prefix}foo.publish" },
            cancellationToken: ct);

        Assert.Equal("scheduled-payload", msg.Data);
        Assert.NotNull(msg.Headers);
        Assert.Equal($"{prefix}foo.schedule", msg.Headers["Nats-Scheduler"].ToString());
        Assert.Equal("purge", msg.Headers["Nats-Schedule-Next"].ToString());
        Assert.Equal("MyValue", msg.Headers["Custom"].ToString());

        // Schedule headers should be stripped
        Assert.False(msg.Headers.ContainsKey("Nats-Schedule"));
        Assert.False(msg.Headers.ContainsKey("Nats-Schedule-Target"));
    }

    [Fact]
    public async Task Schedule_source_not_found_should_remove_schedule()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        if (!connection.HasMinServerVersion(2, 14))
        {
            _output.WriteLine($"Skipping test - server version {connection.ServerInfo?.Version} does not support schedule source (requires 2.14+)");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();

        CancellationToken ct = TestContext.Current.CancellationToken;

        var streamConfig = new StreamConfig($"{prefix}s1", [$"{prefix}foo.*"])
        {
            AllowMsgSchedules = true,
        };

        await js.CreateStreamAsync(streamConfig, ct);

        // Publish a scheduled message with a source subject that has no messages
        var schedule = new NatsMsgSchedule("@at 1970-01-01T00:00:00Z", $"{prefix}foo.publish")
        {
            Source = $"{prefix}foo.data",
        };

        var ack = await js.PublishScheduledAsync(
            $"{prefix}foo.schedule",
            (byte[]?)null,
            schedule,
            cancellationToken: ct);

        Assert.Null(ack.Error);
        Assert.Equal(1UL, ack.Seq);

        // The schedule fires but finds no source message, so it's removed from the scheduler.
        // The schedule message itself remains in the store.
        // Since this is a negative test (proving nothing happens), we wait for the scheduler
        // to have had a chance to fire, then verify no new message was produced.
        var stream = await js.GetStreamAsync($"{prefix}s1", cancellationToken: ct);
        await WaitUntilAsync(
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                await stream.RefreshAsync(ct).ConfigureAwait(false);
                return stream.Info.State.LastSeq == 1 && stream.Info.State.Messages == 1;
            },
            TimeSpan.FromSeconds(10),
            ct);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            if (await condition().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(250, cts.Token).ConfigureAwait(false);
        }

        throw new TimeoutException($"Condition was not met within {timeout}.");
    }
}
