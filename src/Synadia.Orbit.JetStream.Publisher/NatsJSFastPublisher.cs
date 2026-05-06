// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Implementation of <see cref="INatsJSFastPublisher"/> for fast-ingest batch publishing per ADR-50.
/// </summary>
public sealed class NatsJSFastPublisher : INatsJSFastPublisher
{
    private const int OpStart = 0;
    private const int OpAddMsg = 1;
    private const int OpCommitMsg = 2;
    private const int OpCommitEob = 3;
    private const int OpPing = 4;

    private const ushort DefaultFlow = 100;
    private const ushort DefaultMaxOutstandingAcks = 2;

    private static readonly byte[] TypeAckMarker = "\"type\":\"ack\""u8.ToArray();
    private static readonly byte[] TypeGapMarker = "\"type\":\"gap\""u8.ToArray();
    private static readonly byte[] TypeErrMarker = "\"type\":\"err\""u8.ToArray();

    private readonly INatsJSContext _js;
    private readonly INatsConnection _conn;
    private readonly NatsJSFastPublisherOpts _opts;
    private readonly ushort _maxOutstandingAcks;
    private readonly TimeSpan _ackTimeout;
    private readonly Action<Exception>? _errorHandler;
    private readonly string _ackInboxPrefix;
    private readonly string _replyPrefix;

    private readonly object _lock = new();

    private ushort _flow;
    private long _sequence;
    private long _ackSequence;
    private bool _closed;
    private string? _batchSubject;

    private INatsSub<byte[]>? _ackSub;
    private CancellationTokenSource? _ackSubCts;
    private Task? _ackReaderTask;

    private TaskCompletionSource<FastPublishFlowAckResponse>? _firstAckTcs;
    private TaskCompletionSource<BatchPublishAckResponse>? _commitTcs;
    private TaskCompletionSource<bool>? _stallTcs;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSFastPublisher"/> class.
    /// </summary>
    /// <param name="js">The JetStream context.</param>
    /// <param name="opts">Optional publisher options.</param>
    public NatsJSFastPublisher(INatsJSContext js, NatsJSFastPublisherOpts? opts = null)
    {
        _js = js;
        _conn = js.Connection;
        _opts = opts ?? new NatsJSFastPublisherOpts();
        var fc = _opts.FlowControl ?? new NatsJSFastPublishFlowControl();
        _flow = fc.Flow > 0 ? fc.Flow : DefaultFlow;
        _maxOutstandingAcks = fc.MaxOutstandingAcks > 0 ? fc.MaxOutstandingAcks : DefaultMaxOutstandingAcks;
        _ackTimeout = fc.AckTimeout ?? js.Opts.RequestTimeout;
        _errorHandler = _opts.ErrorHandler;

        // Use a non-_INBOX prefix because nats.net dispatches inbox subjects via an exact-match
        // map (with a connection-level _INBOX.<connId>.* wildcard), which would not deliver the
        // multi-token fast-ingest reply subjects.
        _ackInboxPrefix = "_FB." + Nuid.NewNuid();

        // The flow and gap tokens in the reply subject are read by the server only when the
        // batch is first created (on the seq=1 message). They're fixed for the lifetime of the
        // batch from the server's perspective, even though we may track a different current
        // ack frequency in _flow as the server adjusts it.
        var gap = _opts.ContinueOnGap ? "ok" : "fail";
        _replyPrefix = _ackInboxPrefix + "." + _flow.ToString(CultureInfo.InvariantCulture) + "." + gap + ".";
    }

    /// <inheritdoc />
    public long Size
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
    public long AckSequence
    {
        get
        {
            lock (_lock)
            {
                return _ackSequence;
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
    public Task<NatsJSFastPubAck> AddAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<T> { Subject = subject, Data = data };
        return AddMsgInternalAsync(msg, opts, serializer, cancellationToken);
    }

    /// <inheritdoc />
    public Task<NatsJSFastPubAck> AddMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
        => AddMsgInternalAsync(msg, opts, serializer, cancellationToken);

    /// <inheritdoc />
    public Task<NatsJSBatchAck> CommitAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        var msg = new NatsMsg<T> { Subject = subject, Data = data };
        return CommitMsgInternalAsync(msg, opts, serializer, eob: false, cancellationToken);
    }

    /// <inheritdoc />
    public Task<NatsJSBatchAck> CommitMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
        => CommitMsgInternalAsync(msg, opts, serializer, eob: false, cancellationToken);

    /// <inheritdoc />
    public Task<NatsJSBatchAck> CloseAsync(CancellationToken cancellationToken = default)
    {
        string subject;
        lock (_lock)
        {
            if (_closed)
            {
                throw new NatsJSBatchClosedException();
            }

            if (_sequence == 0 || _batchSubject == null)
            {
                throw new InvalidOperationException("Cannot close an empty batch");
            }

            subject = _batchSubject;
        }

        var msg = new NatsMsg<byte[]> { Subject = subject };
        return CommitMsgInternalAsync<byte[]>(msg, opts: null, serializer: null, eob: true, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        TaskCompletionSource<FastPublishFlowAckResponse>? firstTcs;
        TaskCompletionSource<BatchPublishAckResponse>? commitTcs;
        TaskCompletionSource<bool>? stallTcs;
        CancellationTokenSource? cts;
        INatsSub<byte[]>? sub;
        Task? readerTask;

        lock (_lock)
        {
            _closed = true;
            firstTcs = _firstAckTcs;
            commitTcs = _commitTcs;
            stallTcs = _stallTcs;
            cts = _ackSubCts;
            sub = _ackSub;
            readerTask = _ackReaderTask;
            _firstAckTcs = null;
            _commitTcs = null;
            _stallTcs = null;
            _ackSub = null;
            _ackSubCts = null;
            _ackReaderTask = null;
        }

        firstTcs?.TrySetCanceled();
        commitTcs?.TrySetCanceled();
        stallTcs?.TrySetResult(true);

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (sub != null)
        {
            try
            {
                await sub.DisposeAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (readerTask != null)
        {
            try
            {
                await readerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts?.Dispose();
    }

    private string BuildReplySubject(long seq, int operation)
        => _replyPrefix + seq.ToString(CultureInfo.InvariantCulture) + "." + operation.ToString(CultureInfo.InvariantCulture) + ".$FI";

    private async Task<NatsJSFastPubAck> AddMsgInternalAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts, INatsSerialize<T>? serializer, CancellationToken cancellationToken)
    {
        var owned = msg.Data as IDisposable;
        try
        {
            NatsHeaders headers;
            long seq;
            int operation;
            string reply;
            bool isFirst;
            TaskCompletionSource<FastPublishFlowAckResponse>? firstTcs = null;
            TaskCompletionSource<bool>? stallTcs = null;
            bool waitForAck;

            lock (_lock)
            {
                if (_closed)
                {
                    throw new NatsJSBatchClosedException();
                }

                headers = BatchPublishHelper.CloneAndApplyMsgOpts(msg.Headers, opts);

                _sequence++;
                seq = _sequence;
                isFirst = seq == 1;
                operation = isFirst ? OpStart : OpAddMsg;
                reply = BuildReplySubject(seq, operation);

                if (isFirst)
                {
                    firstTcs = new TaskCompletionSource<FastPublishFlowAckResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _firstAckTcs = firstTcs;
                    _batchSubject = msg.Subject;
                }

                waitForAck = !isFirst && (long)_ackSequence + ((long)_flow * _maxOutstandingAcks) < seq;
                if (waitForAck)
                {
                    _stallTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    stallTcs = _stallTcs;
                }
            }

            if (isFirst)
            {
                await EnsureAckSubscriptionAsync(cancellationToken).ConfigureAwait(false);
            }

            if (waitForAck && stallTcs != null)
            {
                await WaitForStallAsync(stallTcs, cancellationToken).ConfigureAwait(false);
            }

            var msgToSend = msg with { Headers = headers, ReplyTo = reply };

            try
            {
                owned = null;
                await _conn.PublishAsync(msgToSend, serializer, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (isFirst)
                {
                    CloseOnError();
                }

                throw;
            }

            if (isFirst && firstTcs != null)
            {
                FastPublishFlowAckResponse firstAck;
                using var cts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, _ackTimeout);

                using (cts.Token.Register(static state => ((TaskCompletionSource<FastPublishFlowAckResponse>)state!).TrySetCanceled(), firstTcs))
                {
                    try
                    {
                        firstAck = await firstTcs.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        CloseOnError();
                        throw new TimeoutException($"Fast batch first message ack timeout after {_ackTimeout}");
                    }
                    catch (OperationCanceledException)
                    {
                        CloseOnError();
                        throw;
                    }
                }

                long ackSeqLocal;
                lock (_lock)
                {
                    _firstAckTcs = null;
                    if (firstAck.Messages != 0)
                    {
                        _flow = firstAck.Messages;
                    }

                    _ackSequence = (long)firstAck.Seq;
                    ackSeqLocal = _ackSequence;
                }

                return new NatsJSFastPubAck { BatchSequence = seq, AckSequence = ackSeqLocal };
            }

            long currentAckSeq;
            lock (_lock)
            {
                currentAckSeq = _ackSequence;
            }

            return new NatsJSFastPubAck { BatchSequence = seq, AckSequence = currentAckSeq };
        }
        catch
        {
            owned?.Dispose();
            throw;
        }
    }

    private async Task<NatsJSBatchAck> CommitMsgInternalAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts, INatsSerialize<T>? serializer, bool eob, CancellationToken cancellationToken)
    {
        var owned = msg.Data as IDisposable;
        try
        {
            NatsHeaders headers;
            long seq;
            int operation;
            string reply;
            bool needsSubscription;
            TaskCompletionSource<BatchPublishAckResponse> commitTcs;
            TaskCompletionSource<bool>? stallTcs = null;
            bool waitForAck;

            lock (_lock)
            {
                if (_closed)
                {
                    throw new NatsJSBatchClosedException();
                }

                if (_sequence == 0)
                {
                    throw new InvalidOperationException("Cannot commit an empty batch; call AddAsync at least once first.");
                }

                headers = BatchPublishHelper.CloneAndApplyMsgOpts(msg.Headers, opts);
                _sequence++;
                seq = _sequence;
                operation = eob ? OpCommitEob : OpCommitMsg;
                reply = BuildReplySubject(seq, operation);

                commitTcs = new TaskCompletionSource<BatchPublishAckResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                _commitTcs = commitTcs;

                needsSubscription = _ackSub == null;

                waitForAck = (long)_ackSequence + ((long)_flow * _maxOutstandingAcks) < seq;
                if (waitForAck)
                {
                    _stallTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    stallTcs = _stallTcs;
                }
            }

            if (needsSubscription)
            {
                await EnsureAckSubscriptionAsync(cancellationToken).ConfigureAwait(false);
            }

            if (waitForAck && stallTcs != null)
            {
                await WaitForStallAsync(stallTcs, cancellationToken).ConfigureAwait(false);
            }

            var msgToSend = msg with { Headers = headers, ReplyTo = reply };

            try
            {
                owned = null;
                await _conn.PublishAsync(msgToSend, serializer, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                CloseOnError();
                throw;
            }

            using var cts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, _ackTimeout);

            BatchPublishAckResponse commitAck;
            using (cts.Token.Register(static state => ((TaskCompletionSource<BatchPublishAckResponse>)state!).TrySetCanceled(), commitTcs))
            {
                try
                {
                    commitAck = await commitTcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    CloseOnError();
                    throw new TimeoutException($"Fast batch commit ack timeout after {_ackTimeout}");
                }
                catch (OperationCanceledException)
                {
                    CloseOnError();
                    throw;
                }
            }

            CloseOnError();

            if (commitAck.Error != null)
            {
                throw BatchPublishHelper.FastPublishExceptionFor(commitAck.Error);
            }

            if (string.IsNullOrEmpty(commitAck.Stream))
            {
                throw new NatsJSInvalidBatchAckException();
            }

            return new NatsJSBatchAck
            {
                Stream = commitAck.Stream!,
                Sequence = commitAck.Seq,
                Domain = commitAck.Domain,
                Value = commitAck.Value,
                BatchId = commitAck.BatchId ?? string.Empty,
                BatchSize = commitAck.BatchSize,
            };
        }
        catch
        {
            owned?.Dispose();
            throw;
        }
    }

    private async Task EnsureAckSubscriptionAsync(CancellationToken cancellationToken)
    {
        INatsSub<byte[]> sub;
        CancellationTokenSource cts;

        lock (_lock)
        {
            if (_ackSub != null)
            {
                return;
            }

            cts = new CancellationTokenSource();
            _ackSubCts = cts;
        }

        try
        {
            sub = await _conn.SubscribeCoreAsync<byte[]>(_ackInboxPrefix + ".>", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_lock)
            {
                _ackSubCts = null;
            }

            cts.Dispose();
            throw;
        }

        Task readerTask;
        lock (_lock)
        {
            _ackSub = sub;
            readerTask = Task.Run(() => AckReadLoopAsync(sub, cts.Token));
            _ackReaderTask = readerTask;
        }
    }

    private async Task AckReadLoopAsync(INatsSub<byte[]> sub, CancellationToken cancellationToken)
    {
        try
        {
            while (await sub.Msgs.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (sub.Msgs.TryRead(out var ack))
                {
                    HandleAck(ack);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            try
            {
                _errorHandler?.Invoke(ex);
            }
            catch
            {
            }
        }
    }

    private void HandleAck(NatsMsg<byte[]> ack)
    {
        var data = ack.Data;
        if (data == null || data.Length == 0)
        {
            return;
        }

        var span = new ReadOnlySpan<byte>(data);

        if (span.IndexOf(new ReadOnlySpan<byte>(TypeGapMarker)) >= 0)
        {
            HandleGap(data);
            return;
        }

        if (span.IndexOf(new ReadOnlySpan<byte>(TypeAckMarker)) >= 0)
        {
            HandleFlowAck(data);
            return;
        }

        if (span.IndexOf(new ReadOnlySpan<byte>(TypeErrMarker)) >= 0)
        {
            HandleErr(data);
            return;
        }

        HandleCommitAck(data);
    }

    private void HandleGap(byte[] data)
    {
        FastPublishGapResponse? gap;
        try
        {
#if NET8_0_OR_GREATER
            gap = JsonSerializer.Deserialize(data, FastPublishJsonSerializerContext.Default.FastPublishGapResponse);
#else
            gap = JsonSerializer.Deserialize<FastPublishGapResponse>(data);
#endif
        }
        catch (Exception ex)
        {
            InvokeErrorHandler(ex);
            return;
        }

        if (gap == null)
        {
            return;
        }

        InvokeErrorHandler(new NatsJSFastPublishGapException((long)gap.LastSeq, (long)gap.Seq));
    }

    private void HandleFlowAck(byte[] data)
    {
        FastPublishFlowAckResponse? flow;
        try
        {
#if NET8_0_OR_GREATER
            flow = JsonSerializer.Deserialize(data, FastPublishJsonSerializerContext.Default.FastPublishFlowAckResponse);
#else
            flow = JsonSerializer.Deserialize<FastPublishFlowAckResponse>(data);
#endif
        }
        catch (Exception ex)
        {
            InvokeErrorHandler(ex);
            return;
        }

        if (flow == null)
        {
            return;
        }

        TaskCompletionSource<FastPublishFlowAckResponse>? firstTcs = null;
        TaskCompletionSource<bool>? stallTcs = null;

        lock (_lock)
        {
            if (_firstAckTcs != null)
            {
                firstTcs = _firstAckTcs;
                _firstAckTcs = null;
                firstTcs.TrySetResult(flow);
                return;
            }

            if (flow.Messages != 0)
            {
                _flow = flow.Messages;
            }

            _ackSequence = (long)flow.Seq;

            if (_stallTcs != null)
            {
                stallTcs = _stallTcs;
                _stallTcs = null;
            }
        }

        stallTcs?.TrySetResult(true);
    }

    private void HandleErr(byte[] data)
    {
        FastPublishErrResponse? err;
        try
        {
#if NET8_0_OR_GREATER
            err = JsonSerializer.Deserialize(data, FastPublishJsonSerializerContext.Default.FastPublishErrResponse);
#else
            err = JsonSerializer.Deserialize<FastPublishErrResponse>(data);
#endif
        }
        catch (Exception ex)
        {
            InvokeErrorHandler(ex);
            return;
        }

        if (err?.Error == null)
        {
            return;
        }

        var apiError = new ApiError { Code = err.Error.Code, ErrCode = err.Error.ErrCode, Description = err.Error.Description };
        InvokeErrorHandler(new NatsJSFastPublishMessageException((long)err.Seq, apiError));
    }

    private void HandleCommitAck(byte[] data)
    {
        BatchPublishAckResponse? commitAck;
        try
        {
            commitAck = BatchPublishHelper.DeserializeAckResponse(data);
        }
        catch (Exception ex)
        {
            InvokeErrorHandler(ex);
            return;
        }

        if (commitAck == null)
        {
            return;
        }

        TaskCompletionSource<BatchPublishAckResponse>? commitTcs;
        lock (_lock)
        {
            commitTcs = _commitTcs;
            _commitTcs = null;
            _closed = true;
        }

        commitTcs?.TrySetResult(commitAck);
    }

    private async Task WaitForStallAsync(TaskCompletionSource<bool> stallTcs, CancellationToken cancellationToken)
    {
        var pingInterval = TimeSpan.FromMilliseconds(Math.Max(1, _ackTimeout.TotalMilliseconds / 3));

        using var cts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, _ackTimeout);

        using (cts.Token.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), stallTcs))
        {
            while (true)
            {
                var pingDelay = Task.Delay(pingInterval, cts.Token);
                var completed = await Task.WhenAny(stallTcs.Task, pingDelay).ConfigureAwait(false);
                if (completed == stallTcs.Task)
                {
                    try
                    {
                        await stallTcs.Task.ConfigureAwait(false);
                        return;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        CloseOnError();
                        throw new TimeoutException($"Fast batch ack timeout after {_ackTimeout}");
                    }
                }

                if (cts.IsCancellationRequested)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    CloseOnError();
                    throw new TimeoutException($"Fast batch ack timeout after {_ackTimeout}");
                }

                await SendPingAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendPingAsync(CancellationToken cancellationToken)
    {
        long seq;
        string? subject;
        lock (_lock)
        {
            seq = _sequence;
            subject = _batchSubject;
        }

        if (subject == null)
        {
            return;
        }

        var reply = BuildReplySubject(seq, OpPing);
        var ping = new NatsMsg<byte[]> { Subject = subject, ReplyTo = reply };
        try
        {
            await _conn.PublishAsync(ping, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            InvokeErrorHandler(ex);
        }
    }

    private void CloseOnError()
    {
        TaskCompletionSource<FastPublishFlowAckResponse>? firstTcs;
        TaskCompletionSource<BatchPublishAckResponse>? commitTcs;
        TaskCompletionSource<bool>? stallTcs;

        lock (_lock)
        {
            _closed = true;
            firstTcs = _firstAckTcs;
            commitTcs = _commitTcs;
            stallTcs = _stallTcs;
            _firstAckTcs = null;
            _commitTcs = null;
            _stallTcs = null;
        }

        firstTcs?.TrySetCanceled();
        commitTcs?.TrySetCanceled();
        stallTcs?.TrySetCanceled();
    }

    private void InvokeErrorHandler(Exception ex)
    {
        if (_errorHandler == null)
        {
            return;
        }

        try
        {
            _errorHandler(ex);
        }
        catch
        {
        }
    }
}
