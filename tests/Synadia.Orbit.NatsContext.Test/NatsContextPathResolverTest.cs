// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.NatsContext.Test;

[Collection("env-tests")]
public class NatsContextPathResolverTest
{
    [Fact]
    public void Load_from_absolute_path()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var contextFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(contextFile, @"{""url"": ""nats://localhost:4222"", ""user"": ""alice""}");

            var (opts, settings) = NatsContext.Load(contextFile);

            Assert.Equal("nats://localhost:4222", opts.Url);
            Assert.Equal("alice", settings.Username);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_named_context()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);
            File.WriteAllText(Path.Combine(contextDir, "myctx.json"), @"{""url"": ""nats://host:4222""}");

            var (opts, _) = NatsContext.Load("myctx");

            Assert.Equal("nats://host:4222", opts.Url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_active_context_from_context_txt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var natsDir = Path.Combine(tempDir, "nats");
        var contextDir = Path.Combine(natsDir, "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);
            File.WriteAllText(Path.Combine(natsDir, "context.txt"), "active-ctx");
            File.WriteAllText(Path.Combine(contextDir, "active-ctx.json"), @"{""url"": ""nats://active:4222""}");

            var (opts, _) = NatsContext.Load();

            Assert.Equal("nats://active:4222", opts.Url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_returns_defaults_when_no_active_context()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var natsDir = Path.Combine(tempDir, "nats");
        Directory.CreateDirectory(natsDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            var (_, settings) = NatsContext.Load();

            Assert.Null(settings.Url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_throws_for_unknown_named_context()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            var ex = Assert.Throws<NatsContextException>(() => NatsContext.Load("nonexistent"));
            Assert.Contains("Unknown context", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_throws_for_invalid_name_with_double_dots()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            var ex = Assert.Throws<ArgumentException>(() => NatsContext.Load("foo..bar"));
            Assert.Contains("Invalid", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_throws_for_name_with_path_separators()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);

            Assert.Throws<ArgumentException>(() => NatsContext.Load("foo/bar"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_uses_xdg_config_home()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);
            File.WriteAllText(Path.Combine(contextDir, "xdg.json"), @"{""url"": ""nats://xdg:4222""}");

            var (opts, _) = NatsContext.Load("xdg");

            Assert.Equal("nats://xdg:4222", opts.Url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_expands_home_in_creds_path()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var contextFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(contextFile, @"{""creds"": ""~/my.creds""}");

            var (opts, _) = NatsContext.Load(contextFile);

            Assert.Equal(home + "/my.creds", opts.AuthOpts.CredsFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
