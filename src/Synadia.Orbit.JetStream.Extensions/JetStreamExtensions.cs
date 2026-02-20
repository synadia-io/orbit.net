// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Synadia.Orbit.JetStream.Extensions.Models;

namespace Synadia.Orbit.JetStream.Extensions;

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

    /// <summary>
    /// Publishes a scheduled message to a JetStream stream.
    /// </summary>
    /// <typeparam name="T">The type of the message data.</typeparam>
    /// <param name="context">The JetStream context.</param>
    /// <param name="subject">The subject to publish the scheduled message to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="schedule">The schedule configuration specifying when and where to deliver the message.</param>
    /// <param name="serializer">Optional serializer for the message data.</param>
    /// <param name="opts">Optional publish options.</param>
    /// <param name="headers">Optional additional headers to include with the message.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="PubAckResponse"/> indicating the result of the publish operation.</returns>
    /// <remarks>
    /// The stream must have <c>AllowMsgSchedules</c> enabled. If using TTL, the stream must also have
    /// <c>AllowMsgTTL</c> enabled. The target subject specified in the schedule must be within the
    /// stream's subject filter.
    /// <para>Server version requirements: <c>@at</c> schedules require NATS Server 2.12+.
    /// <c>@every</c> (repeating interval) and <c>Source</c> (data sampling) require NATS Server 2.14+.</para>
    /// </remarks>
    public static ValueTask<PubAckResponse> PublishScheduledAsync<T>(
        this INatsJSContext context,
        string subject,
        T? data,
        NatsMsgSchedule schedule,
        INatsSerialize<T>? serializer = null,
        NatsJSPubOpts? opts = null,
        NatsHeaders? headers = null,
        CancellationToken cancellationToken = default)
    {
        var mergedHeaders = schedule.ToHeaders(headers);
        return context.PublishAsync(subject, data, serializer, opts, mergedHeaders, cancellationToken);
    }
}
