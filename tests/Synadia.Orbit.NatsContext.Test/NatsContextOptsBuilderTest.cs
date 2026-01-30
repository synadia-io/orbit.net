// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext.Test;

public class NatsContextOptsBuilderTest
{
    [Fact]
    public void Load_maps_url()
    {
        var (opts, _) = LoadContext(@"{""url"": ""nats://myhost:4222""}");
        Assert.Equal("nats://myhost:4222", opts.Url);
    }

    [Fact]
    public void Load_maps_user_password_auth()
    {
        var (opts, _) = LoadContext(@"{""user"": ""alice"", ""password"": ""secret""}");
        Assert.Equal("alice", opts.AuthOpts.Username);
        Assert.Equal("secret", opts.AuthOpts.Password);
    }

    [Fact]
    public void Load_maps_creds_file()
    {
        var (opts, _) = LoadContext(@"{""creds"": ""/path/to/creds.jwt""}");
        Assert.Equal("/path/to/creds.jwt", opts.AuthOpts.CredsFile);
    }

    [Fact]
    public void Load_maps_nkey_file()
    {
        var (opts, _) = LoadContext(@"{""nkey"": ""/path/to/nkey.seed""}");
        Assert.Equal("/path/to/nkey.seed", opts.AuthOpts.NKeyFile);
    }

    [Fact]
    public void Load_auth_priority_user_over_creds()
    {
        var (opts, _) = LoadContext(@"{""user"": ""alice"", ""password"": ""secret"", ""creds"": ""/path/creds""}");
        Assert.Equal("alice", opts.AuthOpts.Username);
        Assert.Null(opts.AuthOpts.CredsFile);
    }

    [Fact]
    public void Load_auth_priority_creds_over_nkey()
    {
        var (opts, _) = LoadContext(@"{""creds"": ""/path/creds"", ""nkey"": ""/path/nkey""}");
        Assert.Equal("/path/creds", opts.AuthOpts.CredsFile);
        Assert.Null(opts.AuthOpts.NKeyFile);
    }

    [Fact]
    public void Load_token_applied_independently()
    {
        var (opts, _) = LoadContext(@"{""user"": ""alice"", ""password"": ""secret"", ""token"": ""mytoken""}");
        Assert.Equal("alice", opts.AuthOpts.Username);
        Assert.Equal("mytoken", opts.AuthOpts.Token);
    }

    [Fact]
    public void Load_maps_jwt()
    {
        var (opts, _) = LoadContext(@"{""user_jwt"": ""eyJ0eXAi...""}");
        Assert.Equal("eyJ0eXAi...", opts.AuthOpts.Jwt);
    }

    [Fact]
    public void Load_maps_inbox_prefix()
    {
        var (opts, _) = LoadContext(@"{""inbox_prefix"": ""_MY_INBOX""}");
        Assert.Equal("_MY_INBOX", opts.InboxPrefix);
    }

    [Fact]
    public void Load_maps_tls_first()
    {
        var (opts, _) = LoadContext(@"{""tls_first"": true}");
        Assert.Equal(TlsMode.Implicit, opts.TlsOpts.Mode);
    }

#if !NETFRAMEWORK
    [Fact]
    public void Load_maps_tls_cert_and_key()
    {
        var (opts, _) = LoadContext(@"{""cert"": ""/path/cert.pem"", ""key"": ""/path/key.pem""}");
        Assert.Equal("/path/cert.pem", opts.TlsOpts.CertFile);
        Assert.Equal("/path/key.pem", opts.TlsOpts.KeyFile);
    }

    [Fact]
    public void Load_maps_ca()
    {
        var (opts, _) = LoadContext(@"{""ca"": ""/path/ca.pem""}");
        Assert.Equal("/path/ca.pem", opts.TlsOpts.CaFile);
    }

    [Fact]
    public void Load_expands_home_in_tls_paths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var (opts, _) = LoadContext(@"{""cert"": ""~/cert.pem"", ""key"": ""~/key.pem"", ""ca"": ""~/ca.pem""}");
        Assert.Equal(home + "/cert.pem", opts.TlsOpts.CertFile);
        Assert.Equal(home + "/key.pem", opts.TlsOpts.KeyFile);
        Assert.Equal(home + "/ca.pem", opts.TlsOpts.CaFile);
    }
#endif

    [Fact]
    public void Load_expands_home_in_nkey_path()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var (opts, _) = LoadContext(@"{""nkey"": ""~/nkey.seed""}");
        Assert.Equal(home + "/nkey.seed", opts.AuthOpts.NKeyFile);
    }

    [Fact]
    public void Load_throws_for_nsc_lookup()
    {
        var ex = Assert.Throws<NotSupportedException>(() => LoadContext(@"{""nsc"": ""myoperator""}"));
        Assert.Contains("NSC", ex.Message);
    }

    [Fact]
    public void Load_uses_defaults_for_empty_settings()
    {
        var (opts, _) = LoadContext(@"{}");
        Assert.Equal(NatsOpts.Default.Url, opts.Url);
        Assert.True(opts.AuthOpts.IsAnonymous);
    }

    private static NatsContextResult LoadContext(string json)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nats-ctx-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var contextFile = Path.Combine(tempDir, "test.json");
            File.WriteAllText(contextFile, json);
            return NatsContext.Load(contextFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
