// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Implementation of batch publisher for publishing messages to a stream in batches.
/// </summary>
/// <remarks>
/// This class is not thread-safe. <c>AddAsync</c> and <c>AddMsgAsync</c> must be called
/// sequentially by a single producer: concurrent calls can reach the socket in an order
/// different from the one in which sequence numbers were assigned, and the server will reject
/// out-of-order sequences.
/// </remarks>
public sealed class NatsJSBatchPublisher : INatsJSBatchPublisher
{
    private readonly INatsJSContext _js;
    private readonly string _batchId;
    private readonly NatsJSBatchFlowControl _flowControl;
    private readonly TimeSpan _ackTimeout;
    private readonly object _lock = new();
    private int _sequence;
    private bool _closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSBatchPublisher"/> class.
    /// </summary>
    /// <param name="js">The JetStream context to use for publishing.</param>
    /// <param name="flowControl">Optional flow control configuration.</param>
    public NatsJSBatchPublisher(INatsJSContext js, NatsJSBatchFlowControl? flowControl = null)
    {
        _js = js;
        _batchId = Nuid.NewNuid();
        _flowControl = flowControl ?? new NatsJSBatchFlowControl();
        _ackTimeout = _flowControl.AckTimeout ?? js.Opts.RequestTimeout;
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
    public Task AddAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<T>
        {
            Subject = subject,
            Data = data,
        };
        return AddMsgInternalAsync(msg, opts, serializer, cancellationToken);
    }

    /// <inheritdoc />
    public Task AddMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
        => AddMsgInternalAsync(msg, opts, serializer, cancellationToken);

    /// <inheritdoc />
    public Task<NatsJSBatchAck> CommitAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<T>
        {
            Subject = subject,
            Data = data,
        };
        return CommitMsgInternalAsync(msg, opts, serializer, cancellationToken);
    }

    /// <inheritdoc />
    public Task<NatsJSBatchAck> CommitMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
        => CommitMsgInternalAsync(msg, opts, serializer, cancellationToken);

    /// <inheritdoc />
    public void Discard()
    {
        lock (_lock)
        {
            if (_closed)
            {
                throw new NatsJSBatchClosedException();
            }

            _closed = true;
        }
    }

    /// <summary>
    /// Closes the batch publisher locally. Equivalent to <see cref="Discard"/> when called on an
    /// uncommitted batch: messages already sent with <c>AddAsync</c> or <c>AddMsgAsync</c>
    /// remain as an incomplete batch on the server until the server's batch timeout expires and
    /// the in-progress messages are garbage collected. To finalize a batch explicitly, call
    /// <c>CommitAsync</c> or <c>CommitMsgAsync</c> before disposal.
    /// </summary>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (!_closed)
            {
                _closed = true;
            }
        }

        return default;
    }

    private async Task AddMsgInternalAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts, INatsSerialize<T>? serializer, CancellationToken cancellationToken)
    {
        // Capture any IDisposable payload up front so we can dispose it on a pre-publish throw
        // (closed batch, invalid opts). The serializer disposes it once the bytes are written.
        var owned = msg.Data as IDisposable;
        try
        {
            // Apply opts before taking _sequence so a thrown ArgumentException doesn't leave a gap.
            var headers = BatchPublishHelper.CloneHeaders(msg.Headers);
            BatchPublishHelper.ApplyBatchMessageOptions(headers, opts);

            int currentSeq;
            bool needsAck;

            lock (_lock)
            {
                if (_closed)
                {
                    throw new NatsJSBatchClosedException();
                }

                _sequence++;
                currentSeq = _sequence;

                needsAck = (_flowControl.AckFirst && currentSeq == 1)
                    || (_flowControl.AckEvery > 0 && currentSeq % _flowControl.AckEvery == 0);
            }

            headers[NatsJSBatchHeaders.BatchId] = _batchId;
            headers[NatsJSBatchHeaders.BatchSeq] = currentSeq.ToString();

            var msgToSend = msg with { Headers = headers };

            if (!needsAck)
            {
                owned = null;
                await _js.Connection.PublishAsync<T>(msgToSend.Subject, msgToSend.Data!, headers: msgToSend.Headers, serializer: serializer, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }

            // Only allocate a linked CTS when the caller actually has a cancellable token.
            using var cts = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();
            cts.CancelAfter(_ackTimeout);

            NatsMsg<byte[]> response;
            try
            {
                owned = null;
                response = await _js.Connection.RequestAsync<T, byte[]>(
                    msgToSend.Subject,
                    msgToSend.Data,
                    headers: msgToSend.Headers,
                    requestSerializer: serializer,
                    cancellationToken: cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Batch is dead on the server after an ack timeout; close locally so further
                // Add/Commit calls fail fast.
                CloseOnError();
                throw new TimeoutException($"Batch message {currentSeq} ack failed: timeout after {_ackTimeout}");
            }

            if (response.Data?.Length > 0)
            {
                BatchPublishApiResponse? apiResponse;
                try
                {
                    apiResponse = BatchPublishHelper.DeserializeApiResponse(response.Data);
                }
                catch (System.Text.Json.JsonException)
                {
                    // Message was sent and the sequence advanced; a malformed response leaves
                    // the batch unrecoverable.
                    CloseOnError();
                    throw;
                }

                if (apiResponse?.Error != null)
                {
                    // Close locally so further Add/Commit calls fail fast instead of silently
                    // targeting a server-rejected batch.
                    CloseOnError();
                    BatchPublishHelper.ThrowBatchPublishException(apiResponse.Error);
                }
            }
        }
        catch
        {
            owned?.Dispose();
            throw;
        }
    }

    private async Task<NatsJSBatchAck> CommitMsgInternalAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts, INatsSerialize<T>? serializer, CancellationToken cancellationToken)
    {
        var owned = msg.Data as IDisposable;
        try
        {
            // Apply opts before taking _sequence so a thrown ArgumentException doesn't close the batch.
            var headers = BatchPublishHelper.CloneHeaders(msg.Headers);
            BatchPublishHelper.ApplyBatchMessageOptions(headers, opts);

            int currentSeq;
            string batchId;

            lock (_lock)
            {
                if (_closed)
                {
                    throw new NatsJSBatchClosedException();
                }

                // Close up-front so concurrent commits can't both send.
                _closed = true;
                _sequence++;
                currentSeq = _sequence;
                batchId = _batchId;
            }

            headers[NatsJSBatchHeaders.BatchId] = batchId;
            headers[NatsJSBatchHeaders.BatchSeq] = currentSeq.ToString();
            headers[NatsJSBatchHeaders.BatchCommit] = "1";

            var msgToSend = msg with { Headers = headers };

            // Always bound the commit wait so a non-responsive server can't block indefinitely,
            // even when the caller supplies a cancellation token.
            using var cts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, _ackTimeout);

            NatsMsg<byte[]> response;
            try
            {
                owned = null;
                response = await _js.Connection.RequestAsync<T, byte[]>(
                    msgToSend.Subject,
                    msgToSend.Data,
                    headers: msgToSend.Headers,
                    requestSerializer: serializer,
                    cancellationToken: cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Batch commit ack failed: timeout after {_ackTimeout}");
            }

            var batchResponse = BatchPublishHelper.DeserializeAckResponse(response.Data);

            if (batchResponse?.Error != null)
            {
                BatchPublishHelper.ThrowBatchPublishException(batchResponse.Error);
            }

            if (batchResponse == null ||
                string.IsNullOrEmpty(batchResponse.Stream) ||
                batchResponse.BatchId != batchId ||
                batchResponse.BatchSize != currentSeq)
            {
                throw new NatsJSInvalidBatchAckException();
            }

            return new NatsJSBatchAck
            {
                Stream = batchResponse.Stream!,
                Sequence = batchResponse.Seq,
                Domain = batchResponse.Domain,
                Value = batchResponse.Value,
                BatchId = batchResponse.BatchId!,
                BatchSize = batchResponse.BatchSize,
            };
        }
        catch
        {
            owned?.Dispose();
            throw;
        }
    }

    private void CloseOnError()
    {
        lock (_lock)
        {
            _closed = true;
        }
    }
}
