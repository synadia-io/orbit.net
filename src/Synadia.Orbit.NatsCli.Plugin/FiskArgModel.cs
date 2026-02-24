// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Model representing a fisk argument (maps from System.CommandLine Argument).
/// </summary>
public class FiskArgModel
{
    /// <summary>
    /// Gets or sets the argument name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the argument help text.
    /// </summary>
    [JsonPropertyName("help")]
    public string Help { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default values.
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Default { get; set; }

    /// <summary>
    /// Gets or sets the placeholder text.
    /// </summary>
    [JsonPropertyName("place_holder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlaceHolder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the argument is required.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the argument is hidden.
    /// </summary>
    [JsonPropertyName("hidden")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hidden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the argument is cumulative. Always serialized.
    /// </summary>
    [JsonPropertyName("cumulative")]
    public bool Cumulative { get; set; }
}
