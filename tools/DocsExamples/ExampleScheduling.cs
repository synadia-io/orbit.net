// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Extensions;
using Synadia.Orbit.JetStream.Extensions.Models;

namespace DocsExamples;

public class ExampleScheduling
{
    public static async Task Run()
    {
        // dotnet add package nats.net
        // dotnet add package synadia.orbit.jetstream.extensions --prerelease
        await using var client = new NatsClient();
        var js = client.CreateJetStreamContext();

        Console.WriteLine("Create stream");
        var stream = await js.CreateStreamAsync(new StreamConfig("SCHEDULING_STREAM", ["scheduling.>", "events.>"])
        {
            AllowMsgSchedules = true,
            AllowMsgTTL = true,
        });

        // Schedule a message for 10 seconds from now
        var scheduleAt = DateTimeOffset.UtcNow.AddSeconds(10);
        var schedule = new NatsMsgSchedule(scheduleAt, "events.t1")
        {
            TtlSeconds = 15, // Optional: override the default TTL
        };

        Console.WriteLine($"Scheduling message for: {scheduleAt:yyyy-MM-ddTHH:mm:ss}Z");

        var ack = await js.PublishScheduledAsync(
            subject: "scheduling.x",
            data: $"message for later {scheduleAt}",
            schedule: schedule);

        ack.EnsureSuccess();
        Console.WriteLine($"Published scheduled message, seq={ack.Seq}");
    }
}
