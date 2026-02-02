// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.CommandLine;
using System.Text.Json;

namespace Synadia.Orbit.NatsCli.Plugin;

/// <summary>
/// Entry point for NATS CLI plugins. Detects fisk introspection requests and delegates
/// to System.CommandLine for normal execution.
/// </summary>
public static class NatsCliPlugin
{
    /// <summary>
    /// Runs the plugin. If <c>--fisk-introspect</c> is present in the arguments,
    /// outputs the fisk JSON model and exits. Otherwise, delegates to System.CommandLine.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="rootCommand">The root command defining the plugin's CLI.</param>
    /// <param name="options">Plugin configuration options.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args, RootCommand rootCommand, NatsCliPluginOptions options)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--fisk-introspect")
            {
                return HandleIntrospect(rootCommand, options);
            }
        }

        return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    private static int HandleIntrospect(RootCommand rootCommand, NatsCliPluginOptions options)
    {
        var model = FiskModelConverter.ToFiskModel(
            rootCommand,
            options.Name,
            options.Version,
            options.Author);

        var json = JsonSerializer.Serialize(model, FiskJsonContext.Default.FiskApplicationModel);
        Console.WriteLine(json);
        return 0;
    }
}
