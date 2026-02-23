// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Represents a high-level JetStream publisher for publishing messages to NATS JetStream.
/// </summary>
/// <typeparam name="T">The type of the data to be published in messages.</typeparam>
public class JetStreamPublisher<T>
{
    private readonly INatsConnection _connection;
    private readonly INatsSerialize<T>? _serializer;
    private readonly Channel<int> _channel;
    private readonly string _inboxPrefix;
    private readonly ConcurrentDictionary<long, Msg<T>> _messages = new();
    private long _id;
    private INatsSub<PubAckResponse>? _sub;

    /// <summary>
    /// Initializes a new instance of the <see cref="JetStreamPublisher{T}"/> class.
    /// Represents a high-level JetStream publisher for publishing messages to NATS JetStream.
    /// </summary>
    /// <param name="connection">NATS connection.</param>
    /// <param name="serializer">Message payload serializer.</param>
    public JetStreamPublisher(INatsConnection connection, INatsSerialize<T>? serializer = null)
    {
        _connection = connection;
        _serializer = serializer;
        _channel = Channel.CreateBounded<int>(128);
        _inboxPrefix = $"_INBOX.{Nuid.NewNuid()}.";
    }

    /// <summary>
    /// Starts the asynchronous operation for the JetStream publisher, initializing necessary subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _sub = await _connection.SubscribeCoreAsync<PubAckResponse>(
            subject: _inboxPrefix + ">",
            serializer: NatsJSJsonSerializer<PubAckResponse>.Default,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Subscribes asynchronously to message status updates from the JetStream publisher.
    /// Uses an asynchronous enumerable to provide real-time updates of message statuses as they occur.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous enumerable of <see cref="MsgStatus{T}"/> representing the status of published messages.</returns>
    public async IAsyncEnumerable<MsgStatus<T>> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<MsgStatus<T>>();

        var msgLoop = Task.Run(
            async () =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    var natsMsg = await _sub!.Msgs.ReadAsync(cancellationToken);

                    _channel.Reader.TryRead(out _);

                    ReadOnlySpan<char> subjectSpan = natsMsg.Subject.AsSpan();
                    ReadOnlySpan<char> lastTokenSpan = subjectSpan.Slice(subjectSpan.LastIndexOf('.') + 1);

#if NETSTANDARD2_0
                    if (!long.TryParse(lastTokenSpan.ToString(), out long id))
#else
                    if (!long.TryParse(lastTokenSpan, out long id))
#endif
                    {
                        await channel.Writer.WriteAsync(
                            new MsgStatus<T>
                            {
                                Error = JetStreamPublisherException.CannotParseSubject(natsMsg.Subject),
                            },
                            cancellationToken);
                        continue;
                    }

                    if (!_messages.TryRemove(id, out Msg<T>? msg))
                    {
                        await channel.Writer.WriteAsync(
                            new MsgStatus<T>
                            {
                                Id = id,
                                Error = JetStreamPublisherException.CannotFindId(id),
                            },
                            cancellationToken);
                        continue;
                    }

                    if (natsMsg.HasNoResponders)
                    {
                        await channel.Writer.WriteAsync(
                            new MsgStatus<T>
                            {
                                Error = JetStreamPublisherException.NoResponders,
                                Id = id,
                                Subject = msg.Subject,
                                Data = msg.Data,
                                Headers = msg.Headers,
                            },
                            cancellationToken);
                        continue;
                    }

                    if (natsMsg.Data is { } ack)
                    {
                        if (ack.IsSuccess())
                        {
                            await channel.Writer.WriteAsync(MsgStatus<T>.AcknowledgedStatus, cancellationToken);
                        }
                        else
                        {
                            await channel.Writer.WriteAsync(
                                new MsgStatus<T>
                                {
                                    Acknowledgment = ack,
                                    Id = id,
                                    Subject = msg.Subject,
                                    Data = msg.Data,
                                    Headers = msg.Headers,
                                },
                                cancellationToken);
                        }
                    }
                    else
                    {
                        await channel.Writer.WriteAsync(
                            new MsgStatus<T>
                            {
                                Error = JetStreamPublisherException.NoData(id),
                                Id = id,
                                Subject = msg.Subject,
                                Data = msg.Data,
                                Headers = msg.Headers,
                            },
                            cancellationToken);
                    }
                }
            },
            cancellationToken);

        var chkLoop = Task.Run(
            async () =>
            {
#if !NETSTANDARD
                var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
#endif
                while (cancellationToken.IsCancellationRequested == false)
                {
#if !NETSTANDARD
                    await timer.WaitForNextTickAsync(cancellationToken);
#else
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
#endif

                    foreach (Msg<T> m in _messages.Values)
                    {
                        if (m.Published.Elapsed > TimeSpan.FromSeconds(5))
                        {
                            m.TotalTimeout += m.Published.Elapsed;
                            await channel.Writer.WriteAsync(
                                new MsgStatus<T>
                                {
                                    Error = JetStreamPublisherException.MessageTimeout(m.Id, m.TotalTimeout),
                                    Id = m.Id,
                                    Subject = m.Subject,
                                    Data = m.Data,
                                    Headers = m.Headers,
                                },
                                cancellationToken);
                            m.Published.Restart();
                        }
                    }
                }
            },
            cancellationToken);

        while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (channel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Removes a published message from the internal message tracking system using its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the message to be removed.</param>
    public void Remove(long id) => _messages.TryRemove(id, out _);

    /// <summary>
    /// Publishes a message to the specified subject on NATS JetStream.
    /// </summary>
    /// <param name="subject">The subject to which the message is published.</param>
    /// <param name="data">The message data to be published.</param>
    /// <param name="headers">Optional headers to include with the message.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation of the publishing operation.</param>
    /// <returns>The unique identifier of the published message.</returns>
    public async ValueTask<long> PublishAsync(string subject, T data, NatsHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        long id = Interlocked.Increment(ref _id);
        await _channel.Writer.WriteAsync(1, cancellationToken);
        string replyTo = _inboxPrefix + id;

        _messages[id] = new Msg<T> { Id = id, Subject = subject, Data = data, Headers = headers, Published = Stopwatch.StartNew() };

        try
        {
            await _connection.PublishAsync<T>(subject, data, headers, replyTo, _serializer, null, cancellationToken);
        }
        catch
        {
            _messages.TryRemove(id, out _);
            throw;
        }

        return id;
    }

    /// <summary>
    /// Represents the status of a message published via the JetStream publisher.
    /// </summary>
    /// <typeparam name="TMsg">The type of the data contained in the message.</typeparam>
    public class MsgStatus<TMsg>
    {
        /// <summary>
        /// Represents the acknowledged status of a message published using the JetStreamPublisher.
        /// This static instance indicates that a message has been successfully acknowledged
        /// by the JetStream server during publishing.
        /// </summary>
        public static readonly MsgStatus<TMsg> AcknowledgedStatus = new() { Acknowledged = true };

        /// <summary>
        /// Gets a value indicating whether a message published via the JetStreamPublisher has been successfully
        /// acknowledged by the JetStream server. A value of <c>true</c> signifies successful acknowledgment,
        /// while <c>false</c> represents that the acknowledgment is either pending or was unsuccessful.
        /// </summary>
        public bool Acknowledged { get; init; }

        /// <summary>
        /// Gets the unique identifier for a message published using the JetStreamPublisher.
        /// This property is used to track and correlate messages within the publishing process,
        /// allowing for efficient acknowledgment handling, removal, and error tracking.
        /// </summary>
        public long Id { get; init; }

        /// <summary>
        /// Gets an error encountered during the processing or publishing of a message
        /// using the JetStream publisher.
        /// This property holds any <see cref="Exception"/> instance that describes the
        /// specific error that occurred, such as message parsing failures, no responders,
        /// timeout exceptions, or other processing issues.
        /// </summary>
        public Exception? Error { get; init; }

        /// <summary>
        /// Gets the acknowledgment response received from the JetStream server for a published message.
        /// This property contains detailed acknowledgment data, including whether the server successfully
        /// processed the message.
        /// </summary>
        public PubAckResponse? Acknowledgment { get; init; }

        /// <summary>
        /// Gets the subject of a message published or received via the JetStream publisher.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Gets the payload or content of the message handled by the JetStream publisher.
        /// </summary>
        public TMsg? Data { get; init; }

        /// <summary>
        /// Gets the collection of headers associated with a specific message in the JetStreamPublisher.
        /// </summary>
        public NatsHeaders? Headers { get; init; }
    }

    private class Msg<TNsg>
    {
        public long Id { get; set; }

        public required string Subject { get; set; }

        public required TNsg Data { get; set; }

        public NatsHeaders? Headers { get; set; }

        public required Stopwatch Published { get; set; }

        public TimeSpan TotalTimeout { get; set; } = TimeSpan.Zero;
    }
}
