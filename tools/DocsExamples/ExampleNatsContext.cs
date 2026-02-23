// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using Synadia.Orbit.NatsContext;

namespace DocsExamples;

public class ExampleNatsContext
{
    public static async Task Run()
    {
        // dotnet add package Synadia.Orbit.NatsContext --prerelease

        // Load settings without connecting (useful for inspecting configuration)
        var ctx = NatsContext.Load();
        Console.WriteLine($"Context: {ctx.Settings.Name}, URL: {ctx.Settings.Url}");

        // Connect using the loaded context
        await using var connection = await ctx.ConnectAsync();
        Console.WriteLine($"Connected to {ctx.Settings.Url}");

        // Publish a message using the connection
        await connection.PublishAsync("greet", "hello");
        Console.WriteLine("Published message to 'greet'");

        // Load and connect with custom options
        var customCtx = NatsContext.Load();
        await using var customConn = await customCtx.ConnectAsync(opts => opts with
        {
            Name = "my-app",
        });
        Console.WriteLine($"Connected as {customConn.Opts.Name}");
    }
}
