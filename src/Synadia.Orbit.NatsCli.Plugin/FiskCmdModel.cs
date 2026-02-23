// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Model representing a fisk command.
/// </summary>
public class FiskCmdModel
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command aliases.
    /// </summary>
    [JsonPropertyName("aliases")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Aliases { get; set; }

    /// <summary>
    /// Gets or sets the command help text.
    /// </summary>
    [JsonPropertyName("help")]
    public string Help { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the long help text.
    /// </summary>
    [JsonPropertyName("help_long")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HelpLong { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is hidden.
    /// </summary>
    [JsonPropertyName("hidden")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hidden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the default command.
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Default { get; set; }

    /// <summary>
    /// Gets or sets the command flags.
    /// </summary>
    [JsonPropertyName("flags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FiskFlagModel>? Flags { get; set; }

    /// <summary>
    /// Gets or sets the command arguments.
    /// </summary>
    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FiskArgModel>? Args { get; set; }

    /// <summary>
    /// Gets or sets the subcommands.
    /// </summary>
    [JsonPropertyName("commands")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FiskCmdModel>? Commands { get; set; }
}
