// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Header constants for batch publishing.
/// </summary>
public static class NatsJSBatchHeaders
{
    /// <summary>
    /// Contains the batch ID for a message in a batch publish.
    /// </summary>
    public const string BatchId = "Nats-Batch-Id";

    /// <summary>
    /// Contains the sequence number of a message within a batch.
    /// </summary>
    public const string BatchSeq = "Nats-Batch-Sequence";

    /// <summary>
    /// Signals the final message in a batch when set to "1".
    /// </summary>
    public const string BatchCommit = "Nats-Batch-Commit";
}
