// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.Counters.Test;

[Collection("nats-server")]
public class CounterValidationTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public CounterValidationTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task GetCounter_nonexistent_stream_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<NatsCounterException>(() => js.GetCounterAsync("nonexistent", ct).AsTask());
        Assert.Same(NatsCounterException.CounterNotFound, ex);
    }

    [Fact]
    public async Task CreateCounter_without_counter_enabled_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S", [$"{prefix}.*"]) { AllowMsgCounter = false, AllowDirect = true }, ct);

        var ex = Assert.Throws<NatsCounterException>(() => js.CreateCounter(stream));
        Assert.Same(NatsCounterException.CounterNotEnabled, ex);
    }

    [Fact]
    public async Task CreateCounter_without_direct_access_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S", [$"{prefix}.*"]) { AllowMsgCounter = true, AllowDirect = false }, ct);

        var ex = Assert.Throws<NatsCounterException>(() => js.CreateCounter(stream));
        Assert.Same(NatsCounterException.DirectAccessRequired, ex);
    }

    [Fact]
    public async Task CreateCounter_valid_stream_succeeds()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S", [$"{prefix}.*"]) { AllowMsgCounter = true, AllowDirect = true }, ct);

        var counter = js.CreateCounter(stream);
        Assert.NotNull(counter);
    }

    [Fact]
    public async Task GetCounter_valid_stream_succeeds()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S", [$"{prefix}.*"]) { AllowMsgCounter = true, AllowDirect = true }, ct);

        var counter = await js.GetCounterAsync($"{prefix}S", ct);
        Assert.NotNull(counter);
    }
}
