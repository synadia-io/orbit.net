// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.Counters.Test;

[Collection("nats-server")]
public class CounterGetManyTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public CounterGetManyTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task GetMany_wildcard_returns_all_entries()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support counters (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.*"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        var subjects = new[] { $"{prefix}.one", $"{prefix}.two", $"{prefix}.three" };
        var values = new[] { new BigInteger(10), new BigInteger(20), new BigInteger(30) };

        for (var i = 0; i < subjects.Length; i++)
        {
            await counter.AddAsync(subjects[i], values[i], ct);
        }

        var foundSubjects = new Dictionary<string, BigInteger>();
        await foreach (var entry in counter.GetManyAsync([$"{prefix}.*"], ct))
        {
            foundSubjects[entry.Subject] = entry.Value;
            _output.WriteLine($"GetMany: {entry.Subject} = {entry.Value}");
        }

        Assert.Equal(3, foundSubjects.Count);
        for (var i = 0; i < subjects.Length; i++)
        {
            Assert.True(foundSubjects.ContainsKey(subjects[i]), $"Missing subject: {subjects[i]}");
            Assert.Equal(values[i], foundSubjects[subjects[i]]);
        }
    }

    [Fact]
    public async Task GetMany_specific_subject_returns_single_entry()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support counters (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.*"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        await counter.AddAsync($"{prefix}.one", new BigInteger(10), ct);
        await counter.AddAsync($"{prefix}.two", new BigInteger(20), ct);

        var count = 0;
        await foreach (var entry in counter.GetManyAsync([$"{prefix}.one"], ct))
        {
            Assert.Equal($"{prefix}.one", entry.Subject);
            Assert.Equal(new BigInteger(10), entry.Value);
            count++;
        }

        Assert.Equal(1, count);
    }
}
