// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.CommandLine;
using System.Text.Json;

namespace Synadia.Orbit.NatsCli.Plugin.Test;

public class FiskModelSerializationTests
{
    [Fact]
    public void BooleanAndCumulative_AlwaysSerialized_OnFlagModel()
    {
        var flag = new FiskFlagModel
        {
            Name = "verbose",
            Help = "Verbose output",
            Boolean = false,
            Cumulative = false,
        };

        var json = JsonSerializer.Serialize(flag);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("boolean", out var boolProp));
        Assert.False(boolProp.GetBoolean());
        Assert.True(doc.RootElement.TryGetProperty("cumulative", out var cumuProp));
        Assert.False(cumuProp.GetBoolean());
    }

    [Fact]
    public void CumulativeAlwaysSerialized_OnArgModel()
    {
        var arg = new FiskArgModel
        {
            Name = "files",
            Help = "Files to process",
            Cumulative = false,
        };

        var json = JsonSerializer.Serialize(arg);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("cumulative", out var cumuProp));
        Assert.False(cumuProp.GetBoolean());
    }

    [Fact]
    public void ShortAlias_SerializedAsInt()
    {
        var flag = new FiskFlagModel
        {
            Name = "server",
            Help = "Server URL",
            Short = 115,
        };

        var json = JsonSerializer.Serialize(flag);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("short", out var shortProp));
        Assert.Equal(115, shortProp.GetInt32());
    }

    [Fact]
    public void OmitEmpty_HiddenNotPresent_WhenFalse()
    {
        var flag = new FiskFlagModel
        {
            Name = "server",
            Help = "Server URL",
            Hidden = false,
        };

        var json = JsonSerializer.Serialize(flag);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("hidden", out _));
    }

    [Fact]
    public void OmitEmpty_RequiredNotPresent_WhenFalse()
    {
        var flag = new FiskFlagModel
        {
            Name = "server",
            Help = "Server URL",
            Required = false,
        };

        var json = JsonSerializer.Serialize(flag);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("required", out _));
    }

    [Fact]
    public void OmitEmpty_DefaultNotPresent_WhenNull()
    {
        var flag = new FiskFlagModel
        {
            Name = "server",
            Help = "Server URL",
        };

        var json = JsonSerializer.Serialize(flag);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("default", out _));
    }

    [Fact]
    public void FullModel_SerializesToValidJson()
    {
        var root = new RootCommand("Test plugin");
        var serverOpt = new Option<string>("--server", "-s") { Description = "Server URL" };
        root.Add(serverOpt);

        var listCmd = new Command("list", "List items");
        listCmd.Aliases.Add("ls");
        var nameArg = new Argument<string>("name") { Description = "Filter name" };
        listCmd.Add(nameArg);
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Verbose" };
        listCmd.Add(verboseOpt);
        root.Add(listCmd);

        var model = FiskModelConverter.ToFiskModel(root, "test-plugin", "1.0.0");
        var json = JsonSerializer.Serialize(model);
        var doc = JsonDocument.Parse(json);
        var rootEl = doc.RootElement;

        Assert.Equal("test-plugin", rootEl.GetProperty("name").GetString());
        Assert.Equal("Test plugin", rootEl.GetProperty("help").GetString());
        Assert.Equal("1.0.0", rootEl.GetProperty("version").GetString());

        var commands = rootEl.GetProperty("commands");
        Assert.Equal(1, commands.GetArrayLength());

        var listCmdJson = commands[0];
        Assert.Equal("list", listCmdJson.GetProperty("name").GetString());
        Assert.Contains("ls", listCmdJson.GetProperty("aliases").EnumerateArray().Select(e => e.GetString()));

        var flags = listCmdJson.GetProperty("flags");
        Assert.Equal(1, flags.GetArrayLength());
        var verboseFlag = flags[0];
        Assert.Equal("verbose", verboseFlag.GetProperty("name").GetString());
        Assert.True(verboseFlag.GetProperty("boolean").GetBoolean());
        Assert.Equal('v', (char)verboseFlag.GetProperty("short").GetInt32());

        var args = listCmdJson.GetProperty("args");
        Assert.Equal(1, args.GetArrayLength());
        Assert.Equal("name", args[0].GetProperty("name").GetString());
    }

    [Fact]
    public void ApplicationModel_VersionOmittedWhenNull()
    {
        var model = new FiskApplicationModel
        {
            Name = "test",
            Help = "Test",
        };

        var json = JsonSerializer.Serialize(model);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("version", out _));
    }

    [Fact]
    public void ApplicationModel_AuthorOmittedWhenNull()
    {
        var model = new FiskApplicationModel
        {
            Name = "test",
            Help = "Test",
        };

        var json = JsonSerializer.Serialize(model);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("author", out _));
    }
}
