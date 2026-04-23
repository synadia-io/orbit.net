// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Extension methods for batch publishing on <see cref="INatsJSContext"/>.
/// </summary>
public static class NatsJSBatchPublishExtensions
{
    /// <summary>
    /// Publishes a batch of messages to a Stream and waits for an ack for the commit.
    /// </summary>
    /// <param name="js">The JetStream context to use for publishing.</param>
    /// <param name="messages">The messages to publish as a batch.</param>
    /// <param name="flowControl">Optional flow control configuration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The batch acknowledgment from the server.</returns>
    public static async Task<NatsJSBatchAck> PublishMsgBatchAsync(
        this INatsJSContext js,
        IReadOnlyList<NatsMsg<byte[]>> messages,
        NatsJSBatchFlowControl? flowControl = null,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            throw new ArgumentException("No messages to publish", nameof(messages));
        }

        await using var publisher = new NatsJSBatchPublisher(js, flowControl);

        for (int i = 0; i < messages.Count - 1; i++)
        {
            await publisher.AddMsgAsync(messages[i], cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await publisher.CommitMsgAsync(messages[messages.Count - 1], cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
