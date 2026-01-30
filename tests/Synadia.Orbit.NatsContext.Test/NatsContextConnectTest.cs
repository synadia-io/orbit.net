// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.NatsContext.Test;

[Collection("nats-server")]
public class NatsContextConnectTest
{
    private readonly NatsServerFixture _server;

    public NatsContextConnectTest(NatsServerFixture server) => _server = server;

    [Fact]
    public async Task ConnectAsync_from_file_path()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var contextFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(contextFile, $@"{{""url"": ""{_server.Url}""}}");

            var (connection, settings) = await NatsContext.ConnectAsync(contextFile);
            await using (connection)
            {
                await connection.PingAsync(TestContext.Current.CancellationToken);
                Assert.Equal(_server.Url, settings.Url);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ConnectAsync_with_configureOpts_callback()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var contextFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(contextFile, $@"{{""url"": ""{_server.Url}""}}");

            var (connection, _) = await NatsContext.ConnectAsync(contextFile, opts => opts with
            {
                Name = "custom-client-name",
            });
            await using (connection)
            {
                await connection.PingAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ConnectAsync_from_named_context()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);
            File.WriteAllText(
                Path.Combine(contextDir, "dev.json"),
                $@"{{""url"": ""{_server.Url}""}}");

            var (connection, settings) = await NatsContext.ConnectAsync("dev");
            await using (connection)
            {
                await connection.PingAsync(TestContext.Current.CancellationToken);
                Assert.Equal(_server.Url, settings.Url);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }
}
