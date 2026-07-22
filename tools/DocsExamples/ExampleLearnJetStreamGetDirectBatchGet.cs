// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

// dotnet add package nats.net
// dotnet add package Synadia.Orbit.JetStream.Extensions --prerelease
using NATS.Client.Core;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Extensions;
using Synadia.Orbit.JetStream.Extensions.Models;

namespace DocsExamples;

public class ExampleLearnJetStreamGetDirectBatchGet
{
    public static async Task Run()
    {
        var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
        await using var client = new NatsClient(url);
        var js = client.CreateJetStreamContext();

        // Batch direct get needs a stream with direct access enabled.
        await js.CreateStreamAsync(new StreamConfig("ORDERS", ["orders.>"])
        {
            AllowDirect = true,
        });

        await js.PublishAsync("orders.created", "{\"sku\":\"COFFEE-1KG\",\"qty\":2}");
        await js.PublishAsync("orders.shipped", "{\"sku\":\"COFFEE-1KG\",\"qty\":2}");
        await js.PublishAsync("orders.created", "{\"sku\":\"FILTER-100\",\"qty\":1}");

        // NATS-DOC-START
        // Fetch up to three messages starting at stream sequence 1, in one request.
        var request = new StreamMsgBatchGetRequest
        {
            Seq = 1,
            Batch = 3,
        };

        // The server streams the messages back in sequence order.
        await foreach (NatsMsg<string> msg in js.GetBatchDirectAsync<string>("ORDERS", request))
        {
            // On a direct get the stream sequence and original subject come back in
            // headers, not in msg.Subject (which is the reply subject).
            msg.Headers!.TryGetValue("Nats-Sequence", out var sequence);
            msg.Headers!.TryGetValue("Nats-Subject", out var subject);

            Console.WriteLine($"seq {sequence}: {subject}");
        }
        // NATS-DOC-END
    }
}
