// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Synadia.Orbit.Counters.Models;

namespace Synadia.Orbit.Counters;

/// <summary>
/// Provides operations on a JetStream stream configured for distributed counters.
/// Each subject in the stream is a separate counter.
/// </summary>
public sealed class NatsJSCounter
{
    private const string CounterSourcesHeader = "Nats-Counter-Sources";
    private const string CounterIncrementHeader = "Nats-Incr";

    private readonly INatsJSContext _js;
    private readonly string _streamName;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsJSCounter"/> class.
    /// </summary>
    /// <param name="js">The JetStream context.</param>
    /// <param name="streamName">The name of the counter stream.</param>
    internal NatsJSCounter(INatsJSContext js, string streamName)
    {
        _js = js;
        _streamName = streamName;
    }

    /// <summary>
    /// Increments the counter for the given subject and returns the new total value.
    /// </summary>
    /// <param name="subject">The counter subject.</param>
    /// <param name="value">The value to add (can be negative for decrement).</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>The new total counter value after the increment.</returns>
    public async ValueTask<BigInteger> AddAsync(string subject, BigInteger value, CancellationToken cancellationToken = default)
    {
        var headers = new NatsHeaders { { CounterIncrementHeader, value.ToString() } };
        var pubAck = await _js.PublishAsync<byte[]?>(
            subject: subject,
            data: null,
            headers: headers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(pubAck.Value))
        {
            throw NatsCounterException.MissingCounterValue();
        }

        if (!BigInteger.TryParse(pubAck.Value, out var result))
        {
            throw NatsCounterException.InvalidCounterValue(pubAck.Value);
        }

        return result;
    }

    /// <summary>
    /// Increments the counter for the given subject and returns the new total value.
    /// </summary>
    /// <param name="subject">The counter subject.</param>
    /// <param name="value">The value to add (can be negative for decrement).</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>The new total counter value after the increment.</returns>
    public ValueTask<BigInteger> AddAsync(string subject, long value, CancellationToken cancellationToken = default)
        => AddAsync(subject, new BigInteger(value), cancellationToken);

    /// <summary>
    /// Returns the current value of the counter for the given subject.
    /// </summary>
    /// <param name="subject">The counter subject.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>The current counter value.</returns>
    public async ValueTask<BigInteger> LoadAsync(string subject, CancellationToken cancellationToken = default)
    {
        var msg = await RequestDirectGetAsync(subject, cancellationToken).ConfigureAwait(false);
        return ParseCounterValue(msg.Data);
    }

    /// <summary>
    /// Returns the full entry with value, source history, and increment for the given subject.
    /// </summary>
    /// <param name="subject">The counter subject.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="CounterEntry"/> with the full counter state.</returns>
    public async ValueTask<CounterEntry> GetAsync(string subject, CancellationToken cancellationToken = default)
    {
        var msg = await RequestDirectGetAsync(subject, cancellationToken).ConfigureAwait(false);

        return new CounterEntry
        {
            Subject = subject,
            Value = ParseCounterValue(msg.Data),
            Sources = ParseSources(msg.Headers),
            Increment = ParseIncrement(msg.Headers),
        };
    }

    /// <summary>
    /// Returns an async enumerable of counter entries matching the given subjects.
    /// Wildcards are supported.
    /// </summary>
    /// <param name="subjects">The subject patterns to match.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>An async enumerable of <see cref="CounterEntry"/> items.</returns>
    public async IAsyncEnumerable<CounterEntry> GetManyAsync(
        string[] subjects,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestMany = _js.Connection.RequestManyAsync(
            subject: $"{_js.Opts.Prefix}.DIRECT.GET.{_streamName}",
            data: new DirectGetMultiRequest { MultiLast = subjects },
            requestSerializer: CounterJsonSerializer<DirectGetMultiRequest>.Default,
            replySerializer: CounterJsonSerializer<CounterValuePayload>.Default,
            replyOpts: new NatsSubOpts { StopOnEmptyMsg = true, ThrowIfNoResponders = true },
            cancellationToken: cancellationToken);

        await foreach (var msg in requestMany.ConfigureAwait(false))
        {
            if (msg.Error is { } error)
            {
                throw error;
            }

            string? entrySubject = null;
            if (msg.Headers != null && msg.Headers.TryGetValue("Nats-Subject", out var subjectValues))
            {
                entrySubject = subjectValues.ToString();
            }

            yield return new CounterEntry
            {
                Subject = entrySubject ?? string.Empty,
                Value = ParseCounterValue(msg.Data),
                Sources = ParseSources(msg.Headers),
                Increment = ParseIncrement(msg.Headers),
            };
        }
    }

    private static BigInteger ParseCounterValue(CounterValuePayload? payload)
    {
        if (payload?.Val == null)
        {
            throw NatsCounterException.MissingCounterValue();
        }

        if (!BigInteger.TryParse(payload.Val, out var value))
        {
            throw NatsCounterException.InvalidCounterValue(payload.Val);
        }

        return value;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, BigInteger>>? ParseSources(NatsHeaders? headers)
    {
        if (headers == null || !headers.TryGetValue(CounterSourcesHeader, out var sourcesValues))
        {
            return null;
        }

        var json = sourcesValues.ToString();
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var rawSources = (Dictionary<string, Dictionary<string, string>>?)JsonSerializer.Deserialize(
            json,
            typeof(Dictionary<string, Dictionary<string, string>>),
            CounterJsonSerializerContext.Default);

        if (rawSources == null)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, BigInteger>>();
        foreach (var kvp in rawSources)
        {
            var subjectMap = new Dictionary<string, BigInteger>();
            foreach (var inner in kvp.Value)
            {
                if (!BigInteger.TryParse(inner.Value, out var val))
                {
                    throw NatsCounterException.InvalidCounterValue(inner.Value);
                }

                subjectMap[inner.Key] = val;
            }

            result[kvp.Key] = subjectMap;
        }

        return result;
    }

    private static BigInteger? ParseIncrement(NatsHeaders? headers)
    {
        if (headers == null || !headers.TryGetValue(CounterIncrementHeader, out var incrValues))
        {
            return null;
        }

        var incrStr = incrValues.ToString();
        if (string.IsNullOrEmpty(incrStr))
        {
            return null;
        }

        if (!BigInteger.TryParse(incrStr, out var value))
        {
            throw NatsCounterException.InvalidCounterValue(incrStr);
        }

        return value;
    }

    private async ValueTask<NatsMsg<CounterValuePayload>> RequestDirectGetAsync(string subject, CancellationToken cancellationToken)
    {
        var msg = await _js.Connection.RequestAsync(
            subject: $"{_js.Opts.Prefix}.DIRECT.GET.{_streamName}",
            data: new DirectGetLastRequest { LastBySubj = subject },
            requestSerializer: CounterJsonSerializer<DirectGetLastRequest>.Default,
            replySerializer: CounterJsonSerializer<CounterValuePayload>.Default,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (msg.Headers?.Code == 404)
        {
            throw NatsCounterException.NoCounterForSubject(subject);
        }

        if (msg.Error is { } error)
        {
            throw error;
        }

        return msg;
    }
}
