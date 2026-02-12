// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using NATS.Client.Core;
using Synadia.Orbit.Counters.Models;

namespace Synadia.Orbit.Counters;

/// <summary>
/// Provides a source-generated JSON serializer for counter-related types.
/// </summary>
/// <typeparam name="T">The type of object being serialized or deserialized.</typeparam>
public static class CounterJsonSerializer<T>
{
    /// <summary>
    /// The default JSON serializer instance for counter operations.
    /// </summary>
    public static readonly INatsSerializer<T> Default = new NatsJsonContextSerializer<T>(CounterJsonSerializerContext.Default);
}

#pragma warning disable SA1402

/// <summary>
/// Source-generated JSON serialization context for counter-related types.
/// </summary>
[JsonSerializable(typeof(DirectGetLastRequest))]
[JsonSerializable(typeof(DirectGetMultiRequest))]
[JsonSerializable(typeof(CounterValuePayload))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
internal partial class CounterJsonSerializerContext : JsonSerializerContext;
