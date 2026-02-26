// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Static helper methods for batch publishing.
/// </summary>
public static class JetStreamBatchPublish
{
    /// <summary>
    /// Publishes a batch of messages to a Stream and waits for an ack for the commit.
    /// </summary>
    /// <param name="js">The JetStream context to use for publishing.</param>
    /// <param name="messages">The messages to publish as a batch.</param>
    /// <param name="flowControl">Optional flow control configuration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The batch acknowledgment from the server.</returns>
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
            headers[BatchHeaders.BatchCommit] = "1";
            var commitMsg = msgToSend with { Headers = headers };

            // Apply default timeout if no cancellation is set, matching Go's wrapContextWithoutDeadline behavior.
            using var commitCts = BatchPublishHelper.CreateCommitCancellationTokenSource(cancellationToken, js.Opts.RequestTimeout);
            var commitToken = commitCts?.Token ?? cancellationToken;

            var commitResponse = await js.Connection.RequestAsync<byte[], byte[]>(
                commitMsg.Subject,
                commitMsg.Data,
                headers: commitMsg.Headers,
                cancellationToken: commitToken);

            var batchResponse = BatchPublishHelper.DeserializeAckResponse(commitResponse.Data);

            if (batchResponse?.Error != null)
            {
                BatchPublishHelper.ThrowBatchPublishException(batchResponse.Error);
            }

            if (batchResponse == null ||
                string.IsNullOrEmpty(batchResponse.Stream) ||
                batchResponse.BatchId != batchId ||
                batchResponse.BatchSize != messages.Length)
            {
                throw new InvalidBatchAckException();
            }

            return new BatchAck
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
