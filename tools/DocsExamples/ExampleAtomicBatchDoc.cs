// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

// dotnet add package nats.net
// dotnet add package Synadia.Orbit.JetStream.Publisher --prerelease
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Publisher;

namespace DocsExamples;

public class ExampleAtomicBatchDoc
{
    public static async Task Run()
    {
        await using var client = new NatsClient();
        var js = client.CreateJetStreamContext();

        // Atomic batches need a stream that opts in with AllowAtomicPublish.
        await js.CreateStreamAsync(new StreamConfig("ORDERS", ["orders.>"])
        {
            AllowAtomicPublish = true,
        });

        // NATS-DOC-START
        await using var batch = new NatsJSBatchPublisher(js);

        // Three line items of one order. They all land, or none do.
        await batch.AddAsync("orders.created", "{\"sku\":\"COFFEE-1KG\",\"qty\":2}");
        await batch.AddAsync("orders.created", "{\"sku\":\"FILTER-100\",\"qty\":1}");

        // The final item commits the batch and returns one ack for the whole order.
        NatsJSBatchAck ack = await batch.CommitAsync("orders.created", "{\"sku\":\"MUG-350ML\",\"qty\":4}");

        Console.WriteLine($"Committed batch {ack.BatchId}: {ack.BatchSize} messages into {ack.Stream}");
        // NATS-DOC-END
    }
}
