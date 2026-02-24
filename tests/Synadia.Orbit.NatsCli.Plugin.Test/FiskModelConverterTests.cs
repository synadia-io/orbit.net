// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.CommandLine;

namespace Synadia.Orbit.NatsCli.Plugin.Test;

public class FiskModelConverterTests
{
    [Fact]
    public void BasicCommand_ConvertsToFiskModel()
    {
        var root = new RootCommand("Test plugin");
        var listCmd = new Command("list", "List items");
        root.Add(listCmd);

        var model = FiskModelConverter.ToFiskModel(root, "test-plugin", "1.0.0");

        Assert.Equal("test-plugin", model.Name);
        Assert.Equal("Test plugin", model.Help);
        Assert.Equal("1.0.0", model.Version);
        Assert.NotNull(model.Commands);
        Assert.Single(model.Commands);
        Assert.Equal("list", model.Commands[0].Name);
        Assert.Equal("List items", model.Commands[0].Help);
    }

    [Fact]
    public void OptionBecomesFlag_WithStrippedPrefix()
    {
        var root = new RootCommand("Test");
        var option = new Option<string>("--server") { Description = "Server URL" };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        Assert.Single(model.Flags);
        Assert.Equal("server", model.Flags[0].Name);
        Assert.Equal("Server URL", model.Flags[0].Help);
        Assert.False(model.Flags[0].Boolean);
    }

    [Fact]
    public void BoolOption_SetsBooleanTrue()
    {
        var root = new RootCommand("Test");
        var option = new Option<bool>("--verbose") { Description = "Enable verbose output" };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        Assert.Single(model.Flags);
        Assert.True(model.Flags[0].Boolean);
    }

    [Fact]
    public void ShortAlias_MapsToAsciiCode()
    {
        var root = new RootCommand("Test");
        var option = new Option<string>("--server", "-s") { Description = "Server URL" };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        Assert.Single(model.Flags);
        Assert.Equal('s', (char)model.Flags[0].Short);
        Assert.Equal(115, model.Flags[0].Short);
    }

    [Fact]
    public void Argument_BecomesArg()
    {
        var root = new RootCommand("Test");
        var cmd = new Command("get", "Get item");
        var arg = new Argument<string>("name") { Description = "Item name" };
        cmd.Add(arg);
        root.Add(cmd);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Commands);
        var getCmd = Assert.Single(model.Commands);
        Assert.NotNull(getCmd.Args);
        var nameArg = Assert.Single(getCmd.Args);
        Assert.Equal("name", nameArg.Name);
        Assert.Equal("Item name", nameArg.Help);
    }

    [Fact]
    public void NestedCommands_AreRecursive()
    {
        var root = new RootCommand("Test");
        var parent = new Command("kv", "Key-Value operations");
        var child = new Command("get", "Get a value");
        parent.Add(child);
        root.Add(parent);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Commands);
        var kvCmd = Assert.Single(model.Commands);
        Assert.Equal("kv", kvCmd.Name);
        Assert.NotNull(kvCmd.Commands);
        var getCmd = Assert.Single(kvCmd.Commands);
        Assert.Equal("get", getCmd.Name);
    }

    [Fact]
    public void HelpAndVersionFlags_AreFiltered()
    {
        var root = new RootCommand("Test");
        var option = new Option<string>("--server") { Description = "Server URL" };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        foreach (var flag in model.Flags)
        {
            Assert.NotEqual("help", flag.Name);
            Assert.NotEqual("version", flag.Name);
        }
    }

    [Fact]
    public void HelpCommand_IsFiltered()
    {
        var root = new RootCommand("Test");
        var listCmd = new Command("list", "List items");
        root.Add(listCmd);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Commands);
        foreach (var cmd in model.Commands)
        {
            Assert.NotEqual("help", cmd.Name);
        }
    }

    [Fact]
    public void CommandAliases_ArePreserved()
    {
        var root = new RootCommand("Test");
        var listCmd = new Command("list", "List items");
        listCmd.Aliases.Add("ls");
        root.Add(listCmd);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Commands);
        Assert.Single(model.Commands);
        Assert.NotNull(model.Commands[0].Aliases);
        Assert.Contains("ls", model.Commands[0].Aliases!);
    }

    [Fact]
    public void HiddenOption_SetsHidden()
    {
        var root = new RootCommand("Test");
        var option = new Option<string>("--internal") { Description = "Internal option", Hidden = true };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        Assert.Single(model.Flags);
        Assert.True(model.Flags[0].Hidden);
    }

    [Fact]
    public void RequiredOption_SetsRequired()
    {
        var root = new RootCommand("Test");
        var option = new Option<string>("--server") { Description = "Server URL", Required = true };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        Assert.Single(model.Flags);
        Assert.True(model.Flags[0].Required);
    }

    [Fact]
    public void DefaultValue_IsIncluded()
    {
        var root = new RootCommand("Test");
        var option = new Option<string>("--server") { Description = "Server URL", DefaultValueFactory = _ => "nats://localhost:4222" };
        root.Add(option);

        var model = FiskModelConverter.ToFiskModel(root, "test");

        Assert.NotNull(model.Flags);
        Assert.Single(model.Flags);
        Assert.NotNull(model.Flags[0].Default);
        var defaultVal = Assert.Single(model.Flags[0].Default!);
        Assert.Equal("nats://localhost:4222", defaultVal);
    }

    [Fact]
    public void Author_IsIncluded()
    {
        var root = new RootCommand("Test");

        var model = FiskModelConverter.ToFiskModel(root, "test", author: "Test Author");

        Assert.Equal("Test Author", model.Author);
    }

    [Fact]
    public void EmptyRootCommand_ProducesMinimalModel()
    {
        var root = new RootCommand("Minimal plugin");

        var model = FiskModelConverter.ToFiskModel(root, "minimal");

        Assert.Equal("minimal", model.Name);
        Assert.Equal("Minimal plugin", model.Help);
        Assert.Null(model.Commands);
        Assert.Null(model.Args);
    }
}
