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
    /// Retrieves a message from the stream using direct get if the stream allows it, falling back to the standard stream get API.
    /// </summary>
    /// <param name="context">JetStream Context.</param>
    /// <param name="stream">The JetStream stream.</param>
    /// <param name="request">The request specifying which message to retrieve.</param>
    /// <param name="serializer">The deserializer to use for the message data.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <typeparam name="T">The type to deserialize the message data into.</typeparam>
    /// <returns>A <see cref="NatsStreamMsg{T}"/> containing the message data and metadata.</returns>
    /// <exception cref="NatsJSNoMessageFoundException">The message was not found.</exception>
    /// <exception cref="NatsJSException">There was an issue retrieving the response.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public static async ValueTask<NatsStreamMsg<T>> GetAutoAsync<T>(
        this INatsJSContext context,
        INatsJSStream stream,
        StreamMsgGetRequest request,
        INatsDeserialize<T>? serializer = default,
        CancellationToken cancellationToken = default)
    {
        serializer ??= context.Connection.Opts.SerializerRegistry.GetDeserializer<T>();

        var streamName = stream.Info.Config.Name
            ?? throw new NatsJSException("Stream name is not available");

        if (stream.Info.Config.AllowDirect)
        {
            try
            {
                var msg = await context.Connection.RequestAsync<StreamMsgGetRequest, T>(
                    subject: $"{context.Opts.Prefix}.DIRECT.GET.{streamName}",
                    data: request,
                    requestSerializer: DirectGetJsonSerializer<StreamMsgGetRequest>.Default,
                    replySerializer: serializer,
                    replyOpts: new NatsSubOpts { ThrowIfNoResponders = true },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                return NatsStreamMsg<T>.FromDirect(msg);
            }
            catch (NatsNoRespondersException)
            {
                // Race condition: AllowDirect was true but server no longer supports direct get.
                // Fall back to the standard stream get API.
            }
        }

        var response = await context.JSRequestResponseAsync<StreamMsgGetRequest, StreamMsgGetResponse>(
            subject: $"{context.Opts.Prefix}.STREAM.MSG.GET.{streamName}",
            request: request,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return NatsStreamMsg<T>.FromStreamResponse(response, serializer);
    }

    /// <summary>
    /// Retrieves a message from the stream using direct get if the stream allows it, falling back to the standard stream get API.
    /// </summary>
    /// <param name="context">JetStream Context.</param>
    /// <param name="stream">The name of the JetStream stream.</param>
    /// <param name="request">The request specifying which message to retrieve.</param>
    /// <param name="serializer">The deserializer to use for the message data.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the API call.</param>
    /// <typeparam name="T">The type to deserialize the message data into.</typeparam>
    /// <returns>A <see cref="NatsStreamMsg{T}"/> containing the message data and metadata.</returns>
    /// <exception cref="NatsJSNoMessageFoundException">The message was not found.</exception>
    /// <exception cref="NatsJSException">There was an issue retrieving the response.</exception>
    /// <exception cref="NatsJSApiException">Server responded with an error.</exception>
    public static async ValueTask<NatsStreamMsg<T>> GetAutoAsync<T>(
        this INatsJSContext context,
        string stream,
        StreamMsgGetRequest request,
        INatsDeserialize<T>? serializer = default,
        CancellationToken cancellationToken = default)
    {
        var streamObj = await context.GetStreamAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await GetAutoAsync(context, streamObj, request, serializer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a scheduled message to a JetStream stream without message data.
    /// This is typically used with <see cref="NatsMsgSchedule.Source"/> where the data is sourced
    /// from another subject when the schedule fires.
    /// </summary>
    /// <param name="context">The JetStream context.</param>
    /// <param name="subject">The subject to publish the scheduled message to.</param>
    /// <param name="schedule">The schedule configuration specifying when and where to deliver the message.</param>
    /// <param name="opts">Optional publish options.</param>
    /// <param name="headers">Optional additional headers to include with the message.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="PubAckResponse"/> indicating the result of the publish operation.</returns>
    /// <remarks>
    /// The stream must have <c>AllowMsgSchedules</c> enabled. If using TTL, the stream must also have
    /// <c>AllowMsgTTL</c> enabled. The target subject specified in the schedule must be within the
    /// stream's subject filter.
    /// <para>Server version requirements: <c>@at</c> schedules require NATS Server 2.12+.
    /// <c>@every</c> (repeating interval), cron schedules, predefined schedules
    /// (<c>@hourly</c>, <c>@daily</c>, ...), and <c>Source</c> (data sampling) require NATS Server 2.14+.</para>
    /// </remarks>
    public static ValueTask<PubAckResponse> PublishScheduledAsync(
        this INatsJSContext context,
        string subject,
        NatsMsgSchedule schedule,
        NatsJSPubOpts? opts = null,
        NatsHeaders? headers = null,
        CancellationToken cancellationToken = default)
    {
        var mergedHeaders = schedule.ToHeaders(headers);
        return context.PublishAsync<byte[]?>(subject, null, opts: opts, headers: mergedHeaders, cancellationToken: cancellationToken);
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
    /// <c>@every</c> (repeating interval), cron schedules, predefined schedules
    /// (<c>@hourly</c>, <c>@daily</c>, ...), and <c>Source</c> (data sampling) require NATS Server 2.14+.</para>
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
