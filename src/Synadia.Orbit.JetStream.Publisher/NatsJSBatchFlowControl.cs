// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Configures flow control for batch publishing.
/// </summary>
public record NatsJSBatchFlowControl
{
    /// <summary>
    /// Gets a value indicating whether to wait for an ack on the first message in the batch. Default: true.
    /// </summary>
    public bool AckFirst { get; init; } = true;

    /// <summary>
    /// Gets the interval at which to wait for an ack (0 = disabled). Default: 0.
    /// </summary>
    public int AckEvery { get; init; } = 0;

    /// <summary>
    /// Gets the timeout for waiting for acks when flow control is enabled.
    /// </summary>
    public TimeSpan AckTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
