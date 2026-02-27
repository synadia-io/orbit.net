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

        if (messages.Count > NatsJSBatchPublisher.MaxBatchSize)
        {
            throw new NatsJSBatchPublishExceedsLimitException();
        }

        var batchId = Nuid.NewNuid();
        var fc = flowControl ?? new NatsJSBatchFlowControl();

        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            var headers = msg.Headers ?? new NatsHeaders();

            headers.Remove(NatsJSBatchHeaders.BatchCommit);
            headers[NatsJSBatchHeaders.BatchId] = batchId;
            headers[NatsJSBatchHeaders.BatchSeq] = (i + 1).ToString();

            var msgToSend = msg with { Headers = headers };

            // Add all but last message to the batch
            if (i < messages.Count - 1)
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
                    await js.Connection.PublishAsync(msgToSend.Subject, msgToSend.Data, headers: msgToSend.Headers, cancellationToken: cancellationToken).ConfigureAwait(false);
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
                        cancellationToken: cts.Token).ConfigureAwait(false);

                    if (response.Data?.Length > 0)
                    {
                        var apiResponse = BatchPublishHelper.DeserializeApiResponse(response.Data);
                        if (apiResponse?.Error != null)
                        {
                            BatchPublishHelper.ThrowBatchPublishException(apiResponse.Error);
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
            headers[NatsJSBatchHeaders.BatchCommit] = "1";
            var commitMsg = msgToSend with { Headers = headers };

            // Apply default timeout, matching Go's wrapContextWithoutDeadline behavior.
            using var commitCts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, js.Opts.RequestTimeout);

            var commitResponse = await js.Connection.RequestAsync<byte[], byte[]>(
                commitMsg.Subject,
                commitMsg.Data,
                headers: commitMsg.Headers,
                cancellationToken: commitCts.Token).ConfigureAwait(false);

            var batchResponse = BatchPublishHelper.DeserializeAckResponse(commitResponse.Data);

            if (batchResponse?.Error != null)
            {
                BatchPublishHelper.ThrowBatchPublishException(batchResponse.Error);
            }

            if (batchResponse == null ||
                string.IsNullOrEmpty(batchResponse.Stream) ||
                batchResponse.BatchId != batchId ||
                batchResponse.BatchSize != messages.Count)
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

        throw new InvalidOperationException("Unreachable code");
    }
}
