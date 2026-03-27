// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.Testing.GoHarness;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.PCGroups.Test.Elastic;

[Collection("nats-server")]
public class NatsPcgElasticGoInteropTests
{
    // lang=go
    private const string GoConsumerCode =
        """
        package main

        import (
            "bufio"
            "context"
            "fmt"
            "os"
            "strconv"
            "strings"
            "sync/atomic"
            "time"

            "github.com/nats-io/nats.go"
            "github.com/nats-io/nats.go/jetstream"
            "github.com/synadia-io/orbit.go/pcgroups"
        )

        func main() {
            scanner := bufio.NewScanner(os.Stdin)
            if !scanner.Scan() {
                fmt.Fprintln(os.Stderr, "no input")
                os.Exit(1)
            }

            parts := strings.Split(scanner.Text(), "|")
            natsUrl := parts[0]
            streamName := parts[1]
            groupName := parts[2]
            memberName := parts[3]
            expectedCount, _ := strconv.Atoi(parts[4])

            nc, err := nats.Connect(natsUrl)
            if err != nil {
                fmt.Fprintf(os.Stderr, "connect: %v\n", err)
                os.Exit(1)
            }
            defer nc.Close()

            js, err := jetstream.New(nc)
            if err != nil {
                fmt.Fprintf(os.Stderr, "jetstream: %v\n", err)
                os.Exit(1)
            }

            ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
            defer cancel()

            config, err := pcgroups.GetElasticConsumerGroupConfig(ctx, js, streamName, groupName)
            if err != nil {
                fmt.Fprintf(os.Stderr, "get config: %v\n", err)
                os.Exit(1)
            }

            filterDesc := ""
            if len(config.PartitioningFilters) > 0 {
                filterDesc = fmt.Sprintf(",filter=%s,wildcards=%v",
                    config.PartitioningFilters[0].Filter,
                    config.PartitioningFilters[0].PartitioningWildcards)
            }
            fmt.Printf("CONFIG:max_members=%d,filters=%d%s\n",
                config.MaxMembers, len(config.PartitioningFilters), filterDesc)

            var count atomic.Int32
            var subjects []string

            consumeCtx, err := pcgroups.ElasticConsume(ctx, js, streamName, groupName, memberName,
                func(msg jetstream.Msg) {
                    subjects = append(subjects, msg.Subject())
                    msg.Ack()
                    count.Add(1)
                },
                jetstream.ConsumerConfig{
                    AckPolicy: jetstream.AckExplicitPolicy,
                    AckWait:   5 * time.Second,
                })
            if err != nil {
                fmt.Fprintf(os.Stderr, "consume: %v\n", err)
                os.Exit(1)
            }
            defer consumeCtx.Stop()

            deadline := time.After(15 * time.Second)
            for {
                if int(count.Load()) >= expectedCount {
                    break
                }
                select {
                case <-deadline:
                    fmt.Fprintf(os.Stderr, "timeout waiting for messages, got %d/%d\n", count.Load(), expectedCount)
                    os.Exit(1)
                case <-time.After(100 * time.Millisecond):
                }
            }

            fmt.Printf("RECEIVED:count=%d,subjects=%s\n", count.Load(), strings.Join(subjects, ","))
        }
        """;

    // lang=go
    private const string GoCreatorCode =
        """
        package main

        import (
            "bufio"
            "context"
            "fmt"
            "os"
            "strconv"
            "strings"
            "time"

            "github.com/nats-io/nats.go"
            "github.com/nats-io/nats.go/jetstream"
            "github.com/synadia-io/orbit.go/pcgroups"
        )

        func main() {
            scanner := bufio.NewScanner(os.Stdin)
            if !scanner.Scan() {
                fmt.Fprintln(os.Stderr, "no input")
                os.Exit(1)
            }

            parts := strings.Split(scanner.Text(), "|")
            natsUrl := parts[0]
            streamName := parts[1]
            groupName := parts[2]
            subjectPrefix := parts[3]
            messageCount, _ := strconv.Atoi(parts[4])

            nc, err := nats.Connect(natsUrl)
            if err != nil {
                fmt.Fprintf(os.Stderr, "connect: %v\n", err)
                os.Exit(1)
            }
            defer nc.Close()

            js, err := jetstream.New(nc)
            if err != nil {
                fmt.Fprintf(os.Stderr, "jetstream: %v\n", err)
                os.Exit(1)
            }

            ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
            defer cancel()

            _, err = pcgroups.CreateElastic(ctx, js, streamName, groupName, 4,
                []pcgroups.PartitioningFilter{
                    {Filter: subjectPrefix + ".*", PartitioningWildcards: []int{1}},
                }, 0, 0)
            if err != nil {
                fmt.Fprintf(os.Stderr, "create elastic: %v\n", err)
                os.Exit(1)
            }

            _, err = pcgroups.AddMembers(ctx, js, streamName, groupName, []string{"dotnet-worker"})
            if err != nil {
                fmt.Fprintf(os.Stderr, "add members: %v\n", err)
                os.Exit(1)
            }

            fmt.Println("CREATED")

            for i := 0; i < messageCount; i++ {
                subject := fmt.Sprintf("%s.item%d", subjectPrefix, i)
                _, err := js.Publish(ctx, subject, []byte(fmt.Sprintf("payload%d", i)))
                if err != nil {
                    fmt.Fprintf(os.Stderr, "publish: %v\n", err)
                    os.Exit(1)
                }
            }

            fmt.Println("PUBLISHED")

            scanner.Scan()
        }
        """;

    private static readonly string[] GoModules =
    [
        "github.com/synadia-io/orbit.go/pcgroups@v0.2.0",
        "github.com/nats-io/nats.go@v1.39.1",
    ];

    private readonly NatsServerFixture _server;

    public NatsPcgElasticGoInteropTests(NatsServerFixture server) => _server = server;

    [Fact]
    public async Task DotNet_creates_Go_consumes()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var id = Guid.NewGuid().ToString("N");
        var streamName = $"interop-{id}";

        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = [$"ord{id}.*"],
        });

        try
        {
            var groupName = $"cg-{id}";

            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 4,
                partitioningFilters: [new NatsPcgPartitioningFilter($"ord{id}.*", [1])]);

            await js.AddPcgElasticMembersAsync(streamName, groupName, ["go-worker"]);

            for (int i = 0; i < 5; i++)
            {
                await js.PublishAsync($"ord{id}.item{i}", $"payload{i}");
            }

            await using var go = await GoProcess.RunCodeAsync(
                GoConsumerCode,
                logger: msg => { },
                goModules: GoModules);

            await go.WriteLineAsync($"{_server.Url}|{streamName}|{groupName}|go-worker|5");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var configLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(configLine);
            Assert.StartsWith("CONFIG:", configLine);
            Assert.Contains("max_members=4", configLine);
            Assert.Contains("filters=1", configLine);
            Assert.Contains($"filter=ord{id}.*", configLine);

            var resultLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(resultLine);
            Assert.StartsWith("RECEIVED:", resultLine);
            Assert.Contains("count=5", resultLine);

            go.CloseInput();
            await go.WaitForExitAsync(cts.Token);
            Assert.Equal(0, go.ExitCode);

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task Go_creates_DotNet_consumes()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        var js = nats.CreateJetStreamContext();

        var id = Guid.NewGuid().ToString("N");
        var streamName = $"interop-{id}";

        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = [$"evt{id}.*"],
        });

        try
        {
            var groupName = $"cg-{id}";

            await using var go = await GoProcess.RunCodeAsync(
                GoCreatorCode,
                logger: msg => { },
                goModules: GoModules);

            await go.WriteLineAsync($"{_server.Url}|{streamName}|{groupName}|evt{id}|5");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var readyLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(readyLine);
            Assert.Equal("CREATED", readyLine);

            var config = await js.GetPcgElasticConfigAsync(streamName, groupName, cts.Token);
            Assert.Equal(4u, config.MaxMembers);
            Assert.Single(config.PartitioningFilters);
            Assert.Equal($"evt{id}.*", config.PartitioningFilters[0].Filter);
            Assert.Equal([1], config.PartitioningFilters[0].PartitioningWildcards);
            Assert.True(config.IsInMembership("dotnet-worker"));

            var publishedLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(publishedLine);
            Assert.Equal("PUBLISHED", publishedLine);

            var received = new List<string>();
            await foreach (var msg in js.ConsumePcgElasticAsync<string>(
                               streamName, groupName, "dotnet-worker", cancellationToken: cts.Token))
            {
                received.Add(msg.Subject);
                await msg.AckAsync(cancellationToken: cts.Token);
                if (received.Count >= 5)
                {
                    break;
                }
            }

            Assert.Equal(5, received.Count);
            Assert.All(received, s => Assert.StartsWith($"evt{id}.", s));

            go.CloseInput();
            await go.WaitForExitAsync(cts.Token);
            Assert.Equal(0, go.ExitCode);

            await js.DeletePcgElasticAsync(streamName, groupName, cts.Token);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    [Fact]
    public async Task DotNet_creates_empty_filters_Go_consumes()
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        await SkipBelow212Async(nats);
        var js = nats.CreateJetStreamContext();

        var id = Guid.NewGuid().ToString("N");
        var streamName = $"interop-{id}";

        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = [$"efg{id}.*"],
        });

        try
        {
            var groupName = $"cg-{id}";

            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 3,
                partitioningFilters: Array.Empty<NatsPcgPartitioningFilter>());

            await js.AddPcgElasticMembersAsync(streamName, groupName, ["go-worker"]);

            for (int i = 0; i < 3; i++)
            {
                await js.PublishAsync($"efg{id}.item{i}", $"payload{i}");
            }

            await using var go = await GoProcess.RunCodeAsync(
                GoConsumerCode,
                logger: msg => { },
                goModules: GoModules);

            await go.WriteLineAsync($"{_server.Url}|{streamName}|{groupName}|go-worker|3");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var configLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(configLine);
            Assert.StartsWith("CONFIG:", configLine);
            Assert.Contains("max_members=3", configLine);
            Assert.Contains("filters=0", configLine);

            var resultLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(resultLine);
            Assert.StartsWith("RECEIVED:", resultLine);
            Assert.Contains("count=3", resultLine);

            go.CloseInput();
            await go.WaitForExitAsync(cts.Token);
            Assert.Equal(0, go.ExitCode);

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    private static async Task SkipBelow212Async(NatsConnection nats)
    {
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(
            nats.HasMinServerVersion(2, 12),
            $"Server version {nats.ServerInfo?.Version} does not support empty-wildcards full-subject partitioning (requires 2.12+)");
    }
}
