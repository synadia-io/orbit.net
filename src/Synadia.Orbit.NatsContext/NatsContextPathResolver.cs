// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Synadia.Orbit.NatsContext;

internal static class NatsContextPathResolver
{
    /// <summary>
    /// Resolves and loads a context JSON model from a name, file path, or the active context.
    /// </summary>
    internal static NatsContextJsonModel Resolve(string? name)
    {
        string filePath;

        if (name is { Length: > 0 } && Path.IsPathRooted(name))
        {
            // Absolute path given - load directly
            filePath = name;
        }
        else
        {
            var configDir = GetConfigDir();

            if (string.IsNullOrEmpty(name))
            {
                // Read selected context from context.txt
                name = ReadSelectedContext(configDir);
                if (string.IsNullOrEmpty(name))
                {
                    // No active context - return empty settings
                    return new NatsContextJsonModel();
                }
            }

            ValidateName(name!);

            filePath = Path.Combine(configDir, "nats", "context", name + ".json");
            if (!File.Exists(filePath))
            {
                throw new NatsContextException($"Unknown context \"{name}\"");
            }
        }

        return LoadFromFile(filePath);
    }

    internal static string GetConfigDir()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfigHome))
        {
            return xdgConfigHome;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            throw new NatsContextException("Cannot determine home directory");
        }

        return Path.Combine(home, ".config");
    }

    internal static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Context name cannot be empty", nameof(name));
        }

        if (name.Contains(".."))
        {
            throw new ArgumentException($"Invalid context name \"{name}\"", nameof(name));
        }

        if (name.IndexOf(Path.DirectorySeparatorChar) >= 0 || name.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            throw new ArgumentException($"Invalid context name \"{name}\"", nameof(name));
        }
    }

    internal static string ExpandEnvironmentVariables(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return Environment.ExpandEnvironmentVariables(value);
    }

    private static string? ReadSelectedContext(string configDir)
    {
        var contextFile = Path.Combine(configDir, "nats", "context.txt");
        if (!File.Exists(contextFile))
        {
            return null;
        }

        return File.ReadAllText(contextFile).Trim();
    }

    private static NatsContextJsonModel LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);

        var model = JsonSerializer.Deserialize(json, NatsContextJsonSerializerContext.Default.NatsContextJsonModel);
        if (model == null)
        {
            throw new NatsContextException($"Failed to deserialize context file: {filePath}");
        }

        // Expand environment variables on creds field (matching Go behavior)
        if (!string.IsNullOrEmpty(model.Creds))
        {
            model.Creds = ExpandEnvironmentVariables(model.Creds);
        }

        return model;
    }
}
