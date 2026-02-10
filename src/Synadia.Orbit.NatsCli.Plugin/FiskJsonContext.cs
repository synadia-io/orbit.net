// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// AOT-compatible JSON serializer context for fisk model types.
/// </summary>
[JsonSerializable(typeof(FiskApplicationModel))]
[JsonSerializable(typeof(FiskCmdModel))]
[JsonSerializable(typeof(FiskFlagModel))]
[JsonSerializable(typeof(FiskArgModel))]
[JsonSerializable(typeof(List<FiskCmdModel>))]
[JsonSerializable(typeof(List<FiskFlagModel>))]
[JsonSerializable(typeof(List<FiskArgModel>))]
[JsonSerializable(typeof(List<string>))]
internal partial class FiskJsonContext : JsonSerializerContext
{
}
