// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using System.Diagnostics;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Publisher;

namespace OrbitPublish;

public class CmdPublisher
{
    public static async Task<int> Run()
    {
        var cts = new CancellationTokenSource();
        await using var client = new NatsClient();
        var js = client.CreateJetStreamContext();
        await js.CreateStreamAsync(new StreamConfig("TEST1", ["test1", "test1.>"]));

        Console.WriteLine("Creat stream");
        JetStreamPublisher<byte[]> publisher = client.CreateOrbitJetStreamPublisher<byte[]>();

        await publisher.StartAsync(cts.Token);

        int numberOfMessages = 1_000_000;

        var sub = Task.Run(
            async () =>
            {
                int count = 0;
                await foreach (var status in publisher.SubscribeAsync(cts.Token))
                {
                    count++;

                    if (count == numberOfMessages)
                    {
                        await cts.CancelAsync();
                        break;
                    }

                    if (!status.Acknowledged)
                    {
                        if (status.NoResponders)
                        {

                        }
                    }
                }
            },
            cts.Token);

        Stopwatch stopwatch = Stopwatch.StartNew();

        var payload = new byte[128];
        Console.WriteLine("Start publishing");
        for (int i = 0; i < numberOfMessages; i++)
        {
            await publisher.PublishAsync("test1", payload, cancellationToken: cts.Token);
        }

        await sub;
        stopwatch.Stop();

        Console.WriteLine("Received all acks");
        Console.WriteLine($"{stopwatch.Elapsed} {numberOfMessages / stopwatch.Elapsed.TotalSeconds:N0} msgs/sec");
        return 0;
    }
}
