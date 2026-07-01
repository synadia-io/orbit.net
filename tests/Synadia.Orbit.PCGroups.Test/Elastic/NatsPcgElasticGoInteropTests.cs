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

    // Consumes continuously until stdin is closed, answering REPORT requests with the
    // running count so the .NET side can coordinate the phases of a rebalance test.
    // lang=go
    private const string GoContinuousConsumerCode =
        """
        package main

        import (
            "bufio"
            "context"
            "fmt"
            "os"
            "strings"
            "sync"
            "time"

            "github.com/nats-io/nats.go"
            "github.com/nats-io/nats.go/jetstream"
            "github.com/synadia-io/orbit.go/pcgroups"
        )

        func main() {
            reader := bufio.NewReader(os.Stdin)
            first, err := reader.ReadString('\n')
            if err != nil {
                fmt.Fprintln(os.Stderr, "no input")
                os.Exit(1)
            }

            parts := strings.Split(strings.TrimSpace(first), "|")
            natsUrl := parts[0]
            streamName := parts[1]
            groupName := parts[2]
            memberName := parts[3]

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

            ctx := context.Background()

            var mu sync.Mutex
            var subjects []string

            consumeCtx, err := pcgroups.ElasticConsume(ctx, js, streamName, groupName, memberName,
                func(msg jetstream.Msg) {
                    mu.Lock()
                    subjects = append(subjects, msg.Subject())
                    mu.Unlock()
                    msg.Ack()
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

            fmt.Println("READY")

            for {
                line, rerr := reader.ReadString('\n')
                cmd := strings.TrimSpace(line)
                if cmd == "REPORT" {
                    mu.Lock()
                    fmt.Printf("COUNT:%d\n", len(subjects))
                    mu.Unlock()
                }
                if rerr != nil || cmd == "STOP" {
                    break
                }
            }

            mu.Lock()
            fmt.Printf("FINAL:count=%d,subjects=%s\n", len(subjects), strings.Join(subjects, ","))
            mu.Unlock()
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
        await SkipBelow211Async(nats);
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
        await SkipBelow211Async(nats);
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

    [Fact]
    public async Task Mixed_members_split_then_DotNet_takes_over_when_Go_member_removed()
    {
        // A .NET member and a Go member share one elastic group and split partitions.
        // When the Go member is removed, the .NET member must take over its partition
        // (delete and recreate its consumer) and receive subsequent messages, with no
        // overlap between the two members' deliveries.
        await using var nats = new NatsConnection(new NatsOpts { Url = _server.Url });
        await SkipBelow211Async(nats);
        var js = nats.CreateJetStreamContext();

        var id = Guid.NewGuid().ToString("N");
        var streamName = $"interop-{id}";

        await js.CreateStreamAsync(new StreamConfig
        {
            Name = streamName,
            Subjects = [$"mix{id}.*"],
        });

        try
        {
            var groupName = $"cg-{id}";

            await js.CreatePcgElasticAsync(
                streamName,
                groupName,
                maxNumMembers: 2,
                partitioningFilters: [new NatsPcgPartitioningFilter($"mix{id}.*", [1])]);

            await js.AddPcgElasticMembersAsync(streamName, groupName, ["dotnet-worker", "go-worker"]);

            const int phase1 = 40;
            for (int i = 0; i < phase1; i++)
            {
                await js.PublishAsync($"mix{id}.k{i}", $"p{i}");
            }

            await using var go = await GoProcess.RunCodeAsync(
                GoContinuousConsumerCode,
                logger: msg => { },
                goModules: GoModules);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            await go.WriteLineAsync($"{_server.Url}|{streamName}|{groupName}|go-worker", cts.Token);
            Assert.Equal("READY", await go.ReadLineAsync(cts.Token));

            var dotnetSubjects = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
            using var consumeCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            var dotnetTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var msg in js.ConsumePcgElasticAsync<string>(streamName, groupName, "dotnet-worker", cancellationToken: consumeCts.Token))
                    {
                        dotnetSubjects.TryAdd(msg.Subject, 0);
                        await msg.AckAsync(cancellationToken: CancellationToken.None);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            async Task<int> GoCountAsync()
            {
                await go.WriteLineAsync("REPORT", cts.Token);
                var line = await go.ReadLineAsync(cts.Token);
                Assert.NotNull(line);
                Assert.StartsWith("COUNT:", line);
                return int.Parse(line!.Substring("COUNT:".Length));
            }

            // Phase 1: both members consume concurrently and together cover all messages.
            int goCount = 0;
            while (!cts.IsCancellationRequested)
            {
                goCount = await GoCountAsync();
                if (dotnetSubjects.Count + goCount >= phase1)
                {
                    break;
                }

                await Task.Delay(300, cts.Token);
            }

            Assert.Equal(phase1, dotnetSubjects.Count + goCount);
            Assert.True(dotnetSubjects.Count > 0, ".NET member should own a partition");
            Assert.True(goCount > 0, "Go member should own a partition");
            int dotnetPhase1 = dotnetSubjects.Count;

            // Remove the Go member; .NET gains its partition. Give both sides time to
            // process the membership change before publishing so the new messages are
            // not delivered to the departing member.
            await js.DeletePcgElasticMembersAsync(streamName, groupName, ["go-worker"], cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

            const int phase2 = 20;
            for (int i = 0; i < phase2; i++)
            {
                await js.PublishAsync($"mix{id}.r{i}", $"q{i}");
            }

            // .NET now owns all partitions and must receive every phase-2 message.
            while (dotnetSubjects.Count < dotnetPhase1 + phase2 && !cts.IsCancellationRequested)
            {
                await Task.Delay(300, cts.Token);
            }

            Assert.Equal(dotnetPhase1 + phase2, dotnetSubjects.Count);

            // The removed Go member received nothing further.
            Assert.Equal(goCount, await GoCountAsync());

            consumeCts.Cancel();
            await dotnetTask;

            go.CloseInput();
            var finalLine = await go.ReadLineAsync(cts.Token);
            Assert.NotNull(finalLine);
            Assert.StartsWith("FINAL:", finalLine);
            await go.WaitForExitAsync(cts.Token);
            Assert.Equal(0, go.ExitCode);

            await js.DeletePcgElasticAsync(streamName, groupName);
        }
        finally
        {
            await js.DeleteStreamAsync(streamName);
        }
    }

    private static async Task SkipBelow211Async(NatsConnection nats)
    {
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(
            nats.HasMinServerVersion(2, 11),
            $"Server version {nats.ServerInfo?.Version} does not support priority groups (requires 2.11+)");
    }

    private static async Task SkipBelow212Async(NatsConnection nats)
    {
        await nats.ConnectRetryAsync();
        Assert.SkipUnless(
            nats.HasMinServerVersion(2, 12),
            $"Server version {nats.ServerInfo?.Version} does not support empty-wildcards full-subject partitioning (requires 2.12+)");
    }
}
