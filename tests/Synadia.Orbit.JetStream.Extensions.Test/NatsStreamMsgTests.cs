// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Synadia.Orbit.JetStream.Extensions;

namespace Synadia.Orbit.JetStream.Extensions.Test;

public class NatsStreamMsgTests
{
    [Fact]
    public void FromDirect_WithHeaders_ReturnsStreamMsg()
    {
        var headers = new NatsHeaders { ["Nats-Subject"] = "orders.created", ["Nats-Sequence"] = "42", ["Nats-Time-Stamp"] = "2026-08-15T10:30:00Z" };

        byte[]? payload = "hello"u8.ToArray();
        var msg = new NatsMsg<string>(
            subject: "_INBOX.abc.123",
            replyTo: "_INBOX.abc.456",
            size: payload.Length,
            headers: headers,
            data: "hello",
            connection: null,
            flags: NatsMsgFlags.None);

        var result = NatsStreamMsg<string>.FromDirect(msg);

        Assert.Equal("hello", result.Data);
        Assert.Equal(42UL, result.Sequence);
        Assert.Equal("orders.created", result.Subject);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 10, 30, 0, TimeSpan.Zero), result.Time);
        Assert.NotNull(result.Headers);
        Assert.Equal("orders.created", result.Headers!["Nats-Subject"]);
    }

    [Fact]
    public void FromDirect_WithoutHeaders_ReturnsStreamMsgWithInboxSubject()
    {
        byte[]? payload = "hello"u8.ToArray();
        var msg = new NatsMsg<string>(
            subject: "_INBOX.abc.123",
            replyTo: "_INBOX.abc.456",
            size: payload.Length,
            headers: null,
            data: "hello",
            connection: null,
            flags: NatsMsgFlags.None);

        var result = NatsStreamMsg<string>.FromDirect(msg);

        Assert.Equal("hello", result.Data);
        Assert.Equal(0UL, result.Sequence);
        Assert.Equal("_INBOX.abc.123", result.Subject);
        Assert.Equal(default(DateTimeOffset), result.Time);
        Assert.Null(result.Headers);
    }

    [Fact]
    public void FromDirect_WithNullData_ReturnsNullData()
    {
        var msg = new NatsMsg<string>(
            subject: "_INBOX.abc.123",
            replyTo: null,
            size: 0,
            headers: null,
            data: null,
            connection: null,
            flags: NatsMsgFlags.None);

        var result = NatsStreamMsg<string>.FromDirect(msg);

        Assert.Null(result.Data);
        Assert.Equal(0UL, result.Sequence);
        Assert.Equal("_INBOX.abc.123", result.Subject);
    }

    [Fact]
    public void FromDirect_With404Status_ThrowsNatsJSNoMessageFoundException()
    {
        var msg = BuildMsg(ParseHeaders("NATS/1.0 404\r\n\r\n"));

        Assert.Throws<NatsJSNoMessageFoundException>(() => NatsStreamMsg<string>.FromDirect(msg));
    }

    [Fact]
    public void FromDirect_WithNon404Status_ThrowsNatsJSException()
    {
        var msg = BuildMsg(ParseHeaders(
            "NATS/1.0 503\r\n" +
            "Description: service unavailable\r\n" +
            "\r\n"));

        var ex = Assert.Throws<NatsJSException>(() => NatsStreamMsg<string>.FromDirect(msg));
        Assert.Contains("service unavailable", ex.Message);
    }

    [Fact]
    public void FromDirect_WithNon404StatusWithoutDescription_ThrowsNatsJSExceptionWithMessageText()
    {
        var msg = BuildMsg(ParseHeaders("NATS/1.0 503 Service Unavailable\r\n\r\n"));

        var ex = Assert.Throws<NatsJSException>(() => NatsStreamMsg<string>.FromDirect(msg));
        Assert.Contains("Service Unavailable", ex.Message);
    }

    [Fact]
    public void FromDirect_WithMalformedSequence_ThrowsNatsJSException()
    {
        var msg = BuildMsg(
            ParseHeaders(
                "NATS/1.0\r\n" +
                "Nats-Subject: foo\r\n" +
                "Nats-Sequence: not-a-number\r\n" +
                "\r\n"),
            "hello");

        Assert.Throws<NatsJSException>(() => NatsStreamMsg<string>.FromDirect(msg));
    }

    [Fact]
    public void FromDirect_WithMalformedTimeStamp_ThrowsNatsJSException()
    {
        var msg = BuildMsg(
            ParseHeaders(
                "NATS/1.0\r\n" +
                "Nats-Subject: foo\r\n" +
                "Nats-Time-Stamp: not-a-date\r\n" +
                "\r\n"),
            "hello");

        Assert.Throws<NatsJSException>(() => NatsStreamMsg<string>.FromDirect(msg));
    }

    [Fact]
    public void FromDirect_NullMsg_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => NatsStreamMsg<string>.FromDirect(default));
    }

    [Fact]
    public void FromStreamResponse_ReturnsStreamMsg()
    {
#pragma warning disable IL3050, IL2026
        var response = new StreamMsgGetResponse
        {
            Message = new StoredMessage
            {
                Subject = "orders.created",
                Seq = 42,
                Data = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize("hello"))),
                Time = new DateTimeOffset(2026, 8, 15, 10, 30, 0, TimeSpan.Zero),
                Hdrs = null,
            },
        };
#pragma warning restore IL3050, IL2026

        var serializer = DirectGetJsonSerializer<string>.Default;
        var result = NatsStreamMsg<string>.FromStreamResponse(response, serializer);

        Assert.Equal("hello", result.Data);
        Assert.Equal(42UL, result.Sequence);
        Assert.Equal("orders.created", result.Subject);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 10, 30, 0, TimeSpan.Zero), result.Time);
        Assert.Null(result.Headers);
    }

    [Fact]
    public void FromStreamResponse_NullResponse_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => NatsStreamMsg<string>.FromStreamResponse(null!, DirectGetJsonSerializer<string>.Default));
    }

    [Fact]
    public void FromStreamResponse_NullSerializer_ThrowsArgumentNullException()
    {
        var response = new StreamMsgGetResponse
        {
            Message = new StoredMessage
            {
                Subject = "orders.created",
                Seq = 42,
                Data = default,
                Time = new DateTimeOffset(2026, 8, 15, 10, 30, 0, TimeSpan.Zero),
                Hdrs = null,
            },
        };

        Assert.Throws<ArgumentNullException>(() => NatsStreamMsg<string>.FromStreamResponse(response, null!));
    }

    [Fact]
    public void FromStreamResponse_EmptyData_ReturnsNullData()
    {
        var response = new StreamMsgGetResponse
        {
            Message = new StoredMessage
            {
                Subject = "orders.created",
                Seq = 42,
                Data = default,
                Time = new DateTimeOffset(2026, 8, 15, 10, 30, 0, TimeSpan.Zero),
                Hdrs = null,
            },
        };

        var serializer = DirectGetJsonSerializer<string>.Default;
        var result = NatsStreamMsg<string>.FromStreamResponse(response, serializer);

        Assert.Null(result.Data);
        Assert.Equal(42UL, result.Sequence);
        Assert.Equal("orders.created", result.Subject);
    }

    private static NatsHeaders ParseHeaders(string frame)
    {
        var bytes = Encoding.UTF8.GetBytes(frame);
        var parser = new NatsHeaderParser(Encoding.UTF8);
        var headers = new NatsHeaders();
        if (parser.ParseHeaders(new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes)), headers))
        {
            return headers;
        }

        throw new InvalidOperationException("Failed to parse headers");
    }

    private static NatsMsg<string> BuildMsg(NatsHeaders? headers, string? data = null)
    {
        return new NatsMsg<string>(
            subject: "_INBOX.test",
            replyTo: null,
            size: 0,
            headers: headers,
            data: data,
            connection: null,
            flags: NatsMsgFlags.None);
    }
}
