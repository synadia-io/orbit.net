using Xunit;

namespace Synadia.Orbit.TestUtils;

using System;
using System.Threading;

public class BaseNatsServerFixture : IDisposable
{
    private readonly NatsServerProcess _server;
    private int _next;

    protected BaseNatsServerFixture(string? config = default) => _server = NatsServerProcess.Start(config: config);

    public string Url => _server.Url;

    public string GetNextId() => $"test{Interlocked.Increment(ref _next)}";

    public void Dispose() => _server.Dispose();
}


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
