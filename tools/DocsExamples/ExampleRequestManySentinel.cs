// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using NATS.Client.Core;
using NATS.Net;
using Synadia.Orbit.Core.Extensions;

namespace DocsExamples;

public class ExampleRequestManySentinel
{
    public static async Task Run()
    {
        // dotnet add package nats.net
        // dotnet add package synadia.orbit.core.extensions --prerelease
        await using var client = new NatsClient();
        var nats = client.Connection;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        // Start a responder service that sends multiple responses
        var responderTask = Task.Run(async () =>
        {
            Console.WriteLine("[Responder] Waiting for requests...");
            await foreach (var msg in client.SubscribeAsync<string>("service.request", cancellationToken: ct))
            {
                Console.WriteLine($"[Responder] Received request: {msg.Data}");

                // Send multiple responses
                for (var i = 1; i <= 5; i++)
                {
                    var isLast = i == 5;
                    var headers = new NatsHeaders();
                    if (isLast)
                    {
                        headers["X-Done"] = "true";
                    }

                    await msg.ReplyAsync(
                        data: $"Response {i}",
                        headers: headers,
                        cancellationToken: ct);

                    Console.WriteLine($"[Responder] Sent response {i} (X-Done={isLast})");
                }

                break; // Only handle one request for this example
            }
        }, ct);

        // Give the responder time to start
        await Task.Delay(100, ct);

        // Example 1: Stop on header condition
        Console.WriteLine("\n[Client] Sending request with header-based sentinel...");
        await foreach (var msg in nats.RequestManyWithSentinelAsync<string, string>(
            subject: "service.request",
            data: "Hello",
            sentinel: m => m.Headers?["X-Done"].ToString() == "true",
            replyOpts: new NatsSubOpts { Timeout = TimeSpan.FromSeconds(5) },
            cancellationToken: ct))
        {
            Console.WriteLine($"[Client] Received: {msg.Data}");
        }

        Console.WriteLine("[Client] Done - sentinel triggered on X-Done header");

        await responderTask;
    }
}
