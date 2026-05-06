// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Publisher.Test;

[Collection("nats-server")]
public class JetStreamFastPublishTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public JetStreamFastPublishTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Basic_fast_batch_publishing()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowBatchPublish = true },
            ct);

        await using var batch = js.CreateOrbitFastPublisher();

        var ack1 = await batch.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct);
        Assert.Equal(1, ack1.BatchSequence);

        var ack2 = await batch.AddMsgAsync(
            new NatsMsg<byte[]> { Subject = $"{subject}.2", Data = "message 2"u8.ToArray() },
            cancellationToken: ct);
        Assert.Equal(2, ack2.BatchSequence);

        var commitAck = await batch.CommitAsync($"{subject}.3", "message 3"u8.ToArray(), cancellationToken: ct);

        Assert.NotNull(commitAck);
        Assert.Equal(3, commitAck.BatchSize);
        Assert.NotEmpty(commitAck.BatchId);
        Assert.Equal(streamName, commitAck.Stream);
        Assert.True(batch.IsClosed);

        await Assert.ThrowsAsync<NatsJSBatchClosedException>(
            async () => await batch.AddAsync($"{subject}.4", "message 4"u8.ToArray(), cancellationToken: ct));

        await stream.RefreshAsync(ct);
        Assert.Equal(3L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Fast_batch_with_flow_control()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowBatchPublish = true },
            ct);

        var opts = new NatsJSFastPublisherOpts
        {
            FlowControl = new NatsJSFastPublishFlowControl
            {
                Flow = 50,
                MaxOutstandingAcks = 3,
                AckTimeout = TimeSpan.FromSeconds(5),
            },
        };

        await using var batch = js.CreateOrbitFastPublisher(opts);

        for (int i = 0; i < 200; i++)
        {
            await batch.AddAsync($"{subject}.msg", "data"u8.ToArray(), cancellationToken: ct);
        }

        var commitAck = await batch.CommitAsync($"{subject}.final", "final"u8.ToArray(), cancellationToken: ct);

        Assert.Equal(201, commitAck.BatchSize);

        await stream.RefreshAsync(ct);
        Assert.Equal(201L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Fast_batch_close_without_final_message()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowBatchPublish = true },
            ct);

        await using var batch = js.CreateOrbitFastPublisher();

        await batch.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct);
        await batch.AddAsync($"{subject}.2", "message 2"u8.ToArray(), cancellationToken: ct);

        var commitAck = await batch.CloseAsync(ct);

        Assert.Equal(2, commitAck.BatchSize);
        Assert.True(batch.IsClosed);

        await Assert.ThrowsAsync<NatsJSBatchClosedException>(
            async () => await batch.AddAsync($"{subject}.3", "message 3"u8.ToArray(), cancellationToken: ct));

        await stream.RefreshAsync(ct);
        Assert.Equal(2L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Fast_batch_close_empty_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        await using var batch = js.CreateOrbitFastPublisher();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await batch.CloseAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Fast_batch_commit_without_add_throws()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        await using var batch = js.CreateOrbitFastPublisher();
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await batch.CommitAsync("subject", "data"u8.ToArray(), cancellationToken: ct));
    }

    [Fact]
    public async Task Fast_batch_continue_on_gap()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowBatchPublish = true },
            ct);

        var opts = new NatsJSFastPublisherOpts
        {
            ContinueOnGap = true,
        };

        await using var batch = js.CreateOrbitFastPublisher(opts);

        for (int i = 0; i < 5; i++)
        {
            await batch.AddAsync($"{subject}.msg", "data"u8.ToArray(), cancellationToken: ct);
        }

        var commitAck = await batch.CommitAsync($"{subject}.final", "final"u8.ToArray(), cancellationToken: ct);

        Assert.Equal(6, commitAck.BatchSize);
    }

    [Fact]
    public async Task Fast_batch_not_enabled_throws_on_commit()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 14), $"Server version {connection.ServerInfo?.Version} does not support fast batch publish (requires 2.14+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]),
            ct);

        await using var batch = js.CreateOrbitFastPublisher(new NatsJSFastPublisherOpts
        {
            FlowControl = new NatsJSFastPublishFlowControl { AckTimeout = TimeSpan.FromSeconds(2) },
        });

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await batch.AddAsync($"{subject}.1", "msg"u8.ToArray(), cancellationToken: ct));
    }
}
