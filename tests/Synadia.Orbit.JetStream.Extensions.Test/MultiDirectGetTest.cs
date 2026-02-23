// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Extensions.Models;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Extensions.Test;

[Collection("nats-server")]
public class MultiDirectGetTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public MultiDirectGetTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Get_many_messages()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });

        await connection.ConnectRetryAsync();

        // TODO: do proper version check
        if (!connection.ServerInfo!.Version.StartsWith("2.11."))
        {
            _output.WriteLine($"Unsupported server version: {connection.ServerInfo!.Version}");
            return;
        }

        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();
        string name = $"{prefix}S1";
        string suject = $"{prefix}s1";

        CancellationToken ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig(name, [suject]) { AllowDirect = true }, ct);

        for (int i = 0; i < 10; i++)
        {
            await js.PublishAsync(subject: suject, i, cancellationToken: ct);
        }

        StreamMsgBatchGetRequest request = new()
        {
            Batch = 8,
            Seq = 1,
        };

        int count = 0;
        await foreach (NatsMsg<int> msg in js.GetBatchDirectAsync<int>(name, request, cancellationToken: ct))
        {
            Assert.Equal(count++, msg.Data);
            _output.WriteLine($"GetBatchDirectAsync: {msg.Data}");
        }

        Assert.Equal(8, count);
    }
}
