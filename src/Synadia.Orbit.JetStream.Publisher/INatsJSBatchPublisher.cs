// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Provides methods for publishing messages to a stream in batches.
/// </summary>
/// <remarks>
/// <para>
/// Disposing without calling <c>CommitAsync</c> or <c>CommitMsgAsync</c> closes the publisher
/// locally but leaves any already-sent messages as an incomplete batch on the server until the
/// server's batch timeout expires. Call <see cref="Discard"/> or commit before disposal to make
/// the intent explicit.
/// </para>
/// <para>
/// If a payload's value implements <see cref="IDisposable"/> (for example
/// <see cref="NatsMemoryOwner{T}"/> or any <see cref="System.Buffers.IMemoryOwner{T}"/>),
/// ownership transfers to the publisher: the buffer is disposed after the bytes are written to
/// the wire, or on any pre-publish throw.
/// </para>
/// </remarks>
public interface INatsJSBatchPublisher : IAsyncDisposable
{
    /// <summary>
    /// Gets the number of messages added to the batch so far.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets a value indicating whether the batch has been committed or discarded.
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// Publishes a message to the batch with the given subject and data.
    /// </summary>
    /// <typeparam name="T">The payload type. Resolved by the configured serializer or by an explicit <paramref name="serializer"/>.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data. See remarks on <see cref="INatsJSBatchPublisher"/> for ownership semantics.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer for the payload. When null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// When flow control is disabled (<see cref="NatsJSBatchFlowControl.AckFirst"/> is false and
    /// <see cref="NatsJSBatchFlowControl.AckEvery"/> is 0), this method publishes fire-and-forget;
    /// server-side errors for individual messages are not observed until commit.
    /// </remarks>
    Task AddAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the batch.
    /// </summary>
    /// <typeparam name="T">The payload type. Resolved by the configured serializer or by an explicit <paramref name="serializer"/>.</typeparam>
    /// <param name="msg">The message to publish. See remarks on <see cref="INatsJSBatchPublisher"/> for ownership semantics.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer for the payload. When null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// When flow control is disabled (<see cref="NatsJSBatchFlowControl.AckFirst"/> is false and
    /// <see cref="NatsJSBatchFlowControl.AckEvery"/> is 0), this method publishes fire-and-forget;
    /// server-side errors for individual messages are not observed until commit.
    /// </remarks>
    Task AddMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message with the given subject and data, and commits the batch.
    /// </summary>
    /// <typeparam name="T">The payload type. Resolved by the configured serializer or by an explicit <paramref name="serializer"/>.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data. See remarks on <see cref="INatsJSBatchPublisher"/> for ownership semantics.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer for the payload. When null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the batch acknowledgment.</returns>
    /// <remarks>
    /// If a <see cref="TimeoutException"/> or <see cref="OperationCanceledException"/> is thrown
    /// after the commit request was sent, the batch may or may not have been persisted; the ack
    /// may simply have been lost. There is no idempotency key for commits, so callers should
    /// check the stream's last sequence before retrying.
    /// </remarks>
    Task<NatsJSBatchAck> CommitAsync<T>(string subject, T data, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message and commits the batch.
    /// </summary>
    /// <typeparam name="T">The payload type. Resolved by the configured serializer or by an explicit <paramref name="serializer"/>.</typeparam>
    /// <param name="msg">The message to publish. See remarks on <see cref="INatsJSBatchPublisher"/> for ownership semantics.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="serializer">Optional serializer for the payload. When null, the connection's registered serializer is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the batch acknowledgment.</returns>
    /// <remarks>
    /// If a <see cref="TimeoutException"/> or <see cref="OperationCanceledException"/> is thrown
    /// after the commit request was sent, the batch may or may not have been persisted; the ack
    /// may simply have been lost. There is no idempotency key for commits, so callers should
    /// check the stream's last sequence before retrying.
    /// </remarks>
    Task<NatsJSBatchAck> CommitMsgAsync<T>(NatsMsg<T> msg, NatsJSBatchMsgOpts? opts = null, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the batch without committing.
    /// </summary>
    void Discard();
}
