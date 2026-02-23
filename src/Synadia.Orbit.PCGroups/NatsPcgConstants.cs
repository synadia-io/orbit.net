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
    /// Interval for self-healing checks.
    /// </summary>
    internal static readonly TimeSpan SelfHealInterval = TimeSpan.FromSeconds(7);
}
