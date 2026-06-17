// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.Counters.Test;

[Collection("nats-server")]
public class CounterSourcesTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public CounterSourcesTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Counter_with_multi_level_sources()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        await connection.ConnectRetryAsync();
        Assert.SkipUnless(connection.HasMinServerVersion(2, 12), $"Server version {connection.ServerInfo?.Version} does not support counters (requires 2.12+)");

        var js = connection.CreateJetStreamContext();
        var prefix = _server.GetNextId();
        var ct = TestContext.Current.CancellationToken;

        // Create Spanish stream
        await js.CreateStreamAsync(
            new StreamConfig($"{prefix}_ES", [$"{prefix}.es.*"]) { AllowMsgCounter = true, AllowDirect = true }, ct);

        // Create Polish stream
        await js.CreateStreamAsync(
            new StreamConfig($"{prefix}_PL", [$"{prefix}.pl.*"]) { AllowMsgCounter = true, AllowDirect = true }, ct);

        // Create EU stream aggregating ES and PL
        await js.CreateStreamAsync(
            new StreamConfig($"{prefix}_EU", [$"{prefix}.eu.*"])
            {
                AllowMsgCounter = true,
                AllowDirect = true,
                Sources =
                [
                    new StreamSource
                    {
                        Name = $"{prefix}_ES",
                        SubjectTransforms =
                        [
                            new SubjectTransform
                            {
                                Src = $"{prefix}.es.>",
                                Dest = $"{prefix}.eu.>",
                            },
                        ],
                    },
                    new StreamSource
                    {
                        Name = $"{prefix}_PL",
                        SubjectTransforms =
                        [
                            new SubjectTransform
                            {
                                Src = $"{prefix}.pl.>",
                                Dest = $"{prefix}.eu.>",
                            },
                        ],
                    },
                ],
            },
            ct);

        // Create global stream aggregating EU
        await js.CreateStreamAsync(
            new StreamConfig($"{prefix}_GLOBAL", [$"{prefix}.g.*"])
            {
                AllowMsgCounter = true,
                AllowDirect = true,
                Sources =
                [
                    new StreamSource
                    {
                        Name = $"{prefix}_EU",
                        SubjectTransforms =
                        [
                            new SubjectTransform
                            {
                                Src = $"{prefix}.eu.>",
                                Dest = $"{prefix}.g.>",
                            },
                        ],
                    },
                ],
            },
            ct);

        var esCounter = await js.GetCounterAsync($"{prefix}_ES", ct);
        var plCounter = await js.GetCounterAsync($"{prefix}_PL", ct);
        var euCounter = await js.GetCounterAsync($"{prefix}_EU", ct);
        var globalCounter = await js.GetCounterAsync($"{prefix}_GLOBAL", ct);

        // Add to local counters
        await esCounter.AddAsync($"{prefix}.es.hits", new BigInteger(100), ct);
        await esCounter.AddAsync($"{prefix}.es.views", new BigInteger(200), ct);
        await plCounter.AddAsync($"{prefix}.pl.hits", new BigInteger(150), ct);

        // Wait for the source aggregation to converge rather than a fixed delay,
        // which flakes on slow runners.
        var euHits = await WaitForCounterValueAsync(euCounter, $"{prefix}.eu.hits", new BigInteger(250), ct);
        _output.WriteLine($"EU hits: {euHits.Value}");
        Assert.Equal(new BigInteger(250), euHits.Value);

        // Verify sources
        Assert.NotNull(euHits.Sources);
        Assert.Equal(2, euHits.Sources.Count);
        Assert.True(euHits.Sources.ContainsKey($"{prefix}_ES"));
        Assert.True(euHits.Sources.ContainsKey($"{prefix}_PL"));
        Assert.Equal(new BigInteger(100), euHits.Sources[$"{prefix}_ES"][$"{prefix}.es.hits"]);
        Assert.Equal(new BigInteger(150), euHits.Sources[$"{prefix}_PL"][$"{prefix}.pl.hits"]);

        // Check EU views
        var euViews = await euCounter.LoadAsync($"{prefix}.eu.views", ct);
        Assert.Equal(new BigInteger(200), euViews);

        // Check EU GetMany
        var euEntries = new Dictionary<string, CounterEntry>();
        await foreach (var entry in euCounter.GetManyAsync([$"{prefix}.eu.>"], ct))
        {
            euEntries[entry.Subject] = entry;
            _output.WriteLine($"EU GetMany: {entry.Subject} = {entry.Value}");
        }

        Assert.Equal(2, euEntries.Count);
        Assert.Equal(new BigInteger(250), euEntries[$"{prefix}.eu.hits"].Value);
        Assert.Equal(new BigInteger(200), euEntries[$"{prefix}.eu.views"].Value);

        // Check global aggregation (second-level source, may converge after EU)
        var globalHits = await WaitForCounterValueAsync(globalCounter, $"{prefix}.g.hits", new BigInteger(250), ct);
        _output.WriteLine($"Global hits: {globalHits.Value}");
        Assert.Equal(new BigInteger(250), globalHits.Value);

        // Global should have EU as source
        Assert.NotNull(globalHits.Sources);
        Assert.True(globalHits.Sources.ContainsKey($"{prefix}_EU"));
        Assert.Equal(new BigInteger(250), globalHits.Sources[$"{prefix}_EU"][$"{prefix}.eu.hits"]);

        var globalViews = await globalCounter.LoadAsync($"{prefix}.g.views", ct);
        Assert.Equal(new BigInteger(200), globalViews);
    }

    // Source aggregation replicates asynchronously, so poll until the counter
    // reaches the expected value instead of waiting a fixed amount of time. The
    // subject is not initialized until the first replicated message arrives, so
    // a miss throws until then; treat that as not-yet-converged.
    private static async Task<CounterEntry> WaitForCounterValueAsync(
        NatsJSCounter counter, string subject, BigInteger expected, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var entry = await counter.GetAsync(subject, cancellationToken).ConfigureAwait(false);
                if (entry.Value == expected)
                {
                    return entry;
                }
            }
            catch (NatsCounterException e) when (e.Code == 2001)
            {
                // NoCounterForSubject: subject not initialized yet; source replication has not arrived.
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        // Final read so the caller's assertion surfaces a clear value on timeout.
        return await counter.GetAsync(subject, cancellationToken).ConfigureAwait(false);
    }
}
