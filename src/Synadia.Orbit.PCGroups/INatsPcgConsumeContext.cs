// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Represents a consume context for a partitioned consumer group.
/// </summary>
public interface INatsPcgConsumeContext : IAsyncDisposable
{
    /// <summary>
    /// Stops the consumer from processing messages.
    /// </summary>
    void Stop();

    /// <summary>
    /// Waits for the consumer to finish processing and returns any exception that occurred.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the wait.</param>
    /// <returns>An exception if one occurred, otherwise null.</returns>
    Task<Exception?> WaitAsync(CancellationToken cancellationToken = default);
}
