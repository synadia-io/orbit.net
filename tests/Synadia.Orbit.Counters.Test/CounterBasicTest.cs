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
public class CounterBasicTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public CounterBasicTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Add_returns_correct_value()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        var value = await counter.AddAsync($"{prefix}.bar", new BigInteger(10), ct);
        Assert.Equal(new BigInteger(10), value);

        _output.WriteLine($"Add returned: {value}");
    }

    [Fact]
    public async Task AddInt_convenience_overload()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        var value = await counter.AddAsync($"{prefix}.bar", 5, ct);
        Assert.Equal(new BigInteger(5), value);
    }

    [Fact]
    public async Task Add_accumulates_values()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        await counter.AddAsync($"{prefix}.bar", new BigInteger(10), ct);
        var value = await counter.AddAsync($"{prefix}.bar", 5, ct);
        Assert.Equal(new BigInteger(15), value);
    }

    [Fact]
    public async Task Add_negative_value_decrements()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        await counter.AddAsync($"{prefix}.bar", new BigInteger(10), ct);
        var value = await counter.AddAsync($"{prefix}.bar", new BigInteger(-3), ct);
        Assert.Equal(new BigInteger(7), value);
    }

    [Fact]
    public async Task Add_zero_preserves_value()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        await counter.AddAsync($"{prefix}.bar", new BigInteger(10), ct);
        var value = await counter.AddAsync($"{prefix}.bar", BigInteger.Zero, ct);
        Assert.Equal(new BigInteger(10), value);
    }

    [Fact]
    public async Task Load_returns_current_value()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        await counter.AddAsync($"{prefix}.bar", new BigInteger(42), ct);
        var loaded = await counter.LoadAsync($"{prefix}.bar", ct);
        Assert.Equal(new BigInteger(42), loaded);
    }

    [Fact]
    public async Task Load_nonexistent_subject_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        var ex = await Assert.ThrowsAsync<NatsCounterException>(() => counter.LoadAsync($"{prefix}.nonexistent", ct).AsTask());
        Assert.Equal(2001, ex.Code);
    }

    [Fact]
    public async Task Get_returns_entry_with_value_and_increment()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        await counter.AddAsync($"{prefix}.bar", new BigInteger(100), ct);

        var entry = await counter.GetAsync($"{prefix}.bar", ct);
        Assert.Equal($"{prefix}.bar", entry.Subject);
        Assert.Equal(new BigInteger(100), entry.Value);
        Assert.True(entry.Sources == null || entry.Sources.Count == 0);
        Assert.Equal(new BigInteger(100), entry.Increment);
    }

    [Fact]
    public async Task Get_nonexistent_subject_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig($"{prefix}S", [$"{prefix}.>"]) { AllowMsgCounter = true, AllowDirect = true }, ct);
        var counter = await js.GetCounterAsync($"{prefix}S", ct);

        var ex = await Assert.ThrowsAsync<NatsCounterException>(() => counter.GetAsync($"{prefix}.nonexistent", ct).AsTask());
        Assert.Equal(2001, ex.Code);
    }
}
