// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.NatsContext;

internal sealed class NatsContextJsonModel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("socks_proxy")]
    public string? SocksProxy { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("creds")]
    public string? Creds { get; set; }

    [JsonPropertyName("nkey")]
    public string? NKey { get; set; }

    [JsonPropertyName("cert")]
    public string? Cert { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("ca")]
    public string? CA { get; set; }

    [JsonPropertyName("nsc")]
    public string? NSCLookup { get; set; }

    [JsonPropertyName("jetstream_domain")]
    public string? JSDomain { get; set; }

    [JsonPropertyName("jetstream_api_prefix")]
    public string? JSAPIPrefix { get; set; }

    [JsonPropertyName("jetstream_event_prefix")]
    public string? JSEventPrefix { get; set; }

    [JsonPropertyName("inbox_prefix")]
    public string? InboxPrefix { get; set; }

    [JsonPropertyName("user_jwt")]
    public string? UserJwt { get; set; }

    [JsonPropertyName("color_scheme")]
    public string? ColorScheme { get; set; }

    [JsonPropertyName("tls_first")]
    public bool TlsFirst { get; set; }

    [JsonPropertyName("windows_cert_store")]
    public string? WinCertStoreType { get; set; }

    [JsonPropertyName("windows_cert_match_by")]
    public string? WinCertStoreMatchBy { get; set; }

    [JsonPropertyName("windows_cert_match")]
    public string? WinCertStoreMatch { get; set; }

    [JsonPropertyName("windows_ca_certs_match")]
    public string[]? WinCertStoreCaMatch { get; set; }
}
