// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Synadia.Orbit.DirectGet.Models;

namespace Synadia.Orbit.DirectGet;

/// <summary>
/// Provides extension methods for JetStream to enable additional functionality,
/// such as requesting direct batch messages.
/// </summary>
public static class JetStreamExtensions
{
    /// <summary>
    /// Request a direct batch message.
    /// </summary>
    /// <param name="context">JetStream Context.</param>
    /// <param name="stream">Stream name.</param>
    /// <param name="request">Batch message request.</param>
    /// <param name="serializer">Serializer to use for the message type.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <typeparam name="T">Message type to deserialize.</typeparam>
    /// <exception cref="NatsNoRespondersException">Stream must have the allow-direct set.</exception>
    /// <returns>Async enumeration to be used in an await-foreach.</returns>
    public static async IAsyncEnumerable<NatsMsg<T>> GetBatchDirectAsync<T>(
        this INatsJSContext context,
        string stream,
        StreamMsgBatchGetRequest request,
        INatsDeserialize<T>? serializer = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestManyAsync = context.Connection.RequestManyAsync(
            subject: $"{context.Opts.Prefix}.DIRECT.GET.{stream}",
            data: request,
            requestSerializer: DirectGetJsonSerializer<StreamMsgBatchGetRequest>.Default,
            replySerializer: serializer,
            replyOpts: new NatsSubOpts { StopOnEmptyMsg = true, ThrowIfNoResponders = true },
            cancellationToken: cancellationToken);

        await foreach (var msg in requestManyAsync.ConfigureAwait(false))
        {
            if (msg.Error is { } error)
            {
                throw error;
            }

            yield return msg;
        }
    }
}
