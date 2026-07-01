// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Constants used by partitioned consumer groups.
/// </summary>
internal static class NatsPcgConstants
{
    /// <summary>
    /// KV bucket name for static consumer group configurations.
    /// </summary>
    internal const string StaticKvBucket = "static-consumer-groups";

    /// <summary>
    /// KV bucket name for elastic consumer group configurations.
    /// </summary>
    internal const string ElasticKvBucket = "elastic-consumer-groups";

    /// <summary>
    /// Priority group name used for consumer pinning.
    /// </summary>
    internal const string PriorityGroupName = "PCG";

    /// <summary>
    /// Message header carrying the server-assigned pinned client id.
    /// </summary>
    internal const string PinIdHeader = "Nats-Pin-Id";

    /// <summary>
    /// JetStream API error codes that mean a consumer with the requested name already
    /// exists (with a different config, still active, or name in use) or that a filtered
    /// work queue consumer would not be unique. All can occur transiently while members
    /// converge on a membership change and are handled by delete-then-recreate.
    /// </summary>
    internal static readonly int[] ConsumerCreateConflictErrCodes =
    {
        10148, // consumer already exists (CREATE with a different config)
        10013, // consumer name already in use
        10105, // consumer already exists and is still active
        10100, // filtered consumer not unique on workqueue stream
    };

    /// <summary>
    /// Default pull request timeout.
    /// </summary>
    internal static readonly TimeSpan PullTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Default ack wait duration.
    /// </summary>
    internal static readonly TimeSpan AckWait = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Default consumer idle timeout (inactive threshold).
    /// </summary>
    internal static readonly TimeSpan ConsumerIdleTimeout = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Minimum delay for reconnect backoff.
    /// </summary>
    internal static readonly TimeSpan MinReconnectDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum delay for reconnect backoff.
    /// </summary>
    internal static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Minimum backoff a non-pinned member waits on a membership change,
    /// giving the pinned member a chance to delete and recreate the consumer first.
    /// </summary>
    internal static readonly TimeSpan MembershipBackoffMin = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Maximum backoff a non-pinned member waits on a membership change.
    /// </summary>
    internal static readonly TimeSpan MembershipBackoffMax = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Interval for self-healing checks.
    /// </summary>
    internal static readonly TimeSpan SelfHealInterval = TimeSpan.FromSeconds(7);
}
