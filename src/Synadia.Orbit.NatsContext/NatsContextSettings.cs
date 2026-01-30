// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.NatsContext;

/// <summary>
/// Read-only settings loaded from a NATS CLI context file.
/// </summary>
public sealed record NatsContextSettings
{
    /// <summary>
    /// Gets the context name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the context description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the NATS server URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets the SOCKS proxy URL.
    /// </summary>
    public string? SocksProxy { get; init; }

    /// <summary>
    /// Gets the authentication token.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Gets the username for authentication.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the password for authentication.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the credentials file path.
    /// </summary>
    public string? CredentialsPath { get; init; }

    /// <summary>
    /// Gets the NKey seed file path.
    /// </summary>
    public string? NKeyPath { get; init; }

    /// <summary>
    /// Gets the client certificate file path.
    /// </summary>
    public string? CertificatePath { get; init; }

    /// <summary>
    /// Gets the client certificate key file path.
    /// </summary>
    public string? KeyPath { get; init; }

    /// <summary>
    /// Gets the CA certificate file path.
    /// </summary>
    public string? CaCertificatePath { get; init; }

    /// <summary>
    /// Gets the NSC lookup value.
    /// </summary>
    public string? NscLookup { get; init; }

    /// <summary>
    /// Gets the JetStream domain.
    /// </summary>
    public string? JetStreamDomain { get; init; }

    /// <summary>
    /// Gets the JetStream API prefix.
    /// </summary>
    public string? JetStreamApiPrefix { get; init; }

    /// <summary>
    /// Gets the JetStream event prefix.
    /// </summary>
    public string? JetStreamEventPrefix { get; init; }

    /// <summary>
    /// Gets the custom inbox prefix.
    /// </summary>
    public string? InboxPrefix { get; init; }

    /// <summary>
    /// Gets the user JWT.
    /// </summary>
    public string? UserJwt { get; init; }

    /// <summary>
    /// Gets the color scheme.
    /// </summary>
    public string? ColorScheme { get; init; }

    /// <summary>
    /// Gets a value indicating whether TLS-first handshake is enabled.
    /// </summary>
    public bool TlsFirst { get; init; }

    /// <summary>
    /// Gets the Windows certificate store type.
    /// </summary>
    public string? WindowsCertStoreType { get; init; }

    /// <summary>
    /// Gets the Windows certificate store match-by field.
    /// </summary>
    public string? WindowsCertStoreMatchBy { get; init; }

    /// <summary>
    /// Gets the Windows certificate store match value.
    /// </summary>
    public string? WindowsCertStoreMatch { get; init; }

    /// <summary>
    /// Gets the Windows CA certificate match patterns.
    /// </summary>
    public string[]? WindowsCertStoreCaMatch { get; init; }

    internal static NatsContextSettings FromModel(NatsContextJsonModel model) => new()
    {
        Name = model.Name,
        Description = model.Description,
        Url = model.Url,
        SocksProxy = model.SocksProxy,
        Token = ExpandHome(model.Token),
        Username = model.User,
        Password = model.Password,
        CredentialsPath = ExpandHome(model.Creds),
        NKeyPath = ExpandHome(model.NKey),
        CertificatePath = ExpandHome(model.Cert),
        KeyPath = ExpandHome(model.Key),
        CaCertificatePath = ExpandHome(model.CA),
        NscLookup = model.NSCLookup,
        JetStreamDomain = model.JSDomain,
        JetStreamApiPrefix = model.JSAPIPrefix,
        JetStreamEventPrefix = model.JSEventPrefix,
        InboxPrefix = model.InboxPrefix,
        UserJwt = model.UserJwt,
        ColorScheme = model.ColorScheme,
        TlsFirst = model.TlsFirst,
        WindowsCertStoreType = model.WinCertStoreType,
        WindowsCertStoreMatchBy = model.WinCertStoreMatchBy,
        WindowsCertStoreMatch = model.WinCertStoreMatch,
        WindowsCertStoreCaMatch = model.WinCertStoreCaMatch,
    };

    private static string? ExpandHome(string? path)
    {
        if (path is not { Length: > 0 } || path[0] != '~')
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            return path;
        }

        return home + path.Substring(1);
    }
}
