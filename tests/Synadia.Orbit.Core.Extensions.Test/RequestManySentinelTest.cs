// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.Core.Extensions.Test;

[Collection("nats-server")]
public class RequestManySentinelTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public RequestManySentinelTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Sentinel_stops_on_data_condition()
    {
        var opts = new NatsOpts
        {
            Url = _server.Url,
            SerializerRegistry = NatsJsonSerializerRegistry.Default,
        };
        await using var nats = new NatsConnection(opts);
        await nats.ConnectAsync();

        var prefix = _server.GetNextId();
        var subject = $"{prefix}.request";
        var ct = TestContext.Current.CancellationToken;

        // Start a responder that sends multiple responses
        var responderTask = Task.Run(
            async () =>
            {
                await foreach (var msg in nats.SubscribeAsync<int>(subject, cancellationToken: ct))
                {
                    // Send 5 responses, the last one with IsLast = true
                    for (var i = 1; i <= 5; i++)
                    {
                        await msg.ReplyAsync(new Response { Value = i, IsLast = i == 5 }, cancellationToken: ct);
                    }

                    break;
                }
            },
            ct);

        // Small delay to ensure subscriber is ready
        await Task.Delay(100, ct);

        var responses = new List<Response>();
        await foreach (var msg in nats.RequestManyWithSentinelAsync<int, Response>(
                           subject: subject,
                           data: 42,
                           sentinel: m => m.Data?.IsLast == true,
                           replyOpts: new NatsSubOpts { Timeout = TimeSpan.FromSeconds(5) },
                           cancellationToken: ct))
        {
            _output.WriteLine($"Received: Value={msg.Data?.Value}, IsLast={msg.Data?.IsLast}");
            responses.Add(msg.Data!);
        }

        await responderTask;

        // Should receive 4 messages (1-4), sentinel message (5) should not be yielded
        Assert.Equal(4, responses.Count);
        Assert.Equal([1, 2, 3, 4], responses.Select(r => r.Value).ToArray());
    }

    [Fact]
    public async Task Sentinel_stops_on_header_condition()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        await nats.ConnectAsync();

        var prefix = _server.GetNextId();
        var subject = $"{prefix}.request";
        var ct = TestContext.Current.CancellationToken;

        // Start a responder that sends multiple responses with headers
        var responderTask = Task.Run(
            async () =>
            {
                await foreach (var msg in nats.SubscribeAsync<int>(subject, cancellationToken: ct))
                {
                    for (var i = 1; i <= 3; i++)
                    {
                        var headers = new NatsHeaders();
                        if (i == 3)
                        {
                            headers["X-Done"] = "true";
                        }

                        await msg.ReplyAsync(i, headers: headers, cancellationToken: ct);
                    }

                    break;
                }
            },
            ct);

        await Task.Delay(100, ct);

        var responses = new List<int>();
        await foreach (var msg in nats.RequestManyWithSentinelAsync<int, int>(
                           subject: subject,
                           data: 0,
                           sentinel: m => m.Headers?["X-Done"].ToString() == "true",
                           replyOpts: new NatsSubOpts { Timeout = TimeSpan.FromSeconds(5) },
                           cancellationToken: ct))
        {
            _output.WriteLine($"Received: {msg.Data}");
            responses.Add(msg.Data);
        }

        await responderTask;

        // Should receive 2 messages (1, 2), sentinel message (3) should not be yielded
        Assert.Equal(2, responses.Count);
        Assert.Equal([1, 2], responses.ToArray());
    }

    [Fact]
    public async Task Sentinel_with_empty_message_still_works()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        await nats.ConnectAsync();

        var prefix = _server.GetNextId();
        var subject = $"{prefix}.request";
        var ct = TestContext.Current.CancellationToken;

        // Start a responder that sends responses and ends with empty message
        var responderTask = Task.Run(
            async () =>
            {
                await foreach (var msg in nats.SubscribeAsync<int>(subject, cancellationToken: ct))
                {
                    await msg.ReplyAsync(1, cancellationToken: ct);
                    await msg.ReplyAsync(2, cancellationToken: ct);

                    // Send empty message as sentinel
                    await nats.PublishAsync<int?>(msg.ReplyTo!, null, cancellationToken: ct);
                    break;
                }
            },
            ct);

        await Task.Delay(100, ct);

        var responses = new List<int>();

        // Use a sentinel that checks for default/null value (empty message)
        await foreach (var msg in nats.RequestManyWithSentinelAsync<int, int>(
                           subject: subject,
                           data: 0,
                           sentinel: m => m.Data == default,
                           replyOpts: new NatsSubOpts { Timeout = TimeSpan.FromSeconds(5) },
                           cancellationToken: ct))
        {
            _output.WriteLine($"Received: {msg.Data}");
            responses.Add(msg.Data);
        }

        await responderTask;

        Assert.Equal(2, responses.Count);
        Assert.Equal([1, 2], responses.ToArray());
    }

    [Fact]
    public async Task Sentinel_with_msg_overload()
    {
        var opts = new NatsOpts
        {
            Url = _server.Url,
            SerializerRegistry = NatsJsonSerializerRegistry.Default,
        };
        await using var nats = new NatsConnection(opts);
        await nats.ConnectAsync();

        var prefix = _server.GetNextId();
        var subject = $"{prefix}.request";
        var ct = TestContext.Current.CancellationToken;

        var responderTask = Task.Run(
            async () =>
            {
                await foreach (var msg in nats.SubscribeAsync<int>(subject, cancellationToken: ct))
                {
                    for (var i = 1; i <= 3; i++)
                    {
                        await msg.ReplyAsync(new Response { Value = i, IsLast = i == 3 }, cancellationToken: ct);
                    }

                    break;
                }
            },
            ct);

        await Task.Delay(100, ct);

        var requestMsg = new NatsMsg<int>
        {
            Subject = subject,
            Data = 42,
            Headers = new NatsHeaders { ["X-Request-Id"] = "123" },
        };

        var responses = new List<Response>();
        await foreach (var msg in nats.RequestManyWithSentinelAsync<int, Response>(
                           msg: requestMsg,
                           sentinel: m => m.Data?.IsLast == true,
                           replyOpts: new NatsSubOpts { Timeout = TimeSpan.FromSeconds(5) },
                           cancellationToken: ct))
        {
            responses.Add(msg.Data!);
        }

        await responderTask;

        Assert.Equal(2, responses.Count);
        Assert.Equal([1, 2], responses.Select(r => r.Value).ToArray());
    }

    private class Response
    {
        public int Value { get; set; }

        public bool IsLast { get; set; }
    }
}
