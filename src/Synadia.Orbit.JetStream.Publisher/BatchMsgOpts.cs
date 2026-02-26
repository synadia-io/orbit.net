// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.JetStream.Publisher;

/// <summary>
/// Options for configuring individual batch messages.
/// </summary>
public record BatchMsgOpts
{
    /// <summary>
    /// Gets the per message TTL for batch messages.
    /// </summary>
    public TimeSpan? Ttl { get; init; }

    /// <summary>
    /// Gets the expected stream the message should be published to.
    /// </summary>
    public string? Stream { get; init; }

    /// <summary>
    /// Gets the expected sequence number the last message on a stream should have.
    /// </summary>
    public ulong? LastSeq { get; init; }

    /// <summary>
    /// Gets the expected sequence number the last message on a subject should have.
    /// </summary>
    public ulong? LastSubjectSeq { get; init; }

    /// <summary>
    /// Gets the subject for which the last sequence number should be checked.
    /// </summary>
    public string? LastSubject { get; init; }
}
