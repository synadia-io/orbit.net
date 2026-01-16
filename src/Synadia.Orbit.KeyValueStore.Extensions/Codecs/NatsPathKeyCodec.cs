// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.KeyValueStore.Extensions.Codecs;

/// <summary>
/// A codec that translates between path-style keys (using '/') and NATS subject notation (using '.').
/// </summary>
/// <remarks>
/// This codec is useful when you want to use familiar path-style keys like "/users/123/profile"
/// which get translated to NATS-compatible keys like "_root_.users.123.profile".
/// </remarks>
public sealed class NatsPathKeyCodec : INatsFilterableKeyCodec
{
    /// <summary>
    /// The prefix used to encode keys that start with a leading slash.
    /// Since NATS subjects cannot start with a dot, we replace the leading slash
    /// with this prefix to maintain round-trip compatibility.
    /// </summary>
    internal const string RootPrefix = "_root_";

    private NatsPathKeyCodec()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="NatsPathKeyCodec"/>.
    /// </summary>
    public static NatsPathKeyCodec Instance { get; } = new();

    /// <inheritdoc/>
    public string EncodeKey(string key)
    {
        // Handle leading / by replacing with _root_
        if (key.StartsWith("/"))
        {
            if (key == "/")
            {
                return RootPrefix;
            }

            key = RootPrefix + "." + key.Substring(1);
        }

        // Trim trailing / as subjects do not allow trailing .
        key = key.TrimEnd('/');

        return key.Replace('/', '.');
    }

    /// <inheritdoc/>
    public string DecodeKey(string key)
    {
        // Handle _root_ prefix
        if (key == RootPrefix)
        {
            return "/";
        }

        var prefixWithDot = RootPrefix + ".";
        if (key.StartsWith(prefixWithDot))
        {
            // Remove _root_ prefix and replace . with /
            var result = key.Substring(prefixWithDot.Length).Replace('.', '/');
            return "/" + result;
        }

        return key.Replace('.', '/');
    }

    /// <inheritdoc/>
    public string EncodeFilter(string filter)
    {
        // For path codec, filter encoding is the same as key encoding
        // since wildcards (* and >) don't conflict with path characters
        return EncodeKey(filter);
    }
}
