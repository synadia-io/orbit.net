// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;
using NATS.Client.Core;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.DirectGet;

public static class DirectGetJsonSerializer<T>
{
    public static readonly INatsSerializer<T> Default = new NatsJsonContextSerializer<T>(DirectGetJsonSerializerContext.Default);
}

[JsonSerializable(typeof(StreamMsgBatchGetRequest))]
internal partial class DirectGetJsonSerializerContext : JsonSerializerContext;
