// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Synadia.Orbit.JetStream.Publisher;

public static class NatsClientExtensions
{
    public static JetStreamPublisher<T> CreateOrbitJetStreamPublisher<T>(this INatsClient client)
    {
        return new JetStreamPublisher<T>(client.Connection);
    }

    /// <summary>
    /// Creates a fast-ingest batch publisher for high-throughput, non-atomic batch publishing.
    /// Requires NATS Server 2.14+ and <c>StreamConfig.AllowBatchPublish = true</c>.
    /// </summary>
    /// <param name="js">The JetStream context.</param>
    /// <param name="opts">Optional publisher options.</param>
    /// <returns>The fast-ingest batch publisher.</returns>
    public static NatsJSFastPublisher CreateOrbitFastPublisher(this INatsJSContext js, NatsJSFastPublisherOpts? opts = null)
    {
        return new NatsJSFastPublisher(js, opts);
    }
}
