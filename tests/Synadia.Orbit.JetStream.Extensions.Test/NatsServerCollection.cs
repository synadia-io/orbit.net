// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.JetStream.Extensions.Test;

[CollectionDefinition("nats-server")]
public class NatsServerCollection : ICollectionFixture<NatsServerFixture>
{
}
