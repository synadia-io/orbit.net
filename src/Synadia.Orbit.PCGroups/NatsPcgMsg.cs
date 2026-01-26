// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Synadia.Orbit.PCGroups;

/// <summary>
/// A message received from a partitioned consumer group.
/// Wraps <see cref="NatsJSMsg{T}"/> and provides a subject with the partition prefix stripped.
/// </summary>
/// <typeparam name="T">The type of the message data.</typeparam>
public readonly struct NatsPcgMsg<T>
{
    private readonly NatsJSMsg<T> _msg;
    private readonly string _subject;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPcgMsg{T}"/> struct.
    /// </summary>
    /// <param name="msg">The underlying JetStream message.</param>
    /// <param name="subject">The subject with partition prefix stripped.</param>
    internal NatsPcgMsg(NatsJSMsg<T> msg, string subject)
    {
        _msg = msg;
        _subject = subject;
    }

    /// <summary>
    /// Gets the subject of the message with the partition prefix stripped.
    /// </summary>
    public string Subject => _subject;

    /// <summary>
    /// Gets the deserialized message data.
    /// </summary>
    public T? Data => _msg.Data;

    /// <summary>
    /// Gets the message headers if set.
    /// </summary>
    public NatsHeaders? Headers => _msg.Headers;

    /// <summary>
    /// Gets the reply subject.
    /// </summary>
    public string? ReplyTo => _msg.ReplyTo;

    /// <summary>
    /// Gets the message size in bytes.
    /// </summary>
    public int Size => _msg.Size;

    /// <summary>
    /// Gets additional metadata about the message.
    /// </summary>
    public NatsJSMsgMetadata? Metadata => _msg.Metadata;

    /// <summary>
    /// Gets any errors encountered while processing the message.
    /// </summary>
    public NatsException? Error => _msg.Error;

    /// <summary>
    /// Throws an exception if the message contains any errors.
    /// </summary>
    public void EnsureSuccess() => _msg.EnsureSuccess();

    /// <summary>
    /// Acknowledges the message was completely handled.
    /// </summary>
    /// <param name="opts">Ack options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the call.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async call.</returns>
    public ValueTask AckAsync(AckOpts? opts = default, CancellationToken cancellationToken = default)
        => _msg.AckAsync(opts, cancellationToken);

    /// <summary>
    /// Signals that the message will not be processed now and processing can move onto the next message.
    /// </summary>
    /// <param name="delay">Delay redelivery of the message.</param>
    /// <param name="opts">Ack options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the call.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async call.</returns>
    public ValueTask NakAsync(AckOpts? opts = default, TimeSpan delay = default, CancellationToken cancellationToken = default)
        => _msg.NakAsync(opts, delay, cancellationToken);

    /// <summary>
    /// Indicates that work is ongoing and the wait period should be extended.
    /// </summary>
    /// <param name="opts">Ack options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the call.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async call.</returns>
    public ValueTask AckProgressAsync(AckOpts? opts = default, CancellationToken cancellationToken = default)
        => _msg.AckProgressAsync(opts, cancellationToken);

    /// <summary>
    /// Instructs the server to stop redelivery of the message without acknowledging it as successfully processed.
    /// </summary>
    /// <param name="opts">Ack options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the call.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async call.</returns>
    public ValueTask AckTerminateAsync(AckOpts? opts = default, CancellationToken cancellationToken = default)
        => _msg.AckTerminateAsync(opts, cancellationToken);

    /// <summary>
    /// Acknowledges the message and waits for confirmation from the server.
    /// This is also known as a "double ack" - the server will acknowledge receipt of your acknowledgment.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the call.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async call.</returns>
    /// <remarks>
    /// Use this method when you need guaranteed confirmation that the server has processed your acknowledgment.
    /// This is useful for exactly-once processing scenarios.
    /// </remarks>
    public ValueTask DoubleAckAsync(CancellationToken cancellationToken = default)
        => _msg.AckAsync(new AckOpts { DoubleAck = true }, cancellationToken);

    /// <summary>
    /// Strips the partition prefix from a subject.
    /// </summary>
    /// <param name="subject">The subject with partition prefix.</param>
    /// <returns>The subject without the partition prefix.</returns>
    internal static string StripPartitionPrefix(string subject)
    {
        var dotIndex = subject.IndexOf('.');
        if (dotIndex >= 0 && dotIndex < subject.Length - 1)
        {
            return subject.Substring(dotIndex + 1);
        }

        return subject;
    }
}
