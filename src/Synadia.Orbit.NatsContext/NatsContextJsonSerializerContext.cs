// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsContext;

[JsonSerializable(typeof(NatsContextJsonModel))]
internal sealed partial class NatsContextJsonSerializerContext : JsonSerializerContext
{
}
