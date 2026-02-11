// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.Testing.NatsServerProcessManager.Test;

public class NatsServerProcessTest
{
    [Fact]
    public void Start_and_dispose()
    {
        using var server = NatsServerProcess.Start();
        Assert.StartsWith("nats://127.0.0.1:", server.Url);
    }

    [Fact]
    public async Task StartAsync_and_dispose()
    {
        await using var server = await NatsServerProcess.StartAsync();
        Assert.StartsWith("nats://127.0.0.1:", server.Url);
    }

    [Fact]
    public void Start_without_jetstream()
    {
        using var server = NatsServerProcess.Start(withJs: false);
        Assert.StartsWith("nats://127.0.0.1:", server.Url);
    }
}
