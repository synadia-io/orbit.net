// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.PCGroups.Static;

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// JSON serializer context for partitioned consumer group types.
/// </summary>
[JsonSerializable(typeof(NatsPCStaticConfig))]
[JsonSerializable(typeof(NatsPCElasticConfig))]
[JsonSerializable(typeof(NatsPCMemberMapping))]
[JsonSerializable(typeof(NatsPCMemberMapping[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class NatsPCJsonSerializerContext : JsonSerializerContext
{
}
