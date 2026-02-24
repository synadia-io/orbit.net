// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.CommandLine;
using System.Text.Json;

namespace Synadia.Orbit.NatsCli.Plugin.Test;

public class NatsCliPluginTests
{
    [Fact]
    public async Task FiskIntrospect_ProducesValidJson()
    {
        var root = new RootCommand("Test plugin");
        var listCmd = new Command("list", "List items");
        listCmd.Aliases.Add("ls");
        root.Add(listCmd);

        var options = new NatsCliPluginOptions
        {
            Name = "test-plugin",
            Version = "1.0.0",
        };

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            var exitCode = await root.RunNatsCliPluginAsync(["--fisk-introspect"], options);

            Assert.Equal(0, exitCode);

            var json = sw.ToString().Trim();
            var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;

            Assert.Equal("test-plugin", rootEl.GetProperty("name").GetString());
            Assert.Equal("Test plugin", rootEl.GetProperty("help").GetString());
            Assert.Equal("1.0.0", rootEl.GetProperty("version").GetString());

            var commands = rootEl.GetProperty("commands");
            Assert.Equal(1, commands.GetArrayLength());
            Assert.Equal("list", commands[0].GetProperty("name").GetString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task NormalArgs_ExecuteCommand()
    {
        var executed = false;
        var root = new RootCommand("Test plugin");
        var listCmd = new Command("list", "List items");
        listCmd.SetAction((parseResult, ct) =>
        {
            executed = true;
            return Task.FromResult(0);
        });
        root.Add(listCmd);

        var options = new NatsCliPluginOptions
        {
            Name = "test-plugin",
            Version = "1.0.0",
        };

        var exitCode = await root.RunNatsCliPluginAsync(["list"], options);

        Assert.Equal(0, exitCode);
        Assert.True(executed);
    }

    [Fact]
    public async Task FiskIntrospect_WithOptions_IncludesFlags()
    {
        var root = new RootCommand("Test plugin");
        var serverOpt = new Option<string>("--server", "-s") { Description = "Server URL" };
        root.Add(serverOpt);
        var cmd = new Command("do", "Do something");
        root.Add(cmd);

        var options = new NatsCliPluginOptions
        {
            Name = "test",
            Version = "2.0.0",
            Author = "Test Author",
        };

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            var exitCode = await root.RunNatsCliPluginAsync(["--fisk-introspect"], options);

            Assert.Equal(0, exitCode);

            var json = sw.ToString().Trim();
            var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;

            Assert.Equal("Test Author", rootEl.GetProperty("author").GetString());

            var flags = rootEl.GetProperty("flags");
            Assert.True(flags.GetArrayLength() >= 1);

            var serverFlag = flags.EnumerateArray().First(f => f.GetProperty("name").GetString() == "server");
            Assert.Equal(115, serverFlag.GetProperty("short").GetInt32());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task FiskIntrospect_FiltersHelpAndVersion()
    {
        var root = new RootCommand("Test plugin");
        var cmd = new Command("run", "Run task");
        root.Add(cmd);

        var options = new NatsCliPluginOptions
        {
            Name = "test",
        };

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            var exitCode = await root.RunNatsCliPluginAsync(["--fisk-introspect"], options);

            Assert.Equal(0, exitCode);

            var json = sw.ToString().Trim();
            var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;

            if (rootEl.TryGetProperty("flags", out var flags))
            {
                foreach (var flag in flags.EnumerateArray())
                {
                    var name = flag.GetProperty("name").GetString();
                    Assert.NotEqual("help", name);
                    Assert.NotEqual("version", name);
                }
            }

            if (rootEl.TryGetProperty("commands", out var cmds))
            {
                foreach (var cmdEl in cmds.EnumerateArray())
                {
                    Assert.NotEqual("help", cmdEl.GetProperty("name").GetString());
                }
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void PluginOptions_DefaultValues()
    {
        var options = new NatsCliPluginOptions();

        Assert.Equal(string.Empty, options.Name);
        Assert.Null(options.Version);
        Assert.Null(options.Author);
    }
}
