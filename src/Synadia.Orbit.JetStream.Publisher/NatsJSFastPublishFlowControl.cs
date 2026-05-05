// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Configures flow control for fast batch publishing.
/// </summary>
public record NatsJSFastPublishFlowControl
{
    /// <summary>
    /// Gets the initial flow value (server ack frequency, in messages). Server may adjust this
    /// dynamically. Default: 100.
    /// </summary>
    public ushort Flow { get; init; } = 100;

    /// <summary>
    /// Gets the maximum number of unacknowledged flow windows before <c>AddAsync</c>/
    /// <c>AddMsgAsync</c> stall awaiting an ack. Default: 2.
    /// </summary>
    public ushort MaxOutstandingAcks { get; init; } = 2;

    /// <summary>
    /// Gets the timeout for waiting on flow acks. When null, the JetStream context default
    /// timeout is used.
    /// </summary>
    public TimeSpan? AckTimeout { get; init; }
}
