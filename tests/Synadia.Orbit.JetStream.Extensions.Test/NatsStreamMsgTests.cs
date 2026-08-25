// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream.Models;

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
    public void FromDirect_NullMsg_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => NatsStreamMsg<string>.FromDirect(default));
    }

    [Fact]
    public void FromDirect_NullData_ReturnsNullData()
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
}
