// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NATS.Client.Core;

namespace Synadia.Orbit.TestUtils;

/// <summary>
/// NATS client utility extensions for test reliability.
/// </summary>
public static class NatsUtils
{
    /// <summary>
    /// Connects to the NATS server with retries to handle transient startup timing issues.
    /// </summary>
    /// <param name="client">The NATS client to connect.</param>
    /// <param name="timeout">Maximum time to keep retrying. Defaults to 30 seconds.</param>
    /// <returns>A task representing the connect operation.</returns>
    public static async Task ConnectRetryAsync(this INatsClient client, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        Exception? exception = null;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                await client.ConnectAsync().ConfigureAwait(false);
                exception = null;
                break;
            }
            catch (Exception e)
            {
                exception = e;
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        if (exception != null)
        {
            throw exception;
        }
    }
}
