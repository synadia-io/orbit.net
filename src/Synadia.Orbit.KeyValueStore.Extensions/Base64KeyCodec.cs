// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;

namespace Synadia.Orbit.KeyValueStore.Extensions;

/// <summary>
/// A codec that encodes keys using URL-safe Base64 encoding.
/// Each token (separated by '.') is encoded separately, preserving the NATS subject structure.
/// </summary>
public sealed class Base64KeyCodec : IFilterableKeyCodec
{
    private Base64KeyCodec()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="Base64KeyCodec"/>.
    /// </summary>
    public static Base64KeyCodec Instance { get; } = new();

    /// <inheritdoc/>
    public string EncodeKey(string key)
    {
        var tokens = key.Split('.');
        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = Base64UrlEncode(tokens[i]);
        }

        return string.Join(".", tokens);
    }

    /// <inheritdoc/>
    public string DecodeKey(string key)
    {
        var tokens = key.Split('.');
        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = Base64UrlDecode(tokens[i]);
        }

        return string.Join(".", tokens);
    }

    /// <inheritdoc/>
    public string EncodeFilter(string filter)
    {
        var tokens = filter.Split('.');
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token != "*" && token != ">")
            {
                tokens[i] = Base64UrlEncode(token);
            }
        }

        return string.Join(".", tokens);
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);

        // Convert to URL-safe Base64 (no padding)
        return base64
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Base64UrlDecode(string input)
    {
        // Convert from URL-safe Base64
        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if needed
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }
}
