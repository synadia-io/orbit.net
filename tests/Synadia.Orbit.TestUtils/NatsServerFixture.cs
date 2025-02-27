// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;

namespace Synadia.Orbit.TestUtils;

// https://xunit.net/docs/shared-context#collection-fixture
public class NatsServerFixture : IDisposable
{
    private int _next;

    public NatsServerFixture()
        : this(null)
    {
    }

    protected NatsServerFixture(string? config)
    {
        Server = NatsServerProcess.Start(config: config);
    }

    public NatsServerProcess Server { get; }

    public int Port => new Uri(Server.Url).Port;

    public string Url => Server.Url;

    public string GetNextId() => $"test{Interlocked.Increment(ref _next)}";

    public void Dispose()
    {
        Server.Dispose();
    }
}
