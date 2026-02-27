// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Provides methods for publishing messages to a stream in batches.
/// </summary>
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
    Task AddAsync(string subject, byte[] data, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the batch.
    /// </summary>
    /// <param name="msg">The message to publish.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddMsgAsync(NatsMsg<byte[]> msg, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message with the given subject and data, and commits the batch.
    /// </summary>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the batch acknowledgment.</returns>
    Task<NatsJSBatchAck> CommitAsync(string subject, byte[] data, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the final message and commits the batch.
    /// </summary>
    /// <param name="msg">The message to publish.</param>
    /// <param name="opts">Optional per-message options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the batch acknowledgment.</returns>
    Task<NatsJSBatchAck> CommitMsgAsync(NatsMsg<byte[]> msg, NatsJSBatchMsgOpts? opts = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the batch without committing.
    /// </summary>
    void Discard();
}
