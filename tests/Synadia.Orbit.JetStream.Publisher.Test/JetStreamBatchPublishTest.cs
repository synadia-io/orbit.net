// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Publisher.Test;

[Collection("nats-server")]
public class JetStreamBatchPublishTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public JetStreamBatchPublishTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Basic_batch_publishing()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        // Create a batch publisher
        var batch = new BatchPublisher(js);

        // Add messages to the batch
        await batch.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct);
        await batch.AddMsgAsync(
            new NatsMsg<byte[]>
            {
                Subject = $"{subject}.2",
                Data = "message 2"u8.ToArray(),
            },
            cancellationToken: ct);

        // Check size
        Assert.Equal(2, batch.Size);

        // Verify no messages in stream yet
        await stream.RefreshAsync(ct);
        Assert.Equal(0L, stream.Info.State.Messages);

        // Commit the batch
        var ack = await batch.CommitAsync($"{subject}.3", "message 3"u8.ToArray(), cancellationToken: ct);

        Assert.NotNull(ack);
        Assert.Equal(3, ack.BatchSize);
        Assert.NotEmpty(ack.BatchId);
        Assert.Equal(streamName, ack.Stream);

        // Verify batch is closed
        Assert.True(batch.IsClosed);

        // Verify we can't add more messages
        await Assert.ThrowsAsync<BatchClosedException>(
            async () => await batch.AddAsync($"{subject}.4", "message 4"u8.ToArray(), cancellationToken: ct));

        // Verify we have 3 messages in the stream
        await stream.RefreshAsync(ct);
        Assert.Equal(3L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Batch_with_options()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing and TTL enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"])
            {
                AllowAtomicPublish = true,
            },
            ct);

        // Publish some initial messages
        for (int i = 0; i < 5; i++)
        {
            await js.PublishAsync($"{subject}.foo", "hello"u8.ToArray(), cancellationToken: ct);
        }

        await stream.RefreshAsync(ct);
        Assert.Equal(5L, stream.Info.State.Messages);

        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var batch = new BatchPublisher(js);

        // Add first message with expected last sequence
        await batch.AddAsync(
            $"{subject}.1",
            "message 1"u8.ToArray(),
            new BatchMsgOpts { LastSeq = 5, Stream = streamName },
            ct);

        // Add second message with expected stream
        await batch.AddMsgAsync(
            new NatsMsg<byte[]>
            {
                Subject = $"{subject}.2",
                Data = "message 2"u8.ToArray(),
            },
            new BatchMsgOpts { Stream = streamName },
            ct);

        // Commit third message
        var ack = await batch.CommitAsync($"{subject}.3", "message 3"u8.ToArray(), cancellationToken: ct);

        Assert.NotNull(ack);
        Assert.Equal(streamName, ack.Stream);

        await stream.RefreshAsync(ct);
        Assert.Equal(8L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task Expect_last_sequence_validation()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        var batch = new BatchPublisher(js);

        // First message with ExpectLastSequence should work
        await batch.AddAsync(
            $"{subject}.1",
            "message 1"u8.ToArray(),
            new BatchMsgOpts { LastSeq = 0 },
            ct);

        var ack = await batch.CommitAsync($"{subject}.2", "message 2"u8.ToArray(), cancellationToken: ct);

        Assert.NotNull(ack);
    }

    [Fact]
    public async Task Invalid_last_sequence()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        var batch = new BatchPublisher(js);

        // First message with invalid ExpectLastSequence should fail
        var ex = await Assert.ThrowsAsync<NatsJSException>(
            async () => await batch.CommitAsync(
                $"{subject}.1",
                "message 1"u8.ToArray(),
                new BatchMsgOpts { LastSeq = 5 },
                ct));

        _output.WriteLine($"Exception: {ex.Message}");
    }

    [Fact]
    public async Task Batch_discard()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        var batch = new BatchPublisher(js);

        // Add messages to the batch
        await batch.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct);
        await batch.AddMsgAsync(
            new NatsMsg<byte[]>
            {
                Subject = $"{subject}.2",
                Data = "message 2"u8.ToArray(),
            },
            cancellationToken: ct);

        // Discard the batch
        batch.Discard();

        // Try discarding again
        Assert.Throws<BatchClosedException>(() => batch.Discard());

        // Verify batch is closed
        Assert.True(batch.IsClosed);

        // Verify we can't add more messages
        await Assert.ThrowsAsync<BatchClosedException>(
            async () => await batch.AddAsync($"{subject}.4", "message 4"u8.ToArray(), cancellationToken: ct));

        // Verify we can't commit
        await Assert.ThrowsAsync<BatchClosedException>(
            async () => await batch.CommitAsync($"{subject}.5", "message 5"u8.ToArray(), cancellationToken: ct));

        // Verify we have 0 messages in the stream
        await stream.RefreshAsync(ct);
        Assert.Equal(0L, stream.Info.State.Messages);
    }

    [Fact]
    public async Task PublishMsgBatch_basic()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        const int count = 100;
        var messages = new NatsMsg<byte[]>[count];
        for (int i = 0; i < count; i++)
        {
            messages[i] = new NatsMsg<byte[]>
            {
                Subject = $"{subject}.subject",
                Data = "message"u8.ToArray(),
            };
        }

        var ack = await JetStreamBatchPublish.PublishMsgBatchAsync(js, messages, cancellationToken: ct);

        Assert.NotNull(ack);
        Assert.Equal(count, ack.BatchSize);

        // Verify we have count messages in the stream
        await stream.RefreshAsync(ct);
        Assert.Equal(count, stream.Info.State.Messages);
    }

    [Fact]
    public async Task PublishMsgBatch_too_many_messages()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        const int count = 1001;
        var messages = new NatsMsg<byte[]>[count];
        for (int i = 0; i < count; i++)
        {
            messages[i] = new NatsMsg<byte[]>
            {
                Subject = $"{subject}.subject",
                Data = "message"u8.ToArray(),
            };
        }

        await Assert.ThrowsAsync<BatchPublishExceedsLimitException>(
            async () => await JetStreamBatchPublish.PublishMsgBatchAsync(js, messages, cancellationToken: ct));
    }

    [Fact]
    public async Task Batch_publish_not_enabled()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream WITHOUT batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]),
            ct);

        // Create batch publisher with flow control enabled
        var batch = new BatchPublisher(
            js,
            new BatchFlowControl
            {
                AckFirst = true,
                AckTimeout = TimeSpan.FromSeconds(5),
            });

        // First message should fail with batch publish not enabled
        await Assert.ThrowsAsync<BatchPublishNotEnabledException>(
            async () => await batch.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct));
    }

    [Fact]
    public async Task Batch_size_limit()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        var batch = new BatchPublisher(js);

        // Add messages until we reach limit (999 + 1 commit = 1000)
        for (int i = 0; i < 999; i++)
        {
            await batch.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct);
        }

        // Commit is msg 1000 (within limit)
        var ack = await batch.CommitAsync($"{subject}.2", "message 2"u8.ToArray(), cancellationToken: ct);
        Assert.NotNull(ack);

        // Try to create another batch and add 1001 messages
        var batch2 = new BatchPublisher(js);

        for (int i = 0; i < 1000; i++)
        {
            await batch2.AddAsync($"{subject}.1", "message 1"u8.ToArray(), cancellationToken: ct);
        }

        // This should be message 1001 and should fail with exceeds limit error
        await Assert.ThrowsAsync<BatchPublishExceedsLimitException>(
            async () => await batch2.CommitAsync($"{subject}.2", "message 2"u8.ToArray(), cancellationToken: ct));
    }

    [Fact]
    public async Task Flow_control_ack_every()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectAsync();

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var streamName = $"{prefix}TEST";
        var subject = $"{prefix}test";

        var ct = TestContext.Current.CancellationToken;

        // Create a stream with batch publishing enabled
        var stream = await js.CreateStreamAsync(
            new StreamConfig(streamName, [$"{subject}.>"]) { AllowAtomicPublish = true },
            ct);

        // Create batch publisher with flow control
        var batch = new BatchPublisher(
            js,
            new BatchFlowControl
            {
                AckFirst = true,
                AckEvery = 10,
                AckTimeout = TimeSpan.FromSeconds(5),
            });

        // Add messages - should ack on 1st, 10th, 20th, etc
        for (int i = 0; i < 25; i++)
        {
            await batch.AddAsync($"{subject}.{i}", System.Text.Encoding.UTF8.GetBytes($"message {i}"), cancellationToken: ct);
        }

        var ack = await batch.CommitAsync($"{subject}.final", "final"u8.ToArray(), cancellationToken: ct);

        Assert.NotNull(ack);
        Assert.Equal(26, ack.BatchSize);
    }
}
