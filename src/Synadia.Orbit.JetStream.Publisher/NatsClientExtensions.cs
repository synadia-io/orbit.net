// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using NATS.Client.Core;

namespace Synadia.Orbit.JetStream.Publisher;

public static class NatsClientExtensions
{
    public static JetStreamPublisher<T> CreateOrbitJetStreamPublisher<T>(this INatsClient client)
    {
        return new JetStreamPublisher<T>(client.Connection);
    }
}
