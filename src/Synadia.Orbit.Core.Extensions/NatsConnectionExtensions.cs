// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;

namespace Synadia.Orbit.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="INatsConnection"/> to enable additional functionality,
/// such as request-many with custom sentinel support.
/// </summary>
public static class NatsConnectionExtensions
{
    /// <summary>
    /// Send a request and receive multiple replies with custom sentinel support.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request data.</typeparam>
    /// <typeparam name="TReply">The type of the reply data.</typeparam>
    /// <param name="connection">The NATS connection.</param>
    /// <param name="subject">The subject to send the request to.</param>
    /// <param name="data">The request data.</param>
    /// <param name="sentinel">
    /// A function that determines when to stop receiving messages.
    /// When it returns <c>true</c>, the enumeration ends and that message is not yielded.
    /// </param>
    /// <param name="headers">Optional headers to include with the request.</param>
    /// <param name="requestSerializer">Optional serializer for the request data.</param>
    /// <param name="replySerializer">Optional deserializer for the reply data.</param>
    /// <param name="requestOpts">Optional publish options for the request.</param>
    /// <param name="replyOpts">Optional subscription options for the replies.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>An async enumerable of reply messages.</returns>
    /// <remarks>
    /// <para>
    /// This method wraps <see cref="INatsConnection.RequestManyAsync{TRequest,TReply}"/> and adds
    /// support for a custom sentinel function. The sentinel function is called for each received
    /// message, and when it returns <c>true</c>, the enumeration stops without yielding that message.
    /// </para>
    /// <para>
    /// This is useful in scatter-gather scenarios where a custom condition (other than an empty message)
    /// signals the end of responses.
    /// </para>
    /// <example>
    /// <code>
    /// // Stop when header indicates completion
    /// await foreach (var msg in nats.RequestManyWithSentinelAsync&lt;Request, Response&gt;(
    ///     subject: "service.request",
    ///     data: new Request(),
    ///     sentinel: msg => msg.Headers?["X-Done"].ToString() == "true"))
    /// {
    ///     Console.WriteLine(msg.Data);
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public static IAsyncEnumerable<NatsMsg<TReply>> RequestManyWithSentinelAsync<TRequest, TReply>(
        this INatsConnection connection,
        string subject,
        TRequest? data,
        Func<NatsMsg<TReply>, bool> sentinel,
        NatsHeaders? headers = default,
        INatsSerialize<TRequest>? requestSerializer = default,
        INatsDeserialize<TReply>? replySerializer = default,
        NatsPubOpts? requestOpts = default,
        NatsSubOpts? replyOpts = default,
        CancellationToken cancellationToken = default)
    {
        if (sentinel == null)
        {
            throw new ArgumentNullException(nameof(sentinel));
        }

        return RequestManyWithSentinelInternalAsync(
            connection,
            subject,
            data,
            sentinel,
            headers,
            requestSerializer,
            replySerializer,
            requestOpts,
            replyOpts,
            cancellationToken);
    }

    /// <summary>
    /// Send a request message and receive multiple replies with custom sentinel support.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request data.</typeparam>
    /// <typeparam name="TReply">The type of the reply data.</typeparam>
    /// <param name="connection">The NATS connection.</param>
    /// <param name="msg">The request message containing subject, data, and headers.</param>
    /// <param name="sentinel">
    /// A function that determines when to stop receiving messages.
    /// When it returns <c>true</c>, the enumeration ends and that message is not yielded.
    /// </param>
    /// <param name="requestSerializer">Optional serializer for the request data.</param>
    /// <param name="replySerializer">Optional deserializer for the reply data.</param>
    /// <param name="requestOpts">Optional publish options for the request.</param>
    /// <param name="replyOpts">Optional subscription options for the replies.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>An async enumerable of reply messages.</returns>
    /// <remarks>
    /// <para>
    /// This method wraps <see cref="INatsConnection.RequestManyAsync{TRequest,TReply}"/> and adds
    /// support for a custom sentinel function. The sentinel function is called for each received
    /// message, and when it returns <c>true</c>, the enumeration stops without yielding that message.
    /// </para>
    /// </remarks>
    public static IAsyncEnumerable<NatsMsg<TReply>> RequestManyWithSentinelAsync<TRequest, TReply>(
        this INatsConnection connection,
        NatsMsg<TRequest> msg,
        Func<NatsMsg<TReply>, bool> sentinel,
        INatsSerialize<TRequest>? requestSerializer = default,
        INatsDeserialize<TReply>? replySerializer = default,
        NatsPubOpts? requestOpts = default,
        NatsSubOpts? replyOpts = default,
        CancellationToken cancellationToken = default)
    {
        if (sentinel == null)
        {
            throw new ArgumentNullException(nameof(sentinel));
        }

        return RequestManyWithSentinelInternalAsync(
            connection,
            msg.Subject,
            msg.Data,
            sentinel,
            msg.Headers,
            requestSerializer,
            replySerializer,
            requestOpts,
            replyOpts,
            cancellationToken);
    }

    private static async IAsyncEnumerable<NatsMsg<TReply>> RequestManyWithSentinelInternalAsync<TRequest, TReply>(
        INatsConnection connection,
        string subject,
        TRequest? data,
        Func<NatsMsg<TReply>, bool> sentinel,
        NatsHeaders? headers,
        INatsSerialize<TRequest>? requestSerializer,
        INatsDeserialize<TReply>? replySerializer,
        NatsPubOpts? requestOpts,
        NatsSubOpts? replyOpts,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Disable the default empty message sentinel since we're using a custom one
        replyOpts = replyOpts == null
            ? new NatsSubOpts { StopOnEmptyMsg = false }
            : replyOpts with { StopOnEmptyMsg = false };

        var requestManyAsync = connection.RequestManyAsync<TRequest, TReply>(
            subject: subject,
            data: data,
            headers: headers,
            requestSerializer: requestSerializer,
            replySerializer: replySerializer,
            requestOpts: requestOpts,
            replyOpts: replyOpts,
            cancellationToken: cancellationToken);

        await foreach (var msg in requestManyAsync.ConfigureAwait(false))
        {
            if (sentinel(msg))
            {
                yield break;
            }

            yield return msg;
        }
    }
}
