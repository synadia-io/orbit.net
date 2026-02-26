// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Implementation of batch publisher for publishing messages to a stream in batches.
/// </summary>
public class NatsJSBatchPublisher : INatsJSBatchPublisher
{
    private readonly INatsJSContext _js;
    private readonly string _batchId;
    private readonly NatsJSBatchFlowControl _flowControl;
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
    public Task AddAsync(string subject, byte[] data, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = data,
        };
        return AddMsgAsync(msg, opts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddMsgAsync(NatsMsg<byte[]> msg, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
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
        BatchPublishHelper.ApplyBatchMessageOptions(headers, opts);

        headers[NatsJSBatchHeaders.BatchId] = _batchId;
        headers[NatsJSBatchHeaders.BatchSeq] = currentSeq.ToString();

        var msgToSend = msg with { Headers = headers };

        // If we don't need an ack, use core NATS publish
        if (!needsAck)
        {
            await _js.Connection.PublishAsync(msgToSend.Subject, msgToSend.Data, headers: msgToSend.Headers, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        // Request with ack
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_flowControl.AckTimeout);

        NatsMsg<byte[]> response;
        try
        {
            response = await _js.Connection.RequestAsync<byte[], byte[]>(
                msgToSend.Subject,
                msgToSend.Data,
                headers: msgToSend.Headers,
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Batch message {currentSeq} ack failed: timeout after {_flowControl.AckTimeout}");
        }

        // For flow control we expect no response data or an error
        if (response.Data?.Length > 0)
        {
            var apiResponse = BatchPublishHelper.DeserializeApiResponse(response.Data);
            if (apiResponse?.Error != null)
            {
                BatchPublishHelper.ThrowBatchPublishException(apiResponse.Error);
            }
        }
    }

    /// <inheritdoc />
    public Task<NatsJSBatchAck> CommitAsync(string subject, byte[] data, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = data,
        };
        return CommitMsgAsync(msg, opts, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NatsJSBatchAck> CommitMsgAsync(NatsMsg<byte[]> msg, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default)
    {
        int currentSeq;
        string batchId;

        lock (_lock)
        {
            if (_closed)
            {
                throw new NatsJSBatchClosedException();
            }

            _sequence++;
            currentSeq = _sequence;
            batchId = _batchId;
        }

        // Prepare headers
        var headers = msg.Headers ?? new NatsHeaders();
        BatchPublishHelper.ApplyBatchMessageOptions(headers, opts);

        headers[NatsJSBatchHeaders.BatchId] = batchId;
        headers[NatsJSBatchHeaders.BatchSeq] = currentSeq.ToString();
        headers[NatsJSBatchHeaders.BatchCommit] = "1";

        var msgToSend = msg with { Headers = headers };

        // Apply default timeout if no cancellation is set, matching Go's wrapContextWithoutDeadline behavior.
        using var cts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, _js.Opts.RequestTimeout);
        var effectiveToken = cts?.Token ?? cancellationToken;

        // Request with ack
        var response = await _js.Connection.RequestAsync<byte[], byte[]>(
            msgToSend.Subject,
            msgToSend.Data,
            headers: msgToSend.Headers,
            cancellationToken: effectiveToken).ConfigureAwait(false);

        lock (_lock)
        {
            _closed = true;
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

    /// <inheritdoc />
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
}
