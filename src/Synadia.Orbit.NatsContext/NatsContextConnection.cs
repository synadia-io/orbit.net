// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext;

/// <summary>
/// Represents the result of connecting to a NATS server using a CLI context, containing the connection and settings.
/// </summary>
/// <param name="Connection">The connected <see cref="NatsConnection"/>.</param>
/// <param name="Settings">The parsed <see cref="NatsContextSettings"/>.</param>
public sealed record NatsContextConnection(NatsConnection Connection, NatsContextSettings Settings) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
