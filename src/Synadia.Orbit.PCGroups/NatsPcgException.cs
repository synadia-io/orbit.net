// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Base exception for partitioned consumer group errors.
/// </summary>
public class NatsPcgException : NatsException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsPcgException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsPcgException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a consumer group configuration is invalid.
/// </summary>
public class NatsPcgConfigurationException : NatsPcgException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsPcgConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsPcgConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a member is not in the consumer group membership.
/// </summary>
public class NatsPcgMembershipException : NatsPcgException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgMembershipException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsPcgMembershipException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgMembershipException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsPcgMembershipException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
