// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Provides methods for publishing messages to a stream in batches.
/// </summary>
/// <remarks>
/// Disposing without calling <see cref="CommitAsync"/> or <see cref="CommitMsgAsync"/> closes the
/// publisher locally but leaves any already-sent messages as an incomplete batch on the server
/// until the server's batch timeout expires. Call <see cref="Discard"/> or commit before disposal
/// to make the intent explicit.
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
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// When flow control is disabled (<see cref="NatsJSBatchFlowControl.AckFirst"/> is false and
    /// <see cref="NatsJSBatchFlowControl.AckEvery"/> is 0), this method publishes fire-and-forget.
    /// Server-side errors for individual messages are not observed until
    /// <see cref="CommitAsync"/>/<see cref="CommitMsgAsync"/> is called.
    /// </remarks>
    Task AddAsync(string subject, byte[] data, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the batch.
    /// </summary>
    /// <param name="msg">The message to publish.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// When flow control is disabled (<see cref="NatsJSBatchFlowControl.AckFirst"/> is false and
    /// <see cref="NatsJSBatchFlowControl.AckEvery"/> is 0), this method publishes fire-and-forget.
    /// Server-side errors for individual messages are not observed until
    /// <see cref="CommitAsync"/>/<see cref="CommitMsgAsync"/> is called.
    /// </remarks>
    Task AddMsgAsync(NatsMsg<byte[]> msg, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message with the given subject and data, and commits the batch.
    /// </summary>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the batch acknowledgment.</returns>
    /// <remarks>
    /// If a <see cref="TimeoutException"/> is thrown, the commit request was already sent to the
    /// server. The batch may or may not have been persisted; the ack may simply have been lost.
    /// There is no idempotency key for commits, so callers should check the stream's last sequence
    /// to determine whether the batch was actually written before retrying.
    /// </remarks>
    Task<NatsJSBatchAck> CommitAsync(string subject, byte[] data, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message and commits the batch.
    /// </summary>
    /// <param name="msg">The message to publish.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the batch acknowledgment.</returns>
    /// <remarks>
    /// If a <see cref="TimeoutException"/> is thrown, the commit request was already sent to the
    /// server. The batch may or may not have been persisted; the ack may simply have been lost.
    /// There is no idempotency key for commits, so callers should check the stream's last sequence
    /// to determine whether the batch was actually written before retrying.
    /// </remarks>
    Task<NatsJSBatchAck> CommitMsgAsync(NatsMsg<byte[]> msg, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the batch without committing.
    /// </summary>
    void Discard();
}
