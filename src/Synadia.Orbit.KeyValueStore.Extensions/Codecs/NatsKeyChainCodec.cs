// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.KeyValueStore.Extensions.Codecs;

/// <summary>
/// Applies multiple key codecs in sequence.
/// Encoding is applied in order (first to last), decoding in reverse order (last to first).
/// </summary>
public sealed class NatsKeyChainCodec : INatsFilterableKeyCodec
{
    private readonly INatsKeyCodec[] _codecs;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsKeyChainCodec"/> class.
    /// </summary>
    /// <param name="codecs">The codecs to chain together. At least one codec must be provided.</param>
    /// <exception cref="ArgumentException">Thrown when no codecs are provided.</exception>
    public NatsKeyChainCodec(params INatsKeyCodec[] codecs)
    {
        if (codecs == null || codecs.Length == 0)
        {
            throw new ArgumentException("At least one codec must be provided.", nameof(codecs));
        }

        _codecs = codecs;
    }

    /// <inheritdoc/>
    public string EncodeKey(string key)
    {
        var result = key;
        for (var i = 0; i < _codecs.Length; i++)
        {
            try
            {
                result = _codecs[i].EncodeKey(result);
            }
            catch (Exception ex) when (ex is not NatsKeyCodecException)
            {
                throw new NatsKeyCodecException($"Failed to encode key at codec {i}.", ex);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public string DecodeKey(string key)
    {
        var result = key;
        for (var i = _codecs.Length - 1; i >= 0; i--)
        {
            try
            {
                result = _codecs[i].DecodeKey(result);
            }
            catch (Exception ex) when (ex is not NatsKeyCodecException)
            {
                throw new NatsKeyCodecException($"Failed to decode key at codec {i}.", ex);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="NatsKeyCodecException">Thrown when any codec in the chain does not support filtering.</exception>
    public string EncodeFilter(string filter)
    {
        // First, verify all codecs support filtering
        for (var i = 0; i < _codecs.Length; i++)
        {
            if (_codecs[i] is not INatsFilterableKeyCodec)
            {
                throw new NatsKeyCodecException($"Codec at index {i} does not support wildcard filtering.");
            }
        }

        // All codecs support filtering, apply them in sequence
        var result = filter;
        for (var i = 0; i < _codecs.Length; i++)
        {
            try
            {
                result = ((INatsFilterableKeyCodec)_codecs[i]).EncodeFilter(result);
            }
            catch (Exception ex) when (ex is not NatsKeyCodecException)
            {
                throw new NatsKeyCodecException($"Failed to encode filter at codec {i}.", ex);
            }
        }

        return result;
    }
}
