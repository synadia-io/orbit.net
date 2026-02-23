// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.JetStream;

namespace Synadia.Orbit.Counters;

/// <summary>
/// Extension methods for <see cref="INatsJSContext"/> to create counter instances.
/// </summary>
public static class NatsJSCounterExtensions
{
    /// <summary>
    /// Gets a counter for the specified stream name.
    /// The stream must exist and be configured with <c>AllowMsgCounter</c> and <c>AllowDirect</c> enabled.
    /// </summary>
    /// <param name="context">The JetStream context.</param>
    /// <param name="streamName">The name of the stream to use as a counter.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="NatsJSCounter"/> instance for the stream.</returns>
    /// <exception cref="NatsCounterException">
    /// Thrown when the stream is not found, or is not configured for counters.
    /// </exception>
    public static async ValueTask<NatsJSCounter> GetCounterAsync(
        this INatsJSContext context,
        string streamName,
        CancellationToken cancellationToken = default)
    {
        INatsJSStream stream;
        try
        {
            stream = await context.GetStreamAsync(streamName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsJSApiException e) when (e.Error.ErrCode == 10059)
        {
            throw NatsCounterException.CounterNotFound;
        }

        return context.CreateCounter(stream);
    }

    /// <summary>
    /// Creates a counter from an existing stream.
    /// The stream must be configured with <c>AllowMsgCounter</c> and <c>AllowDirect</c> enabled.
    /// </summary>
    /// <param name="context">The JetStream context.</param>
    /// <param name="stream">The JetStream stream to wrap as a counter.</param>
    /// <returns>A <see cref="NatsJSCounter"/> instance for the stream.</returns>
    /// <exception cref="NatsCounterException">
    /// Thrown when the stream is not configured for counters.
    /// </exception>
    public static NatsJSCounter CreateCounter(this INatsJSContext context, INatsJSStream stream)
    {
        if (!stream.Info.Config.AllowMsgCounter)
        {
            throw NatsCounterException.CounterNotEnabled;
        }

        if (!stream.Info.Config.AllowDirect)
        {
            throw NatsCounterException.DirectAccessRequired;
        }

        return new NatsJSCounter(context, stream.Info.Config.Name!);
    }
}
