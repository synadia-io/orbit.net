// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using NATS.Client.Core;
using Synadia.Orbit.DirectGet.Models;

namespace Synadia.Orbit.DirectGet;

/// <summary>
/// Provides a static implementation of a default JSON serializer for specific types
/// within the Direct Get module to enable serialization and deserialization for
/// NATS messaging operations.
/// </summary>
/// <typeparam name="T">The type of object being serialized or deserialized.</typeparam>
public static class DirectGetJsonSerializer<T>
{
    /// <summary>
    /// Represents the default JSON serializer instance for handling serialization and deserialization
    /// of objects of type <typeparamref name="T"/> within the Direct Get module. This serializer is used
    /// internally for NATS messaging operations where JSON formatting is required.
    /// </summary>
    public static readonly INatsSerializer<T> Default = new NatsJsonContextSerializer<T>(DirectGetJsonSerializerContext.Default);
}

#pragma warning disable SA1402
/// <summary>
/// Represents a source-generated JSON serialization context for the Direct Get module,
/// specifically configured for serializing and deserializing types such as
/// <see cref="StreamMsgBatchGetRequest"/>. This context is internally used for efficient
/// JSON processing within the module.
/// </summary>
[JsonSerializable(typeof(StreamMsgBatchGetRequest))]
internal partial class DirectGetJsonSerializerContext : JsonSerializerContext;
