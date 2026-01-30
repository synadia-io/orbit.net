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

            var (opts, settings) = NatsContext.Load(contextFile);

            Assert.Equal("nats://testhost:4222", opts.Url);
            Assert.Equal("bob", opts.AuthOpts.Username);
            Assert.Equal("s3cret", opts.AuthOpts.Password);
            Assert.Equal("bob", settings.Username);
            Assert.Equal("s3cret", settings.Password);
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

            var (opts, settings) = NatsContext.Load("prod");

            Assert.Equal("nats://prod:4222", opts.Url);
            Assert.Equal("t0ken", opts.AuthOpts.Token);
            Assert.Equal("_CUSTOM", opts.InboxPrefix);
            Assert.Equal("t0ken", settings.Token);
            Assert.Equal("_CUSTOM", settings.InboxPrefix);
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

            var (opts, settings) = NatsContext.Load();

            Assert.Equal("nats://default:4222", opts.Url);
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

            var (opts, settings) = NatsContext.Load();

            // Should return default NatsOpts when no active context
            Assert.NotNull(opts);
            Assert.Null(settings.Url);
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

            var (_, settings) = NatsContext.Load(contextFile);

            Assert.Equal("nats://host:4222", settings.Url);
            Assert.Equal("alice", settings.Username);
            Assert.Equal("pass", settings.Password);
            Assert.Equal("tok", settings.Token);
            Assert.Equal("/path/creds", settings.CredentialsPath);
            Assert.Equal("/path/nkey", settings.NKeyPath);
            Assert.Equal("/path/cert", settings.CertificatePath);
            Assert.Equal("/path/key", settings.KeyPath);
            Assert.Equal("/path/ca", settings.CaCertificatePath);
            Assert.Equal("_INBOX", settings.InboxPrefix);
            Assert.Equal("jwt-value", settings.UserJwt);
            Assert.True(settings.TlsFirst);
            Assert.Equal("hub", settings.JetStreamDomain);
            Assert.Equal("$JS.hub.API", settings.JetStreamApiPrefix);
            Assert.Equal("$JS.hub.EVENT", settings.JetStreamEventPrefix);
            Assert.Equal("Test context", settings.Description);
            Assert.Equal("dark", settings.ColorScheme);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
