// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using System.Diagnostics;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.JetStream.Publisher;

namespace OrbitPublish;

public class CmdCompare
{
    private const string StreamName = "BENCH";
    private const string Subject = "bench";
    private const int NumberOfMessages = 200_000;
    private const int PayloadSize = 128;
    private const int Iterations = 3;
    private const int FastBatchSize = 5_000;
    private const int ConcurrentBatchSize = 100;

    public static async Task<int> Run()
    {
        await using var client = new NatsClient();
        var js = client.CreateJetStreamContext();

        var streamConfig = new StreamConfig(StreamName, [$"{Subject}", $"{Subject}.>"])
        {
            AllowBatchPublish = true,
        };

        try
        {
            await js.CreateStreamAsync(streamConfig);
        }
        catch (NatsJSApiException)
        {
            await js.UpdateStreamAsync(streamConfig);
        }

        Console.WriteLine($"Stream '{StreamName}' ready (AllowBatchPublish=true)");
        Console.WriteLine($"Messages per run: {NumberOfMessages:N0}, payload: {PayloadSize} bytes, iterations: {Iterations}");
        Console.WriteLine();

        var stream = await js.GetStreamAsync(StreamName);
        var payload = new byte[PayloadSize];

        var backends = new (string Name, Func<INatsJSContext, byte[], Task<TimeSpan>> Run)[]
        {
            ("Serial", RunSerialAsync),
            ("Concurrent", RunConcurrentAsync),
            ("Publisher", RunPublisherAsync),
            ("FastPublisher", RunFastPublisherAsync),
        };

        var results = new List<(string Name, double[] Throughputs)>();

        foreach (var (name, runner) in backends)
        {
            Console.WriteLine($"--- {name} ---");
            var throughputs = new double[Iterations];
            for (int i = 0; i < Iterations; i++)
            {
                await stream.PurgeAsync(new StreamPurgeRequest { Keep = 0 });
                var elapsed = await runner(js, payload);
                var rate = NumberOfMessages / elapsed.TotalSeconds;
                throughputs[i] = rate;
                Console.WriteLine($"  run {i + 1}: {elapsed} {rate:N0} msgs/sec");
            }

            results.Add((name, throughputs));
            Console.WriteLine();
        }

        PrintTable(results);
        return 0;
    }

    private static async Task<TimeSpan> RunSerialAsync(INatsJSContext js, byte[] payload)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < NumberOfMessages; i++)
        {
            var ack = await js.PublishAsync(Subject, payload);
            ack.EnsureSuccess();
        }

        sw.Stop();
        return sw.Elapsed;
    }

    private static async Task<TimeSpan> RunConcurrentAsync(INatsJSContext js, byte[] payload)
    {
        var futures = new NatsJSPublishConcurrentFuture[ConcurrentBatchSize];
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < NumberOfMessages / ConcurrentBatchSize; i++)
        {
            for (int j = 0; j < ConcurrentBatchSize; j++)
            {
                futures[j] = await js.PublishConcurrentAsync(Subject, payload);
            }

            foreach (var future in futures)
            {
                await using (future)
                {
                    var ack = await future.GetResponseAsync();
                    ack.EnsureSuccess();
                }
            }
        }

        sw.Stop();
        return sw.Elapsed;
    }

    private static async Task<TimeSpan> RunPublisherAsync(INatsJSContext js, byte[] payload)
    {
        using var cts = new CancellationTokenSource();
        var publisher = js.Connection.CreateOrbitJetStreamPublisher<byte[]>();
        await publisher.StartAsync(cts.Token);

        var sub = Task.Run(
            async () =>
            {
                int count = 0;
                await foreach (var status in publisher.SubscribeAsync(cts.Token))
                {
                    count++;
                    if (count == NumberOfMessages)
                    {
                        await cts.CancelAsync();
                        return;
                    }
                }
            },
            cts.Token);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < NumberOfMessages; i++)
        {
            await publisher.PublishAsync(Subject, payload, cancellationToken: CancellationToken.None);
        }

        try
        {
            await sub;
        }
        catch (OperationCanceledException)
        {
        }

        sw.Stop();
        return sw.Elapsed;
    }

    private static async Task<TimeSpan> RunFastPublisherAsync(INatsJSContext js, byte[] payload)
    {
        var sw = Stopwatch.StartNew();
        int sent = 0;
        while (sent < NumberOfMessages)
        {
            int remaining = NumberOfMessages - sent;
            int chunk = Math.Min(FastBatchSize, remaining);

            await using var batch = js.CreateOrbitFastPublisher();
            for (int i = 0; i < chunk - 1; i++)
            {
                await batch.AddAsync(Subject, payload);
            }

            await batch.CommitAsync(Subject, payload);
            sent += chunk;
        }

        sw.Stop();
        return sw.Elapsed;
    }

    private static void PrintTable(List<(string Name, double[] Throughputs)> results)
    {
        Console.WriteLine("=== Comparison (msgs/sec) ===");
        Console.WriteLine($"{"Backend",-16} {"Min",12} {"Median",12} {"Max",12}  Relative (vs Serial median)");

        double serialMedian = 0;
        foreach (var (name, t) in results)
        {
            if (name == "Serial")
            {
                serialMedian = Median(t);
                break;
            }
        }

        foreach (var (name, t) in results)
        {
            var sorted = (double[])t.Clone();
            Array.Sort(sorted);
            double min = sorted[0];
            double max = sorted[^1];
            double median = Median(sorted);
            string relative = serialMedian > 0 ? $"{median / serialMedian:F2}x" : "n/a";
            Console.WriteLine($"{name,-16} {min,12:N0} {median,12:N0} {max,12:N0}  {relative}");
        }
    }

    private static double Median(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        int n = sorted.Length;
        return n % 2 == 0 ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0 : sorted[n / 2];
    }
}
