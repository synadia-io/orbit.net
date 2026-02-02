// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Model representing a fisk flag (maps from System.CommandLine Option).
/// </summary>
public class FiskFlagModel
{
    /// <summary>
    /// Gets or sets the flag name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the flag help text.
    /// </summary>
    [JsonPropertyName("help")]
    public string Help { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the short alias as an ASCII code (e.g. 115 for 's').
    /// </summary>
    [JsonPropertyName("short")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Short { get; set; }

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
    /// Gets or sets a value indicating whether the flag is required.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flag is hidden.
    /// </summary>
    [JsonPropertyName("hidden")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hidden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flag is boolean. Always serialized.
    /// </summary>
    [JsonPropertyName("boolean")]
    public bool Boolean { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flag is negatable.
    /// </summary>
    [JsonPropertyName("negatable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Negatable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flag is cumulative. Always serialized.
    /// </summary>
    [JsonPropertyName("cumulative")]
    public bool Cumulative { get; set; }
}
