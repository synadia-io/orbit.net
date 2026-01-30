// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext;

internal static class NatsContextOptsFactory
{
    /// <summary>
    /// Creates <see cref="NatsOpts"/> from context settings.
    /// </summary>
    internal static NatsOpts Create(NatsContextSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.NscLookup))
        {
            throw new NotSupportedException("NSC lookup is not supported; configure credentials directly instead");
        }

        var opts = NatsOpts.Default;

        if (!string.IsNullOrEmpty(settings.Url))
        {
            opts = opts with { Url = settings.Url! };
        }

        opts = opts with { AuthOpts = BuildAuthOpts(settings) };
        opts = opts with { TlsOpts = BuildTlsOpts(settings) };

        if (!string.IsNullOrEmpty(settings.InboxPrefix))
        {
            opts = opts with { InboxPrefix = settings.InboxPrefix! };
        }

        return opts;
    }

    internal static NatsAuthOpts BuildAuthOpts(NatsContextSettings settings)
    {
        var authOpts = NatsAuthOpts.Default;

        // Auth priority (matching Go): User/Password > CredsFile > NKeyFile
        if (!string.IsNullOrEmpty(settings.Username))
        {
            authOpts = authOpts with
            {
                Username = settings.Username,
                Password = settings.Password,
            };
        }
        else if (!string.IsNullOrEmpty(settings.CredentialsPath))
        {
            authOpts = authOpts with
            {
                CredsFile = settings.CredentialsPath,
            };
        }
        else if (!string.IsNullOrEmpty(settings.NKeyPath))
        {
            authOpts = authOpts with
            {
                NKeyFile = settings.NKeyPath,
            };
        }

        // Token is applied independently
        if (!string.IsNullOrEmpty(settings.Token))
        {
            authOpts = authOpts with
            {
                Token = settings.Token,
            };
        }

        // JWT
        if (!string.IsNullOrEmpty(settings.UserJwt))
        {
            authOpts = authOpts with { Jwt = settings.UserJwt };
        }

        return authOpts;
    }

    internal static NatsTlsOpts BuildTlsOpts(NatsContextSettings settings)
    {
        var tlsOpts = NatsTlsOpts.Default;

#if !NETSTANDARD
        if (!string.IsNullOrEmpty(settings.CertificatePath) && !string.IsNullOrEmpty(settings.KeyPath))
        {
            tlsOpts = tlsOpts with
            {
                CertFile = settings.CertificatePath,
                KeyFile = settings.KeyPath,
            };
        }

        if (!string.IsNullOrEmpty(settings.CaCertificatePath))
        {
            tlsOpts = tlsOpts with
            {
                CaFile = settings.CaCertificatePath,
            };
        }
#endif

        if (settings.TlsFirst)
        {
            tlsOpts = tlsOpts with { Mode = TlsMode.Implicit };
        }

        return tlsOpts;
    }
}
