// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using System.Diagnostics;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

namespace OrbitPublish;

public class CmdSerial
{
    public static async Task<int> Run()
    {
        await using var client = new NatsClient();
        var js = client.CreateJetStreamContext();
        await js.CreateStreamAsync(new StreamConfig("TEST1", ["test1", "test1.>"]));

        Console.WriteLine("Creat stream");

        int numberOfMessages = 1_000_000;
        var payload = new byte[128];

        Stopwatch stopwatch = Stopwatch.StartNew();

        Console.WriteLine("Start publishing");
        for (int i = 0; i < numberOfMessages; i++)
        {
            var ack = await js.PublishAsync("test1", payload);
            ack.EnsureSuccess();
        }

        stopwatch.Stop();

        Console.WriteLine("Received all acks");
        Console.WriteLine($"{stopwatch.Elapsed} {numberOfMessages / stopwatch.Elapsed.TotalSeconds:N0} msgs/sec");
        return 0;
    }
}
