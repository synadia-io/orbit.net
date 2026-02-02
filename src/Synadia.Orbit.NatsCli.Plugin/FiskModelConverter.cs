// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.CommandLine;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Converts a System.CommandLine command tree into fisk's ApplicationModel.
/// </summary>
public static class FiskModelConverter
{
    /// <summary>
    /// Converts a <see cref="RootCommand"/> into a <see cref="FiskApplicationModel"/>.
    /// </summary>
    /// <param name="rootCommand">The root command to convert.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="version">The plugin version.</param>
    /// <param name="author">The plugin author.</param>
    /// <returns>A fisk application model.</returns>
    public static FiskApplicationModel ToFiskModel(RootCommand rootCommand, string name, string? version = null, string? author = null)
    {
        var model = new FiskApplicationModel
        {
            Name = name,
            Help = rootCommand.Description ?? string.Empty,
            Version = version,
            Author = author,
        };

        var flags = ConvertFlags(rootCommand.Options);
        if (flags.Count > 0)
        {
            model.Flags = flags;
        }

        var args = ConvertArgs(rootCommand.Arguments);
        if (args.Count > 0)
        {
            model.Args = args;
        }

        var commands = ConvertCommands(rootCommand.Subcommands);
        if (commands.Count > 0)
        {
            model.Commands = commands;
        }

        return model;
    }

    private static List<FiskCmdModel> ConvertCommands(IEnumerable<Command> commands)
    {
        var result = new List<FiskCmdModel>();
        foreach (var cmd in commands)
        {
            if (IsFilteredCommand(cmd.Name))
            {
                continue;
            }

            var cmdModel = new FiskCmdModel
            {
                Name = cmd.Name,
                Help = cmd.Description ?? string.Empty,
                Hidden = cmd.Hidden,
            };

            var aliases = GetAliases(cmd);
            if (aliases.Count > 0)
            {
                cmdModel.Aliases = aliases;
            }

            var flags = ConvertFlags(cmd.Options);
            if (flags.Count > 0)
            {
                cmdModel.Flags = flags;
            }

            var args = ConvertArgs(cmd.Arguments);
            if (args.Count > 0)
            {
                cmdModel.Args = args;
            }

            var subcommands = ConvertCommands(cmd.Subcommands);
            if (subcommands.Count > 0)
            {
                cmdModel.Commands = subcommands;
            }

            result.Add(cmdModel);
        }

        return result;
    }

    private static List<FiskFlagModel> ConvertFlags(IEnumerable<Option> options)
    {
        var result = new List<FiskFlagModel>();
        foreach (var option in options)
        {
            var name = StripPrefix(option.Name);
            if (IsFilteredFlag(name))
            {
                continue;
            }

            var flagModel = new FiskFlagModel
            {
                Name = name,
                Help = option.Description ?? string.Empty,
                Required = option.Required,
                Hidden = option.Hidden,
                Boolean = IsBoolOption(option),
            };

            var shortAlias = GetShortAlias(option);
            if (shortAlias != 0)
            {
                flagModel.Short = shortAlias;
            }

            var defaultValue = GetDefaultValue(option);
            if (defaultValue != null)
            {
                flagModel.Default = defaultValue;
            }

            result.Add(flagModel);
        }

        return result;
    }

    private static List<FiskArgModel> ConvertArgs(IEnumerable<Argument> arguments)
    {
        var result = new List<FiskArgModel>();
        foreach (var arg in arguments)
        {
            var argModel = new FiskArgModel
            {
                Name = arg.Name,
                Help = arg.Description ?? string.Empty,
                Hidden = arg.Hidden,
            };

            result.Add(argModel);
        }

        return result;
    }

    private static bool IsFilteredCommand(string name) =>
        name is "help" or "cheat" or "help_long";

    private static bool IsFilteredFlag(string name) =>
        name == "help"
        || name == "version"
        || name.StartsWith("help-", StringComparison.Ordinal)
        || name.StartsWith("completion-", StringComparison.Ordinal)
        || name.StartsWith("fisk-", StringComparison.Ordinal);

    private static string StripPrefix(string name)
    {
        if (name.StartsWith("--", StringComparison.Ordinal))
        {
            return name.Substring(2);
        }

        if (name.StartsWith("-", StringComparison.Ordinal))
        {
            return name.Substring(1);
        }

        return name;
    }

    private static List<string> GetAliases(Command cmd)
    {
        return cmd.Aliases.Where(alias => alias != cmd.Name).ToList();
    }

    private static int GetShortAlias(Option option)
    {
        foreach (var alias in option.Aliases)
        {
            var stripped = alias;
            if (stripped.StartsWith("-", StringComparison.Ordinal) && !stripped.StartsWith("--", StringComparison.Ordinal))
            {
                stripped = stripped.Substring(1);
            }

            if (stripped.Length == 1)
            {
                return stripped[0];
            }
        }

        return 0;
    }

    private static bool IsBoolOption(Option option)
    {
        var valueType = option.ValueType;
        return valueType == typeof(bool) || valueType == typeof(bool?);
    }

    private static List<string>? GetDefaultValue(Option option)
    {
        if (option.HasDefaultValue)
        {
            var defaultValue = option.GetDefaultValue();
            var str = defaultValue?.ToString();
            if (str is { Length: > 0 })
            {
                return [str];
            }
        }

        return null;
    }
}
