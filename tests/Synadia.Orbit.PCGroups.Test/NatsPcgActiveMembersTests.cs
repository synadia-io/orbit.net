// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.PCGroups.Static;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.PCGroups.Test;

// Active member listing and step-down look up each member's own consumer:
// "{member}" on the work-queue stream for elastic groups, "{group}-{member}"
// on the source stream for static groups. These are the names orbit.go uses.
[Collection("nats-server")]
public class NatsPcgActiveMembersTests
{
    private readonly NatsServerFixture _server;

    public NatsPcgActiveMembersTests(NatsServerFixture server) => _server = server;

    [Fact]
    public async Task Elastic_ListActiveMembers_And_StepDown()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var id = Guid.NewGuid().ToString("N");
        var streamName = $"test-stream-{id}";
        var groupName = $"test-group-{id}";
        var subject = $"am{id}";

        await js.CreateStreamAsync(new StreamConfig { Name = streamName, Subjects = [$"{subject}.*"] });

        try
        {
            await js.CreatePcgElasticAsync(streamName, groupName, 3, [new NatsPcgPartitioningFilter($"{subject}.*", [1])]);

            // Nothing is consuming yet.
            Assert.Empty(await ToListAsync(js.ListPcgElasticActiveMembersAsync(streamName, groupName)));

            // "c" is a member that never starts consuming.
            await js.AddPcgElasticMembersAsync(streamName, groupName, ["a", "b", "c"]);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var received = 0;
            var consumers = new[] { "a", "b" }.Select(member => ConsumeAsync(
                js.ConsumePcgElasticAsync<string>(streamName, groupName, member, cancellationToken: cts.Token),
                () => Interlocked.Increment(ref received),
                cts.Token)).ToArray();

            // Publish to many keys so that "c" is the only member left with unconsumed messages.
            await PublishAsync(js, subject, 0, 30);
            await WaitAsync(() => Volatile.Read(ref received) > 0, "messages", cts.Token);

            await WaitAsync(
                async () => (await ToListAsync(js.ListPcgElasticActiveMembersAsync(streamName, groupName))).Count == 2,
                "active members",
                cts.Token);
            var active = await ToListAsync(js.ListPcgElasticActiveMembersAsync(streamName, groupName));
            active.Sort(StringComparer.Ordinal);
            Assert.Equal(["a", "b"], active);

            Assert.Equal((true, true), await js.IsInPcgElasticMembershipAndActiveAsync(streamName, groupName, "a"));
            Assert.Equal((true, false), await js.IsInPcgElasticMembershipAndActiveAsync(streamName, groupName, "c"));
            Assert.Equal((false, false), await js.IsInPcgElasticMembershipAndActiveAsync(streamName, groupName, "nope"));

            // Step-down unpins the member's consumer; the same instance re-pins and keeps going.
            await js.PcgElasticMemberStepDownAsync(streamName, groupName, "a");
            var before = Volatile.Read(ref received);
            await PublishAsync(js, subject, 30, 30);
            await WaitAsync(() => Volatile.Read(ref received) > before, "messages after step-down", cts.Token);

            var ex = await Assert.ThrowsAsync<NatsJSApiException>(() => js.PcgElasticMemberStepDownAsync(streamName, groupName, "c"));
            Assert.Equal(404, ex.Error.Code);

            cts.Cancel();
            await Task.WhenAll(consumers);
            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Static_ListActiveMembers_And_StepDown(bool useMappings)
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var id = Guid.NewGuid().ToString("N");
        var streamName = $"test-stream-{id}";
        var groupName = $"test-group-{id}";
        var subject = $"sm{id}";

        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = [$"{subject}.*"],
            SubjectTransform = new SubjectTransform
            {
                Src = $"{subject}.*",
                Dest = $"{{{{partition(2,1)}}}}.{subject}.{{{{wildcard(1)}}}}",
            },
        });

        try
        {
            // "s2" is a member that never starts consuming.
            if (useMappings)
            {
                await js.CreatePcgStaticAsync(
                    streamName,
                    groupName,
                    2,
                    [$"{subject}.*"],
                    memberMappings: [new NatsPcgMemberMapping("s1", [0]), new NatsPcgMemberMapping("s2", [1])]);
            }
            else
            {
                await js.CreatePcgStaticAsync(streamName, groupName, 2, [$"{subject}.*"], ["s1", "s2"]);
            }

            Assert.Empty(await ToListAsync(js.ListPcgStaticActiveMembersAsync(streamName, groupName)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var received = 0;
            var consumer = ConsumeAsync(
                js.ConsumePcgStaticAsync<string>(streamName, groupName, "s1", cancellationToken: cts.Token),
                () => Interlocked.Increment(ref received),
                cts.Token);

            await PublishAsync(js, subject, 0, 30);
            await WaitAsync(() => Volatile.Read(ref received) > 0, "messages", cts.Token);

            // Active means the member's consumer has a pull outstanding, so wait for idle.
            await WaitAsync(
                async () => (await ToListAsync(js.ListPcgStaticActiveMembersAsync(streamName, groupName))).Count == 1,
                "active members",
                cts.Token);
            Assert.Equal(["s1"], await ToListAsync(js.ListPcgStaticActiveMembersAsync(streamName, groupName)));

            await js.PcgStaticMemberStepDownAsync(streamName, groupName, "s1");
            var before = Volatile.Read(ref received);
            await PublishAsync(js, subject, 30, 30);
            await WaitAsync(() => Volatile.Read(ref received) > before, "messages after step-down", cts.Token);

            var ex = await Assert.ThrowsAsync<NatsJSApiException>(() => js.PcgStaticMemberStepDownAsync(streamName, groupName, "s2"));
            Assert.Equal(404, ex.Error.Code);

            cts.Cancel();
            await consumer;
            await js.DeletePcgStaticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    private static async Task PublishAsync(INatsJSContext js, string subject, int from, int count)
    {
        for (var i = from; i < from + count; i++)
        {
            await js.PublishAsync($"{subject}.key{i}", $"payload-{i}");
        }
    }

    private static Task ConsumeAsync<T>(IAsyncEnumerable<NatsPcgMsg<T>> messages, Action onMessage, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in messages)
                {
                    onMessage();
                    await msg.AckAsync(cancellationToken: CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
        });
    }

    private static async Task<List<string>> ToListAsync(IAsyncEnumerable<string> items)
    {
        var list = new List<string>();
        await foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private static Task WaitAsync(Func<bool> condition, string what, CancellationToken ct)
        => WaitAsync(() => Task.FromResult(condition()), what, ct);

    private static async Task WaitAsync(Func<Task<bool>> condition, string what, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(200, ct);
        }

        Assert.Fail($"timed out waiting for {what}");
    }
}
