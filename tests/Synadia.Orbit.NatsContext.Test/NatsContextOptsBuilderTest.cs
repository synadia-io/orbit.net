// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext.Test;

public class NatsContextOptsBuilderTest
{
    [Fact]
    public void Load_maps_url()
    {
        var ctx = LoadContext(@"{""url"": ""nats://myhost:4222""}");
        Assert.Equal("nats://myhost:4222", ctx.Opts.Url);
    }

    [Fact]
    public void Load_maps_user_password_auth()
    {
        var ctx = LoadContext(@"{""user"": ""alice"", ""password"": ""secret""}");
        Assert.Equal("alice", ctx.Opts.AuthOpts.Username);
        Assert.Equal("secret", ctx.Opts.AuthOpts.Password);
    }

    [Fact]
    public void Load_maps_creds_file()
    {
        var ctx = LoadContext(@"{""creds"": ""/path/to/creds.jwt""}");
        Assert.Equal("/path/to/creds.jwt", ctx.Opts.AuthOpts.CredsFile);
    }

    [Fact]
    public void Load_maps_nkey_file()
    {
        var ctx = LoadContext(@"{""nkey"": ""/path/to/nkey.seed""}");
        Assert.Equal("/path/to/nkey.seed", ctx.Opts.AuthOpts.NKeyFile);
    }

    [Fact]
    public void Load_auth_priority_user_over_creds()
    {
        var ctx = LoadContext(@"{""user"": ""alice"", ""password"": ""secret"", ""creds"": ""/path/creds""}");
        Assert.Equal("alice", ctx.Opts.AuthOpts.Username);
        Assert.Null(ctx.Opts.AuthOpts.CredsFile);
    }

    [Fact]
    public void Load_auth_priority_creds_over_nkey()
    {
        var ctx = LoadContext(@"{""creds"": ""/path/creds"", ""nkey"": ""/path/nkey""}");
        Assert.Equal("/path/creds", ctx.Opts.AuthOpts.CredsFile);
        Assert.Null(ctx.Opts.AuthOpts.NKeyFile);
    }

    [Fact]
    public void Load_token_applied_independently()
    {
        var ctx = LoadContext(@"{""user"": ""alice"", ""password"": ""secret"", ""token"": ""mytoken""}");
        Assert.Equal("alice", ctx.Opts.AuthOpts.Username);
        Assert.Equal("mytoken", ctx.Opts.AuthOpts.Token);
    }

    [Fact]
    public void Load_maps_jwt()
    {
        var ctx = LoadContext(@"{""user_jwt"": ""eyJ0eXAi...""}");
        Assert.Equal("eyJ0eXAi...", ctx.Opts.AuthOpts.Jwt);
    }

    [Fact]
    public void Load_maps_inbox_prefix()
    {
        var ctx = LoadContext(@"{""inbox_prefix"": ""_MY_INBOX""}");
        Assert.Equal("_MY_INBOX", ctx.Opts.InboxPrefix);
    }

    [Fact]
    public void Load_maps_tls_first()
    {
        var ctx = LoadContext(@"{""tls_first"": true}");
        Assert.Equal(TlsMode.Implicit, ctx.Opts.TlsOpts.Mode);
    }

#if !NETFRAMEWORK
    [Fact]
    public void Load_maps_tls_cert_and_key()
    {
        var ctx = LoadContext(@"{""cert"": ""/path/cert.pem"", ""key"": ""/path/key.pem""}");
        Assert.Equal("/path/cert.pem", ctx.Opts.TlsOpts.CertFile);
        Assert.Equal("/path/key.pem", ctx.Opts.TlsOpts.KeyFile);
    }

    [Fact]
    public void Load_maps_ca()
    {
        var ctx = LoadContext(@"{""ca"": ""/path/ca.pem""}");
        Assert.Equal("/path/ca.pem", ctx.Opts.TlsOpts.CaFile);
    }

    [Fact]
    public void Load_expands_home_in_tls_paths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var ctx = LoadContext(@"{""cert"": ""~/cert.pem"", ""key"": ""~/key.pem"", ""ca"": ""~/ca.pem""}");
        Assert.Equal(home + "/cert.pem", ctx.Opts.TlsOpts.CertFile);
        Assert.Equal(home + "/key.pem", ctx.Opts.TlsOpts.KeyFile);
        Assert.Equal(home + "/ca.pem", ctx.Opts.TlsOpts.CaFile);
    }
#endif

    [Fact]
    public void Load_expands_home_in_nkey_path()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var ctx = LoadContext(@"{""nkey"": ""~/nkey.seed""}");
        Assert.Equal(home + "/nkey.seed", ctx.Opts.AuthOpts.NKeyFile);
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
        var ctx = LoadContext(@"{}");
        Assert.Equal(NatsOpts.Default.Url, ctx.Opts.Url);
        Assert.True(ctx.Opts.AuthOpts.IsAnonymous);
    }

    private static NatsContext LoadContext(string json)
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
