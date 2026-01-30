// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext;

/// <summary>
/// Represents a loaded NATS CLI context with configured options and settings.
/// Use <see cref="Load"/> to create an instance, then <see cref="ConnectAsync"/> to connect.
/// </summary>
/// <remarks>
/// Context files are JSON files stored at <c>~/.config/nats/context/*.json</c> (or under
/// <c>$XDG_CONFIG_HOME/nats/context/</c>). The active context name is read from <c>context.txt</c>
/// in the same directory.
/// </remarks>
public sealed class NatsContext
{
    private NatsContext(NatsOpts opts, NatsContextSettings settings)
    {
        Opts = opts;
        Settings = settings;
    }

    /// <summary>
    /// Gets the <see cref="NatsOpts"/> configured from the context.
    /// </summary>
    public NatsOpts Opts { get; }

    /// <summary>
    /// Gets the parsed <see cref="NatsContextSettings"/>.
    /// </summary>
    public NatsContextSettings Settings { get; }

    /// <summary>
    /// Loads a NATS CLI context and returns a <see cref="NatsContext"/> instance
    /// containing the corresponding <see cref="NatsOpts"/> and settings.
    /// </summary>
    /// <param name="name">
    /// The context to load. Can be:
    /// <list type="bullet">
    /// <item><description><c>null</c> or empty to use the active context from <c>context.txt</c></description></item>
    /// <item><description>A context name (e.g. <c>"production"</c>)</description></item>
    /// <item><description>An absolute file path to a context JSON file</description></item>
    /// </list>
    /// </param>
    /// <returns>A <see cref="NatsContext"/> instance with configured <see cref="Opts"/> and <see cref="Settings"/>.</returns>
    /// <exception cref="NatsContextException">Thrown when the context cannot be found or is invalid.</exception>
    public static NatsContext Load(string? name = null)
    {
        var model = NatsContextPathResolver.Resolve(name);
        var settings = NatsContextSettings.FromModel(model);
        var opts = NatsContextOptsFactory.Create(settings);
        return new NatsContext(opts, settings);
    }

    /// <summary>
    /// Creates a connection to the NATS server using this context's options.
    /// </summary>
    /// <param name="configureOpts">An optional callback to customize the <see cref="NatsOpts"/> before connecting.</param>
    /// <returns>A connected <see cref="NatsConnection"/>.</returns>
    public async Task<NatsConnection> ConnectAsync(
        Func<NatsOpts, NatsOpts>? configureOpts = null)
    {
        var opts = Opts;

        if (configureOpts != null)
        {
            opts = configureOpts(opts);
        }

        var connection = new NatsConnection(opts);
        await connection.ConnectAsync().ConfigureAwait(false);

        return connection;
    }
}
