// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Configures a <see cref="NatsJSFastPublisher"/>.
/// </summary>
public record NatsJSFastPublisherOpts
{
    /// <summary>
    /// Gets a value indicating whether the server should continue the batch on a detected gap.
    /// When false (default), the server abandons the batch on a gap. When true, the batch
    /// continues and the gap is reported via <see cref="ErrorHandler"/>.
    /// </summary>
    public bool ContinueOnGap { get; init; }

    /// <summary>
    /// Gets the handler for asynchronous errors (gap reports, server-side per-message errors,
    /// and ack-deserialization failures). Called from the ack-processing loop, so it must not
    /// block.
    /// </summary>
    public Action<Exception>? ErrorHandler { get; init; }

    /// <summary>
    /// Gets the flow control configuration.
    /// </summary>
    public NatsJSFastPublishFlowControl? FlowControl { get; init; }
}
