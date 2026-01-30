// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext;

/// <summary>
/// Loads NATS CLI context files and produces <see cref="NatsOpts"/> for creating connections.
/// </summary>
/// <remarks>
/// Context files are JSON files stored at <c>~/.config/nats/context/*.json</c> (or under
/// <c>$XDG_CONFIG_HOME/nats/context/</c>). The active context name is read from <c>context.txt</c>
/// in the same directory.
/// </remarks>
public static class NatsContext
{
    /// <summary>
    /// Loads a NATS CLI context and returns the corresponding <see cref="NatsOpts"/> and settings.
    /// </summary>
    /// <param name="name">
    /// The context to load. Can be:
    /// <list type="bullet">
    /// <item><description><c>null</c> or empty to use the active context from <c>context.txt</c></description></item>
    /// <item><description>A context name (e.g. <c>"production"</c>)</description></item>
    /// <item><description>An absolute file path to a context JSON file</description></item>
    /// </list>
    /// </param>
    /// <returns>A <see cref="NatsContextResult"/> containing the configured <see cref="NatsOpts"/> and the parsed <see cref="NatsContextSettings"/>.</returns>
    /// <exception cref="NatsContextException">Thrown when the context cannot be found or is invalid.</exception>
    public static NatsContextResult Load(string? name = null)
    {
        var model = NatsContextPathResolver.Resolve(name);
        var settings = NatsContextSettings.FromModel(model);
        var opts = NatsContextOptsFactory.Create(settings);
        return new NatsContextResult(opts, settings);
    }

    /// <summary>
    /// Loads a NATS CLI context, creates a connection, and connects to the NATS server.
    /// </summary>
    /// <param name="name">
    /// The context to load. Can be:
    /// <list type="bullet">
    /// <item><description><c>null</c> or empty to use the active context from <c>context.txt</c></description></item>
    /// <item><description>A context name (e.g. <c>"production"</c>)</description></item>
    /// <item><description>An absolute file path to a context JSON file</description></item>
    /// </list>
    /// </param>
    /// <param name="configureOpts">An optional callback to customize the <see cref="NatsOpts"/> before connecting.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="NatsContextConnection"/> containing the connected <see cref="NatsConnection"/> and the parsed <see cref="NatsContextSettings"/>.</returns>
    /// <exception cref="NatsContextException">Thrown when the context cannot be found or is invalid.</exception>
    public static async Task<NatsContextConnection> ConnectAsync(
        string? name = null,
        Func<NatsOpts, NatsOpts>? configureOpts = null,
        CancellationToken cancellationToken = default)
    {
        var (opts, settings) = Load(name);

        if (configureOpts != null)
        {
            opts = configureOpts(opts);
        }

        var connection = new NatsConnection(opts);
        await connection.ConnectAsync().ConfigureAwait(false);

        return new NatsContextConnection(connection, settings);
    }
}
