// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Extensions;

/// <summary>
/// A message retrieved from a JetStream stream, encapsulating the message data and metadata.
/// </summary>
/// <typeparam name="T">The type of the message data.</typeparam>
public readonly record struct NatsStreamMsg<T>(
    T? Data,
    ulong Sequence,
    string Subject,
    DateTimeOffset Time,
    NatsHeaders? Headers)
{
    private const string NatsSequenceHeader = "Nats-Sequence";
    private const string NatsTimeStampHeader = "Nats-Time-Stamp";
    private const string NatsSubjectHeader = "Nats-Subject";

    /// <summary>
    /// Creates a <see cref="NatsStreamMsg{T}"/> from a <see cref="NatsMsg{T}"/> returned by a direct get.
    /// </summary>
    /// <param name="msg">The message returned by the direct get API.</param>
    /// <returns>A <see cref="NatsStreamMsg{T}"/> containing the message data and metadata.</returns>
    /// <exception cref="ArgumentNullException">The <paramref name="msg"/> is null.</exception>
    /// <exception cref="NatsJSNoMessageFoundException">The message was not found (404).</exception>
    /// <exception cref="NatsJSException">The server responded with an error status other than 404, or the response is missing the Nats-Subject, Nats-Sequence or Nats-Time-Stamp header needed to reconstruct the message.</exception>
    public static NatsStreamMsg<T> FromDirect(NatsMsg<T> msg)
    {
        if (EqualityComparer<NatsMsg<T>>.Default.Equals(msg, default))
        {
            throw new ArgumentNullException(nameof(msg));
        }

        if (msg.Headers is { Code: var code } && code != 0)
        {
            if (code == 404)
            {
                throw new NatsJSNoMessageFoundException();
            }

            msg.Headers.TryGetLastValue("Description", out string? description);
            throw new NatsJSException(!string.IsNullOrEmpty(description) ? description : msg.Headers.MessageText);
        }

        if (msg.Headers is { Error: { } error })
        {
            throw error;
        }

        // The direct get response carries the stored message's identity in headers only, so
        // every one of them has to be there for the message to be reconstructed at all.
        if (msg.Headers is not { } headers)
        {
            throw new NatsJSException("Direct get response has no headers");
        }

        var sequenceString = headers[NatsSequenceHeader];
        if (StringValues.IsNullOrEmpty(sequenceString))
        {
            throw new NatsJSException($"Missing {NatsSequenceHeader} header");
        }

        if (!ulong.TryParse(sequenceString, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sequence))
        {
            throw new NatsJSException($"Invalid {NatsSequenceHeader} header value: {sequenceString}");
        }

        var timeString = headers[NatsTimeStampHeader];
        if (StringValues.IsNullOrEmpty(timeString))
        {
            throw new NatsJSException($"Missing {NatsTimeStampHeader} header");
        }

        if (!DateTimeOffset.TryParse(timeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
        {
            throw new NatsJSException($"Invalid {NatsTimeStampHeader} header value: {timeString}");
        }

        if (!headers.TryGetLastValue(NatsSubjectHeader, out string? subject) || string.IsNullOrEmpty(subject))
        {
            throw new NatsJSException($"Missing {NatsSubjectHeader} header");
        }

        return new NatsStreamMsg<T>(msg.Data, sequence, subject, time, headers);
    }

    /// <summary>
    /// Creates a <see cref="NatsStreamMsg{T}"/> from a <see cref="StreamMsgGetResponse"/> returned by the stream get API.
    /// </summary>
    /// <param name="response">The response from the stream get API.</param>
    /// <param name="serializer">The deserializer to use for the message data.</param>
    /// <returns>A <see cref="NatsStreamMsg{T}"/> containing the message data and metadata.</returns>
    /// <exception cref="ArgumentNullException">The <paramref name="response"/> or <paramref name="serializer"/> is null.</exception>
    public static NatsStreamMsg<T> FromStreamResponse(StreamMsgGetResponse response, INatsDeserialize<T> serializer)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (serializer is null)
        {
            throw new ArgumentNullException(nameof(serializer));
        }

        var message = response.Message;

        var data = message.Data.IsEmpty ? default : serializer.Deserialize(new ReadOnlySequence<byte>(message.Data), new NatsMsgContext(message.Subject));
        var headers = !string.IsNullOrEmpty(message.Hdrs) ? ParseHeaders(message.Hdrs) : null;

        return new NatsStreamMsg<T>(data, message.Seq, message.Subject, message.Time, headers);
    }

    private static NatsHeaders? ParseHeaders(string? hdrs)
    {
        if (string.IsNullOrEmpty(hdrs))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(hdrs);
        }
        catch (FormatException e)
        {
            throw new NatsJSException("Failed to decode message headers", e);
        }

        var parser = new NatsHeaderParser(Encoding.UTF8);
        var headers = new NatsHeaders();
        if (parser.ParseHeaders(new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes)), headers))
        {
            return headers;
        }

        throw new NatsJSException("Failed to parse message headers");
    }
}
