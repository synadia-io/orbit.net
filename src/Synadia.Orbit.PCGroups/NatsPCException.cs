// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// Base exception for partitioned consumer group errors.
/// </summary>
public class NatsPCException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsPCException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsPCException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a consumer group configuration is invalid.
/// </summary>
public class NatsPCConfigurationException : NatsPCException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsPCConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsPCConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a member is not in the consumer group membership.
/// </summary>
public class NatsPCMembershipException : NatsPCException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCMembershipException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NatsPCMembershipException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPCMembershipException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NatsPCMembershipException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
