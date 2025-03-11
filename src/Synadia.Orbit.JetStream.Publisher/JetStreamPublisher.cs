// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

public class JetStreamPublisher<T>
{
    private readonly INatsConnection _connection;
    private readonly INatsSerialize<T>? _serializer;

    public class MsgStatus<T>
    {
        public static readonly MsgStatus<T> AcknowledgedStatus = new MsgStatus<T> { Acknowledged = true };

        internal JetStreamPublisher<T> Publisher { get; }

        public bool Acknowledged { get; init; }

        public long Id { get; init; }

        public Exception? Error { get; init; }

        public PubAckResponse? Ackowlegment { get; init; }

        public string? Subject { get; init; }

        public T? Data { get; init; }

        public NatsHeaders? Headers { get; init; }

        public void Remove() => Publisher.Remove(Id);
    }

    class Msg<T>
    {
        public long Id { get; set; }

        public string Subject { get; set; }

        public T Data { get; set; }

        public NatsHeaders? Headers { get; set; }

        public Stopwatch Published { get; set; }

        public TimeSpan TotalTimeout { get; set; } = TimeSpan.Zero;
    }


    private readonly Channel<int> _channel;
    private readonly string _inboxPrefix;
    private readonly ConcurrentDictionary<long, Msg<T>> _messages = new();
    private long _id;
    private INatsSub<PubAckResponse> _sub;

    public JetStreamPublisher(INatsConnection connection, INatsSerialize<T>? serializer = null)
    {
        _connection = connection;
        _serializer = serializer;
        _channel = Channel.CreateBounded<int>(128);
        _inboxPrefix = $"_INBOX.{Nuid.NewNuid()}.";
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _sub = await _connection.SubscribeCoreAsync<PubAckResponse>(
            subject: _inboxPrefix + ">",
            serializer: NatsJSJsonSerializer<PubAckResponse>.Default,
            cancellationToken: cancellationToken);
    }
    public async IAsyncEnumerable<MsgStatus<T>> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<MsgStatus<T>>();

        var msgLoop = Task.Run(
            async () =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    var natsMsg = await _sub.Msgs.ReadAsync(cancellationToken);

                    _channel.Reader.TryRead(out _);

                    ReadOnlySpan<char> subjectSpan = natsMsg.Subject.AsSpan();
                    ReadOnlySpan<char> lastTokenSpan = subjectSpan.Slice(subjectSpan.LastIndexOf('.') + 1);

#if NETSTANDARD2_0
                    if (!long.TryParse(lastTokenSpan.ToString(), out long id))
#else
                    if (!long.TryParse(lastTokenSpan, out long id))
#endif
                    {
                        await channel.Writer.WriteAsync(new MsgStatus<T> { Error = new CannotParseSubjectException(natsMsg.Subject) }, cancellationToken);
                        continue;
                    }

                    if (!_messages.TryRemove(id, out Msg<T> msg))
                    {
                        await channel.Writer.WriteAsync(new MsgStatus<T> { Id = id, Error = new CannotFindPublishStatusException(id) }, cancellationToken);
                        continue;
                    }

                    if (natsMsg.HasNoResponders)
                    {
                        await channel.Writer.WriteAsync(
                            new MsgStatus<T>
                            {
                                Error = NoRespondersException.Default,
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
                                    Ackowlegment = ack,
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
                                Error = new NoDataException(id),
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
                                    Error = new MessageTimeoutException(m.Id, m.TotalTimeout),
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

    public void Remove(long id) => _messages.TryRemove(id, out _);

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
}

public class CannotParseSubjectException(string subject) : Exception
{
    public string Subject { get; } = subject;
}

public class CannotFindPublishStatusException(long id) : Exception
{
    public long Id { get; } = id;
}

public class NoDataException(long id) : Exception
{
    public long Id { get; } = id;
}

public class NoRespondersException : Exception
{
    public static readonly NoRespondersException Default = new();
}

public class MessageTimeoutException(long id, TimeSpan timeout) : Exception
{
    public long Id { get; } = id;

    public TimeSpan Timeout { get; } = timeout;
}
