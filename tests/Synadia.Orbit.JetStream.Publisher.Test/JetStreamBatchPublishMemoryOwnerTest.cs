// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Publisher.Test;

[Collection("nats-server")]
public class JetStreamBatchPublishMemoryOwnerTest
{
    private readonly NatsServerFixture _server;

    public JetStreamBatchPublishMemoryOwnerTest(NatsServerFixture server)
    {
        _server = server;
    }

    [Fact]
    public async Task Memory_owner_basic_batch_publishing()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support batch publish (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        await using var batch = new NatsJSBatchPublisher(js);

        await batch.AddAsync($"{subject}.1", AllocateOwner("message 1"u8), cancellationToken: ct);
        await batch.AddMsgAsync(
            new NatsMsg<NatsMemoryOwner<byte>>
            {
                Subject = $"{subject}.2",
                Data = AllocateOwner("message 2"u8),
            },
            cancellationToken: ct);

        Assert.Equal(2, batch.Size);

        await stream.RefreshAsync(ct);
        Assert.Equal(0L, stream.Info.State.Messages);

        var ack = await batch.CommitAsync($"{subject}.3", AllocateOwner("message 3"u8), cancellationToken: ct);

        Assert.NotNull(ack);
        Assert.Equal(3, ack.BatchSize);
        Assert.Equal(streamName, ack.Stream);

        Assert.True(batch.IsClosed);

        await stream.RefreshAsync(ct);
        Assert.Equal(3L, stream.Info.State.Messages);

        // Read messages back and verify payloads were written correctly through the pooled-buffer path.
        var consumer = await stream.CreateOrUpdateConsumerAsync(
            new ConsumerConfig($"{prefix}consumer") { AckPolicy = ConsumerConfigAckPolicy.None },
            ct);
        var seen = new List<string>();
        await foreach (var m in consumer.FetchAsync<string>(new NatsJSFetchOpts { MaxMsgs = 3 }, cancellationToken: ct))
        {
            seen.Add(m.Data!);
        }

        Assert.Equal(new[] { "message 1", "message 2", "message 3" }, seen);
    }

    [Fact]
    public async Task Memory_owner_commit_msg_and_publish_msg_batch()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support batch publish (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        const int count = 10;
        var messages = new NatsMsg<NatsMemoryOwner<byte>>[count];
        for (int i = 0; i < count; i++)
        {
            messages[i] = new NatsMsg<NatsMemoryOwner<byte>>
            {
                Subject = $"{subject}.subject",
                Data = AllocateOwner($"message {i}"),
            };
        }

        var ack = await js.PublishMsgBatchAsync(messages, cancellationToken: ct);

        Assert.NotNull(ack);
        Assert.Equal(count, ack.BatchSize);

        await stream.RefreshAsync(ct);
        Assert.Equal(count, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Memory_owner_with_flow_control_ack()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support batch publish (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        // AckFirst exercises the RequestAsync flow-control path (not just fire-and-forget publish)
        // for an IMemoryOwner payload.
        await using var batch = new NatsJSBatchPublisher(
            js,
            new NatsJSBatchFlowControl
            {
                AckFirst = true,
                AckTimeout = TimeSpan.FromSeconds(5),
            });

        await batch.AddAsync($"{subject}.1", AllocateOwner("hello"u8), cancellationToken: ct);
        var ack = await batch.CommitMsgAsync(
            new NatsMsg<NatsMemoryOwner<byte>>
            {
                Subject = $"{subject}.2",
                Data = AllocateOwner("world"u8),
            },
            cancellationToken: ct);

        Assert.Equal(2, ack.BatchSize);

        await stream.RefreshAsync(ct);
        Assert.Equal(2L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Memory_owner_disposed_when_batch_already_closed()
    {
        // Regression: pooled buffers transferred to the publisher must be disposed even when
        // the call throws before reaching the wire (closed batch, invalid opts).
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support batch publish (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var ct = TestContext.Current.CancellationToken;

        await using var batch = new NatsJSBatchPublisher(js);
        batch.Discard();

        var pool = new TrackingArrayPool<byte>();

        var addOwner = NatsMemoryOwner<byte>.Allocate(8, pool);
        await Assert.ThrowsAsync<NatsJSBatchClosedException>(
            async () => await batch.AddAsync("subj", addOwner, cancellationToken: ct));

        var commitOwner = NatsMemoryOwner<byte>.Allocate(8, pool);
        await Assert.ThrowsAsync<NatsJSBatchClosedException>(
            async () => await batch.CommitAsync("subj", commitOwner, cancellationToken: ct));

        Assert.Equal(2, pool.ReturnCount);
    }

    private static NatsMemoryOwner<byte> AllocateOwner(ReadOnlySpan<byte> data)
    {
        var owner = NatsMemoryOwner<byte>.Allocate(data.Length);
        data.CopyTo(owner.Memory.Span);
        return owner;
    }

    private static NatsMemoryOwner<byte> AllocateOwner(string text)
        => AllocateOwner(System.Text.Encoding.UTF8.GetBytes(text));

    private sealed class TrackingArrayPool<T> : ArrayPool<T>
    {
        private readonly ArrayPool<T> _inner = ArrayPool<T>.Shared;
        private int _returnCount;

        public int ReturnCount => Volatile.Read(ref _returnCount);

        public override T[] Rent(int minimumLength) => _inner.Rent(minimumLength);

        public override void Return(T[] array, bool clearArray = false)
        {
            Interlocked.Increment(ref _returnCount);
            _inner.Return(array, clearArray);
        }
    }
}
