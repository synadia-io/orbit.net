// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Extensions.Test;

[Collection("nats-server")]
public class GetAutoAsyncTest(NatsServerFixture server)
{
    [Fact]
    public async Task GetAutoAsync_WithAllowDirectAndExistingSeq_ReturnsDirectMessage()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}s1", "hello-world", cancellationToken: cts.Token);

        var result = await js.GetAutoAsync<string>(
            stream,
            new StreamMsgGetRequest { Seq = 1 },
            cancellationToken: cts.Token);

        Assert.Equal("hello-world", result.Data);
        Assert.Equal(1UL, result.Sequence);
        Assert.Equal($"{prefix}s1", result.Subject);
    }

    [Fact]
    public async Task GetAutoAsync_WithAllowDirectAndMissingSeq_ThrowsNoMessageFound()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await Assert.ThrowsAsync<NatsJSNoMessageFoundException>(async () =>
            await js.GetAutoAsync<string>(
                stream,
                new StreamMsgGetRequest { Seq = 999 },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetAutoAsync_WithoutAllowDirect_UsesStreamGet()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = false },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}s1", "hello-stream", cancellationToken: cts.Token);

        var result = await js.GetAutoAsync<string>(
            stream,
            new StreamMsgGetRequest { Seq = 1 },
            cancellationToken: cts.Token);

        Assert.Equal("hello-stream", result.Data);
        Assert.Equal(1UL, result.Sequence);
        Assert.Equal($"{prefix}s1", result.Subject);
    }

    [Fact]
    public async Task GetAutoAsync_WithLastBySubjAndAllowDirect_ReturnsDirectMessage()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}s1", "msg-1", cancellationToken: cts.Token);
        await js.PublishAsync($"{prefix}s1", "msg-2", cancellationToken: cts.Token);
        await js.PublishAsync($"{prefix}s1", "msg-3", cancellationToken: cts.Token);

        var result = await js.GetAutoAsync<string>(
            stream,
            new StreamMsgGetRequest { LastBySubj = $"{prefix}s1" },
            cancellationToken: cts.Token);

        Assert.Equal("msg-3", result.Data);
        Assert.Equal($"{prefix}s1", result.Subject);
    }

    [Fact]
    public async Task GetAutoAsync_WithLastBySubjMissing_ThrowsNoMessageFound()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await Assert.ThrowsAsync<NatsJSNoMessageFoundException>(async () =>
            await js.GetAutoAsync<string>(
                stream,
                new StreamMsgGetRequest { LastBySubj = $"{prefix}nonexistent" },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetAutoAsync_WithAllowDirectAndMultipleMessages_ReturnsCorrectSequence()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}s1", "first", cancellationToken: cts.Token);
        await js.PublishAsync($"{prefix}s1", "second", cancellationToken: cts.Token);
        await js.PublishAsync($"{prefix}s1", "third", cancellationToken: cts.Token);

        var result = await js.GetAutoAsync<string>(
            stream,
            new StreamMsgGetRequest { Seq = 2 },
            cancellationToken: cts.Token);

        Assert.Equal("second", result.Data);
        Assert.Equal(2UL, result.Sequence);
        Assert.Equal($"{prefix}s1", result.Subject);
    }

    [Fact]
    public async Task GetAutoAsync_DirectGetBadRequest_Throws()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}s1", "hello", cancellationToken: cts.Token);

        // Neither Seq nor LastBySubj set: the server answers 408 Bad Request.
        await Assert.ThrowsAsync<NatsJSException>(async () =>
            await js.GetAutoAsync<string>(stream, new StreamMsgGetRequest(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetAutoAsync_AllowDirectTurnedOffAfterInfoWasRead_FallsBack()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = true },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}s1", "hello", cancellationToken: cts.Token);

        // The handle above keeps AllowDirect = true in its cached info, so the direct
        // request goes out to a subject the server no longer has a responder on.
        await js.UpdateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = false },
            cancellationToken: cts.Token);

        var result = await js.GetAutoAsync<string>(
            stream,
            new StreamMsgGetRequest { Seq = 1 },
            cancellationToken: cts.Token);

        Assert.Equal("hello", result.Data);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAutoAsync_PreservesMessageHeaders(bool allowDirect)
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = allowDirect },
            cancellationToken: cts.Token);

        await js.PublishAsync(
            $"{prefix}s1",
            "hello",
            headers: new NatsHeaders { ["X-Test"] = "abc" },
            cancellationToken: cts.Token);

        var result = await js.GetAutoAsync<string>(
            stream,
            new StreamMsgGetRequest { Seq = 1 },
            cancellationToken: cts.Token);

        Assert.Equal("hello", result.Data);
        Assert.NotNull(result.Headers);
        Assert.Equal("abc", result.Headers!["X-Test"]);
    }

    [Fact]
    public async Task GetAutoAsync_DirectAndStreamPaths_ReportTimeConsistently()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var direct = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}D", [$"{prefix}d"]) { AllowDirect = true },
            cancellationToken: cts.Token);
        var indirect = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}N", [$"{prefix}n"]) { AllowDirect = false },
            cancellationToken: cts.Token);

        await js.PublishAsync($"{prefix}d", "hello", cancellationToken: cts.Token);
        await js.PublishAsync($"{prefix}n", "hello", cancellationToken: cts.Token);

        var fromDirect = await js.GetAutoAsync<string>(direct, new StreamMsgGetRequest { Seq = 1 }, cancellationToken: cts.Token);
        var fromStream = await js.GetAutoAsync<string>(indirect, new StreamMsgGetRequest { Seq = 1 }, cancellationToken: cts.Token);

        Assert.NotEqual(default, fromDirect.Time);
        Assert.NotEqual(default, fromStream.Time);
        Assert.Equal(fromStream.Time.Offset, fromDirect.Time.Offset);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAutoAsync_MissingMessage_ThrowsNoMessageFoundOnBothPaths(bool allowDirect)
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = server.Url });
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(nats.HasMinServerVersion(2, 10), $"Server version {nats.ServerInfo?.Version} does not support direct get (requires 2.10+)");

        var prefix = server.GetNextId();
        var js = new NatsJSContext(nats);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stream = await js.CreateStreamAsync(
            new StreamConfig($"{prefix}S1", [$"{prefix}s1"]) { AllowDirect = allowDirect },
            cancellationToken: cts.Token);

        await Assert.ThrowsAsync<NatsJSNoMessageFoundException>(async () =>
            await js.GetAutoAsync<string>(
                stream,
                new StreamMsgGetRequest { Seq = 999 },
                cancellationToken: cts.Token));
    }
}
