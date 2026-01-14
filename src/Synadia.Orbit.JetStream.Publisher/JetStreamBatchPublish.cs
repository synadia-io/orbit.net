// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Header constants for batch publishing.
/// </summary>
public static class BatchHeaders
{
    /// <summary>
    /// Contains the batch ID for a message in a batch publish.
    /// </summary>
    public const string BatchId = "Nats-Batch-Id";

    /// <summary>
    /// Contains the sequence number of a message within a batch.
    /// </summary>
    public const string BatchSeq = "Nats-Batch-Sequence";

    /// <summary>
    /// Signals the final message in a batch when set to "1".
    /// </summary>
    public const string BatchCommit = "Nats-Batch-Commit";
}

/// <summary>
/// The acknowledgment for a batch publish operation.
/// </summary>
public record BatchAck
{
    /// <summary>
    /// The stream name the message was published to.
    /// </summary>
    [JsonPropertyName("stream")]
    public string Stream { get; init; } = string.Empty;

    /// <summary>
    /// The stream sequence number of the message.
    /// </summary>
    [JsonPropertyName("seq")]
    public ulong Sequence { get; init; }

    /// <summary>
    /// The domain the message was published to.
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    /// <summary>
    /// The unique identifier for the batch.
    /// </summary>
    [JsonPropertyName("batch")]
    public string BatchId { get; init; } = string.Empty;

    /// <summary>
    /// The number of messages in the batch.
    /// </summary>
    [JsonPropertyName("count")]
    public int BatchSize { get; init; }
}

/// <summary>
/// Configures flow control for batch publishing.
/// </summary>
public record BatchFlowControl
{
    /// <summary>
    /// Waits for an ack on the first message in the batch. Default: true
    /// </summary>
    public bool AckFirst { get; init; } = true;

    /// <summary>
    /// Waits for an ack every N messages (0 = disabled). Default: 0
    /// </summary>
    public int AckEvery { get; init; } = 0;

    /// <summary>
    /// The timeout for waiting for acks when flow control is enabled.
    /// </summary>
    public TimeSpan AckTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Options for configuring individual batch messages.
/// </summary>
public record BatchMsgOpts
{
    /// <summary>
    /// Sets per message TTL for batch messages.
    /// </summary>
    public TimeSpan? Ttl { get; init; }

    /// <summary>
    /// Sets the expected stream the message should be published to.
    /// </summary>
    public string? Stream { get; init; }

    /// <summary>
    /// Sets the expected sequence number the last message on a stream should have.
    /// </summary>
    public ulong? LastSeq { get; init; }

    /// <summary>
    /// Sets the expected sequence number the last message on a subject should have.
    /// </summary>
    public ulong? LastSubjectSeq { get; init; }

    /// <summary>
    /// Sets the subject for which the last sequence number should be checked.
    /// </summary>
    public string? LastSubject { get; init; }
}

/// <summary>
/// Provides methods for publishing messages to a stream in batches.
/// </summary>
public interface IBatchPublisher
{
    /// <summary>
    /// Publishes a message to the batch with the given subject and data.
    /// </summary>
    Task AddAsync(string subject, byte[] data, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the batch.
    /// </summary>
    Task AddMsgAsync(NatsMsg<byte[]> msg, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message with the given subject and data, and commits the batch.
    /// </summary>
    Task<BatchAck> CommitAsync(string subject, byte[] data, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message and commits the batch.
    /// </summary>
    Task<BatchAck> CommitMsgAsync(NatsMsg<byte[]> msg, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the batch without committing.
    /// </summary>
    void Discard();

    /// <summary>
    /// Returns the number of messages added to the batch so far.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Returns true if the batch has been committed or discarded.
    /// </summary>
    bool IsClosed { get; }
}

/// <summary>
/// Implementation of batch publisher for publishing messages to a stream in batches.
/// </summary>
public class BatchPublisher : IBatchPublisher
{
    private readonly INatsJSContext _js;
    private readonly string _batchId;
    private readonly BatchFlowControl _flowControl;
    private readonly object _lock = new();
    private int _sequence;
    private bool _closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchPublisher"/> class.
    /// </summary>
    public BatchPublisher(INatsJSContext js, BatchFlowControl? flowControl = null)
    {
        _js = js;
        _batchId = Nuid.NewNuid();
        _flowControl = flowControl ?? new BatchFlowControl();
    }

    /// <inheritdoc />
    public int Size
    {
        get
        {
            lock (_lock)
            {
                return _sequence;
            }
        }
    }

    /// <inheritdoc />
    public bool IsClosed
    {
        get
        {
            lock (_lock)
            {
                return _closed;
            }
        }
    }

    /// <inheritdoc />
    public Task AddAsync(string subject, byte[] data, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = data,
        };
        return AddMsgAsync(msg, opts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddMsgAsync(NatsMsg<byte[]> msg, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        int currentSeq;
        bool needsAck;

        lock (_lock)
        {
            if (_closed)
            {
                throw new BatchClosedException();
            }

            _sequence++;
            currentSeq = _sequence;

            // Determine if we need flow control for this message
            needsAck = false;
            if (_flowControl.AckFirst && currentSeq == 1)
            {
                needsAck = true; // wait on first message
            }
            else if (_flowControl.AckEvery > 0 && currentSeq % _flowControl.AckEvery == 0)
            {
                needsAck = true; // periodic flow control
            }
        }

        // Prepare headers
        var headers = msg.Headers ?? new NatsHeaders();
        ApplyBatchMessageOptions(headers, opts);

        headers[BatchHeaders.BatchId] = _batchId;
        headers[BatchHeaders.BatchSeq] = currentSeq.ToString();

        var msgToSend = msg with { Headers = headers };

        // If we don't need an ack, use core NATS publish
        if (!needsAck)
        {
            await _js.Connection.PublishAsync(msgToSend.Subject, msgToSend.Data, headers: msgToSend.Headers, cancellationToken: cancellationToken);
            return;
        }

        // Request with ack
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_flowControl.AckTimeout);

        try
        {
            var response = await _js.Connection.RequestAsync<byte[], byte[]>(
                msgToSend.Subject,
                msgToSend.Data,
                headers: msgToSend.Headers,
                cancellationToken: cts.Token);

            // For flow control we expect no response data or an error
            if (response.Data?.Length > 0)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(response.Data);
                if (apiResponse?.Error != null)
                {
                    ThrowBatchPublishException(apiResponse.Error);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Batch message {currentSeq} ack failed: timeout after {_flowControl.AckTimeout}");
        }
        catch (NatsJSException ex)
        {
            throw new Exception($"Batch message {currentSeq} ack failed", ex);
        }
    }

    /// <inheritdoc />
    public Task<BatchAck> CommitAsync(string subject, byte[] data, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = data,
        };
        return CommitMsgAsync(msg, opts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BatchAck> CommitMsgAsync(NatsMsg<byte[]> msg, BatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        int currentSeq;
        string batchId;

        lock (_lock)
        {
            if (_closed)
            {
                throw new BatchClosedException();
            }

            _sequence++;
            currentSeq = _sequence;
            batchId = _batchId;
        }

        // Prepare headers
        var headers = msg.Headers ?? new NatsHeaders();
        ApplyBatchMessageOptions(headers, opts);

        headers[BatchHeaders.BatchId] = batchId;
        headers[BatchHeaders.BatchSeq] = currentSeq.ToString();
        headers[BatchHeaders.BatchCommit] = "1";

        var msgToSend = msg with { Headers = headers };

        // Request with ack
        var response = await _js.Connection.RequestAsync<byte[], byte[]>(
            msgToSend.Subject,
            msgToSend.Data,
            headers: msgToSend.Headers,
            cancellationToken: cancellationToken);

        lock (_lock)
        {
            _closed = true;
        }

        if (response.Data == null || response.Data.Length == 0)
        {
            throw new InvalidBatchAckException();
        }

        var batchResponse = JsonSerializer.Deserialize<BatchAckResponse>(response.Data);

        if (batchResponse?.Error != null)
        {
            ThrowBatchPublishException(batchResponse.Error);
        }

        if (batchResponse?.PubAck == null ||
            string.IsNullOrEmpty(batchResponse.PubAck.Stream) ||
            batchResponse.BatchId != batchId ||
            batchResponse.BatchSize != currentSeq)
        {
            throw new InvalidBatchAckException();
        }

        return new BatchAck
        {
            Stream = batchResponse.PubAck.Stream,
            Sequence = batchResponse.PubAck.Seq,
            Domain = batchResponse.PubAck.Domain,
            BatchId = batchResponse.BatchId!,
            BatchSize = batchResponse.BatchSize,
        };
    }

    /// <inheritdoc />
    public void Discard()
    {
        lock (_lock)
        {
            if (_closed)
            {
                throw new BatchClosedException();
            }

            _closed = true;
        }
    }

    private static void ApplyBatchMessageOptions(NatsHeaders headers, BatchMsgOpts? opts)
    {
        if (opts == null)
            return;

        if (opts.Ttl.HasValue)
        {
            // Convert to nanoseconds (Ticks * 100 since each tick is 100ns)
            headers["Nats-TTL"] = (opts.Ttl.Value.Ticks * 100).ToString();
        }

        if (!string.IsNullOrEmpty(opts.Stream))
        {
            headers["Nats-Expected-Stream"] = opts.Stream;
        }

        if (opts.LastSeq.HasValue)
        {
            headers["Nats-Expected-Last-Sequence"] = opts.LastSeq.Value.ToString();
        }

        if (opts.LastSubjectSeq.HasValue)
        {
            headers["Nats-Expected-Last-Subject-Sequence"] = opts.LastSubjectSeq.Value.ToString();
        }

        if (!string.IsNullOrEmpty(opts.LastSubject))
        {
            headers["Nats-Expected-Last-Subject-Sequence-Subject"] = opts.LastSubject;
            if (opts.LastSubjectSeq.HasValue)
            {
                headers["Nats-Expected-Last-Subject-Sequence"] = opts.LastSubjectSeq.Value.ToString();
            }
        }
    }

    private static void ThrowBatchPublishException(ErrorResponse error)
    {
        switch (error.Code)
        {
            case BatchPublishNotEnabledException.ErrorCode:
                throw new BatchPublishNotEnabledException();
            case BatchPublishIncompleteException.ErrorCode:
                throw new BatchPublishIncompleteException();
            case BatchPublishMissingSeqException.ErrorCode:
                throw new BatchPublishMissingSeqException();
            case BatchPublishUnsupportedHeaderException.ErrorCode:
                throw new BatchPublishUnsupportedHeaderException();
            case BatchPublishExceedsLimitException.ErrorCode:
                throw new BatchPublishExceedsLimitException();
            default:
                throw new NatsJSException($"Batch publish error: {error.Description}");
        }
    }

    private record ApiResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("error")]
        public ErrorResponse? Error { get; init; }
    }

    private record BatchAckResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("error")]
        public ErrorResponse? Error { get; init; }

        [JsonPropertyName("stream")]
        public string? Stream { get; init; }

        [JsonPropertyName("seq")]
        public ulong Seq { get; init; }

        [JsonPropertyName("domain")]
        public string? Domain { get; init; }

        [JsonPropertyName("batch")]
        public string? BatchId { get; init; }

        [JsonPropertyName("count")]
        public int BatchSize { get; init; }

        public PubAckResponse? PubAck => string.IsNullOrEmpty(Stream) ? null : new PubAckResponse
        {
            Stream = Stream,
            Seq = Seq,
            Domain = Domain,
        };
    }

    private record ErrorResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("err_code")]
        public int ErrCode { get; init; }
    }
}

/// <summary>
/// Static helper methods for batch publishing.
/// </summary>
public static class JetStreamBatchPublish
{
    /// <summary>
    /// Publishes a batch of messages to a Stream and waits for an ack for the commit.
    /// </summary>
    public static async Task<BatchAck> PublishMsgBatchAsync(
        INatsJSContext js,
        NatsMsg<byte[]>[] messages,
        BatchFlowControl? flowControl = null,
        CancellationToken cancellationToken = default)
    {
        if (messages.Length == 0)
        {
            throw new ArgumentException("No messages to publish", nameof(messages));
        }

        var batchId = Nuid.NewNuid();
        var fc = flowControl ?? new BatchFlowControl();

        for (int i = 0; i < messages.Length; i++)
        {
            var msg = messages[i];
            var headers = msg.Headers ?? new NatsHeaders();

            headers.Remove(BatchHeaders.BatchCommit);
            headers[BatchHeaders.BatchId] = batchId;
            headers[BatchHeaders.BatchSeq] = (i + 1).ToString();

            var msgToSend = msg with { Headers = headers };

            // Add all but last message to the batch
            if (i < messages.Length - 1)
            {
                // Determine if we need flow control for this message
                bool needsAck = false;
                int seq = i + 1;
                if (fc.AckFirst && seq == 1)
                {
                    needsAck = true;
                }
                else if (fc.AckEvery > 0 && seq % fc.AckEvery == 0)
                {
                    needsAck = true;
                }

                if (!needsAck)
                {
                    await js.Connection.PublishAsync(msgToSend.Subject, msgToSend.Data, headers: msgToSend.Headers, cancellationToken: cancellationToken);
                    continue;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(fc.AckTimeout);

                try
                {
                    var response = await js.Connection.RequestAsync<byte[], byte[]>(
                        msgToSend.Subject,
                        msgToSend.Data,
                        headers: msgToSend.Headers,
                        cancellationToken: cts.Token);

                    if (response.Data?.Length > 0)
                    {
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse>(response.Data);
                        if (apiResponse?.Error != null)
                        {
                            ThrowBatchPublishException(apiResponse.Error);
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Batch message {seq} ack failed: timeout after {fc.AckTimeout}");
                }

                continue;
            }

            // Commit the batch on the last message
            headers[BatchHeaders.BatchCommit] = "1";
            var commitMsg = msgToSend with { Headers = headers };

            var commitResponse = await js.Connection.RequestAsync<byte[], byte[]>(
                commitMsg.Subject,
                commitMsg.Data,
                headers: commitMsg.Headers,
                cancellationToken: cancellationToken);

            if (commitResponse.Data == null || commitResponse.Data.Length == 0)
            {
                throw new InvalidBatchAckException();
            }

            var batchResponse = JsonSerializer.Deserialize<BatchAckResponse>(commitResponse.Data);

            if (batchResponse?.Error != null)
            {
                ThrowBatchPublishException(batchResponse.Error);
            }

            if (batchResponse?.PubAck == null ||
                string.IsNullOrEmpty(batchResponse.PubAck.Stream) ||
                batchResponse.BatchId != batchId ||
                batchResponse.BatchSize != messages.Length)
            {
                throw new InvalidBatchAckException();
            }

            return new BatchAck
            {
                Stream = batchResponse.PubAck.Stream,
                Sequence = batchResponse.PubAck.Seq,
                Domain = batchResponse.PubAck.Domain,
                BatchId = batchResponse.BatchId!,
                BatchSize = batchResponse.BatchSize,
            };
        }

        throw new InvalidOperationException("Unreachable code");
    }

    private static void ThrowBatchPublishException(ErrorResponse error)
    {
        switch (error.Code)
        {
            case BatchPublishNotEnabledException.ErrorCode:
                throw new BatchPublishNotEnabledException();
            case BatchPublishIncompleteException.ErrorCode:
                throw new BatchPublishIncompleteException();
            case BatchPublishMissingSeqException.ErrorCode:
                throw new BatchPublishMissingSeqException();
            case BatchPublishUnsupportedHeaderException.ErrorCode:
                throw new BatchPublishUnsupportedHeaderException();
            case BatchPublishExceedsLimitException.ErrorCode:
                throw new BatchPublishExceedsLimitException();
            default:
                throw new NatsJSException($"Batch publish error: {error.Description}");
        }
    }

    private record ApiResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("error")]
        public ErrorResponse? Error { get; init; }
    }

    private record BatchAckResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("error")]
        public ErrorResponse? Error { get; init; }

        [JsonPropertyName("stream")]
        public string? Stream { get; init; }

        [JsonPropertyName("seq")]
        public ulong Seq { get; init; }

        [JsonPropertyName("domain")]
        public string? Domain { get; init; }

        [JsonPropertyName("batch")]
        public string? BatchId { get; init; }

        [JsonPropertyName("count")]
        public int BatchSize { get; init; }

        public PubAckResponse? PubAck => string.IsNullOrEmpty(Stream) ? null : new PubAckResponse
        {
            Stream = Stream,
            Seq = Seq,
            Domain = Domain,
        };
    }

    private record ErrorResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("err_code")]
        public int ErrCode { get; init; }
    }
}
