// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Provides methods for fast-ingest batch publishing to a stream (ADR-50).
/// </summary>
/// <remarks>
/// <para>
/// Fast-ingest delivers high-throughput batch publishing without atomicity. Unlike
/// <see cref="INatsJSBatchPublisher"/>, individual messages may be lost (gaps) and a partial
/// batch may persist. The server enforces flow control: <c>AddAsync</c>/<c>AddMsgAsync</c>
/// stall until acks arrive when the configured <see cref="NatsJSFastPublishFlowControl.MaxOutstandingAcks"/>
/// window is exceeded.
/// </para>
/// <para>
/// This type is NOT thread-safe. <c>AddAsync</c>, <c>AddMsgAsync</c>, <c>CommitAsync</c>,
/// <c>CommitMsgAsync</c>, and <c>CloseAsync</c> must be called sequentially by a single
/// producer.
/// </para>
/// <para>
/// Disposing without <c>CommitAsync</c>/<c>CommitMsgAsync</c>/<c>CloseAsync</c> closes the
/// publisher locally; the server's batch timeout (10s of inactivity) abandons the batch.
/// </para>
/// <para>
/// Requires <c>StreamConfig.AllowBatchPublish = true</c> and nats-server v2.14.0+.
/// </para>
/// </remarks>
public interface INatsJSFastPublisher : IAsyncDisposable
{
    /// <summary>
    /// Gets the number of messages added to the batch so far.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets the highest batch sequence the server has acknowledged so far.
    /// </summary>
    long AckSequence { get; }

    /// <summary>
    /// Gets a value indicating whether the batch has been committed, closed, or terminated by error.
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// Publishes a message to the batch with the given subject and data.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="subject">The subject to publish to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer; when null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The per-message ack with the local batch sequence and the highest server-acked sequence so far.</returns>
    Task<NatsJSFastPubAck> AddAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the batch.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="msg">The message.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer; when null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The per-message ack with the local batch sequence and the highest server-acked sequence so far.</returns>
    Task<NatsJSFastPubAck> AddMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message and commits the batch.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="subject">The subject to publish to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer; when null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The batch ack.</returns>
    Task<NatsJSBatchAck> CommitAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message and commits the batch.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="msg">The final message.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer; when null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The batch ack.</returns>
    Task<NatsJSBatchAck> CommitMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the batch (end-of-batch commit) without storing a final message. The batch must
    /// contain at least one message.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The batch ack.</returns>
    Task<NatsJSBatchAck> CloseAsync(CancellationToken cancellationToken = default);
}
