// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Options for configuring the NATS CLI plugin.
/// </summary>
public class NatsCliPluginOptions
{
    /// <summary>
    /// Gets or sets the plugin name as registered with the NATS CLI.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the plugin author.
    /// </summary>
    public string? Author { get; set; }
}
