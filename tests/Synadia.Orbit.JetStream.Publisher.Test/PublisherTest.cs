// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Publisher.Test;

[Collection("nats-server")]
public class PublisherTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public PublisherTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Get_many_messages()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });

        await connection.ConnectRetryAsync();

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();
        string name = $"{prefix}S1";
        string suject = $"{prefix}s1";

        CancellationToken ct = TestContext.Current.CancellationToken;

        var stream = await js.CreateStreamAsync(new StreamConfig(name, [suject, suject + ".>"]) { AllowDirect = true }, ct);

        for (int i = 0; i < 10; i++)
        {
            await js.PublishAsync(subject: suject, i, cancellationToken: ct);
        }

        JetStreamPublisher<int> publisher = new(connection);
        await publisher.StartAsync(ct);

        var sub = Task.Run(
            async () =>
            {
                int count = 0;
                await foreach (var status in publisher.SubscribeAsync(ct))
                {
                    if (++count == 100)
                    {
                        break;
                    }
                }
            },
            ct);

        for (int i = 0; i < 100; i++)
        {
            await publisher.PublishAsync(subject: suject, data: i, cancellationToken: ct);
        }

        await sub;

        await stream.RefreshAsync(ct);
        _output.WriteLine($"stream.Info.State.Messages: {stream.Info.State.Messages}");
    }
}
