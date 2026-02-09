// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Top-level model representing a fisk application for plugin introspection.
/// </summary>
public class FiskApplicationModel
{
    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application help text.
    /// </summary>
    [JsonPropertyName("help")]
    public string Help { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the application author.
    /// </summary>
    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the application flags.
    /// </summary>
    [JsonPropertyName("flags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FiskFlagModel>? Flags { get; set; }

    /// <summary>
    /// Gets or sets the application arguments.
    /// </summary>
    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FiskArgModel>? Args { get; set; }

    /// <summary>
    /// Gets or sets the application commands.
    /// </summary>
    [JsonPropertyName("commands")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FiskCmdModel>? Commands { get; set; }
}
