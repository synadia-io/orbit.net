// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

// dotnet add package nats.net
// dotnet add package Synadia.Orbit.Counters --prerelease
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.Counters;

namespace DocsExamples;

public class ExampleCounters
{
    public static async Task Run()
    {
        await using var client = new NatsClient();
        var js = client.CreateJetStreamContext();

        // Create a counter-enabled stream
        await js.CreateStreamAsync(new StreamConfig("COUNTERS", ["counter.>"])
        {
            AllowMsgCounter = true,
            AllowDirect = true,
        });

        // Get a counter handle
        var counter = await js.GetCounterAsync("COUNTERS");

        // Increment
        var value = await counter.AddAsync("counter.hits", 1);
        Console.WriteLine($"After increment: {value}");

        // Load current value
        var current = await counter.LoadAsync("counter.hits");
        Console.WriteLine($"Current value: {current}");

        // Get full entry with source tracking
        var entry = await counter.GetAsync("counter.hits");
        Console.WriteLine($"Entry - Subject: {entry.Subject}, Value: {entry.Value}");

        // Get multiple counters at once
        await counter.AddAsync("counter.hits2", 2);
        await counter.AddAsync("counter.hits3", 3);
        await foreach (var e in counter.GetManyAsync(new[] { "counter.>" }))
        {
            Console.WriteLine($"{e.Subject}: {e.Value}");
        }
    }
}
