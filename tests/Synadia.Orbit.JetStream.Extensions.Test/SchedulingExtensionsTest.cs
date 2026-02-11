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
    public async Task PublishScheduledAsync_publishes_message_with_schedule_headers()
    {
        // Arrange
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();

        // Check server version - scheduling requires 2.12+
        var version = connection.ServerInfo?.Version ?? "0.0.0";
        if (!version.StartsWith("2.12") && !version.StartsWith("2.13") && !version.StartsWith("2.14"))
        {
            _output.WriteLine($"Skipping test - server version {version} does not support scheduling (requires 2.12+)");
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
}
