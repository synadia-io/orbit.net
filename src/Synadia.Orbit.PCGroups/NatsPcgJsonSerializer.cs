// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.PCGroups.Static;

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Provides AOT-compatible JSON serializers for partitioned consumer group types.
/// </summary>
/// <typeparam name="T">The type of object being serialized or deserialized.</typeparam>
public static class NatsPcgJsonSerializer<T>
{
    /// <summary>
    /// Gets the default JSON serializer instance for handling serialization and deserialization
    /// of objects of type <typeparamref name="T"/> within the PCGroups module.
    /// </summary>
    /// <remarks>
    /// This serializer uses source-generated JSON serialization for AOT compatibility.
    /// Supported types are <see cref="NatsPcgStaticConfig"/>, <see cref="NatsPcgElasticConfig"/>,
    /// and <see cref="NatsPcgMemberMapping"/>.
    /// </remarks>
    public static readonly INatsSerializer<T> Default = new NatsJsonContextSerializer<T>(NatsPcgJsonSerializerContext.Default);
}
