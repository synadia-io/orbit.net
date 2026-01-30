// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.NatsContext.Test;

[Collection("env-tests")]
public class NatsContextLoadTest
{
    [Fact]
    public void Load_from_absolute_file_path()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var contextFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(contextFile, @"{""url"": ""nats://testhost:4222"", ""user"": ""bob"", ""password"": ""s3cret""}");

            var ctx = NatsContext.Load(contextFile);

            Assert.Equal("nats://testhost:4222", ctx.Opts.Url);
            Assert.Equal("bob", ctx.Opts.AuthOpts.Username);
            Assert.Equal("s3cret", ctx.Opts.AuthOpts.Password);
            Assert.Equal("bob", ctx.Settings.Username);
            Assert.Equal("s3cret", ctx.Settings.Password);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_from_named_context()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var contextDir = Path.Combine(tempDir, "nats", "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);
            File.WriteAllText(
                Path.Combine(contextDir, "prod.json"),
                @"{""url"": ""nats://prod:4222"", ""token"": ""t0ken"", ""inbox_prefix"": ""_CUSTOM""}");

            var ctx = NatsContext.Load("prod");

            Assert.Equal("nats://prod:4222", ctx.Opts.Url);
            Assert.Equal("t0ken", ctx.Opts.AuthOpts.Token);
            Assert.Equal("_CUSTOM", ctx.Opts.InboxPrefix);
            Assert.Equal("t0ken", ctx.Settings.Token);
            Assert.Equal("_CUSTOM", ctx.Settings.InboxPrefix);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_from_active_context()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        var natsDir = Path.Combine(tempDir, "nats");
        var contextDir = Path.Combine(natsDir, "context");
        Directory.CreateDirectory(contextDir);

        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tempDir);
            File.WriteAllText(Path.Combine(natsDir, "context.txt"), "default");
            File.WriteAllText(
                Path.Combine(contextDir, "default.json"),
                @"{""url"": ""nats://default:4222""}");

            var ctx = NatsContext.Load();

            Assert.Equal("nats://default:4222", ctx.Opts.Url);
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

            var ctx = NatsContext.Load();

            // Should return default NatsOpts when no active context
            Assert.NotNull(ctx.Opts);
            Assert.Null(ctx.Settings.Url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_preserves_all_settings_fields()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var contextFile = Path.Combine(tempDir, "full.json");
            var json = @"{
                ""url"": ""nats://host:4222"",
                ""user"": ""alice"",
                ""password"": ""pass"",
                ""token"": ""tok"",
                ""creds"": ""/path/creds"",
                ""nkey"": ""/path/nkey"",
                ""cert"": ""/path/cert"",
                ""key"": ""/path/key"",
                ""ca"": ""/path/ca"",
                ""inbox_prefix"": ""_INBOX"",
                ""user_jwt"": ""jwt-value"",
                ""tls_first"": true,
                ""jetstream_domain"": ""hub"",
                ""jetstream_api_prefix"": ""$JS.hub.API"",
                ""jetstream_event_prefix"": ""$JS.hub.EVENT"",
                ""description"": ""Test context"",
                ""color_scheme"": ""dark""
            }";
            File.WriteAllText(contextFile, json);

            var ctx = NatsContext.Load(contextFile);

            Assert.Equal("nats://host:4222", ctx.Settings.Url);
            Assert.Equal("alice", ctx.Settings.Username);
            Assert.Equal("pass", ctx.Settings.Password);
            Assert.Equal("tok", ctx.Settings.Token);
            Assert.Equal("/path/creds", ctx.Settings.CredentialsPath);
            Assert.Equal("/path/nkey", ctx.Settings.NKeyPath);
            Assert.Equal("/path/cert", ctx.Settings.CertificatePath);
            Assert.Equal("/path/key", ctx.Settings.KeyPath);
            Assert.Equal("/path/ca", ctx.Settings.CaCertificatePath);
            Assert.Equal("_INBOX", ctx.Settings.InboxPrefix);
            Assert.Equal("jwt-value", ctx.Settings.UserJwt);
            Assert.True(ctx.Settings.TlsFirst);
            Assert.Equal("hub", ctx.Settings.JetStreamDomain);
            Assert.Equal("$JS.hub.API", ctx.Settings.JetStreamApiPrefix);
            Assert.Equal("$JS.hub.EVENT", ctx.Settings.JetStreamEventPrefix);
            Assert.Equal("Test context", ctx.Settings.Description);
            Assert.Equal("dark", ctx.Settings.ColorScheme);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
