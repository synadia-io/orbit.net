// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Synadia.Orbit.KeyValueStore.Extensions;

/// <summary>
/// A wrapper around <see cref="INatsKVStore"/> that applies key encoding/decoding using a codec.
/// </summary>
public sealed class NatsKVCodecStore : INatsKVStore
{
    private readonly INatsKVStore _store;
    private readonly IKeyCodec _keyCodec;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsKVCodecStore"/> class.
    /// </summary>
    /// <param name="store">The underlying KV store to wrap.</param>
    /// <param name="keyCodec">The codec to use for key encoding/decoding.</param>
    public NatsKVCodecStore(INatsKVStore store, IKeyCodec keyCodec)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _keyCodec = keyCodec ?? throw new ArgumentNullException(nameof(keyCodec));
    }

    /// <inheritdoc/>
    public INatsJSContext JetStreamContext => _store.JetStreamContext;

    /// <inheritdoc/>
    public string Bucket => _store.Bucket;

    /// <inheritdoc/>
    public ValueTask<ulong> PutAsync<T>(string key, T value, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.PutAsync(encodedKey, value, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult<ulong>> TryPutAsync<T>(string key, T value, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryPutAsync(encodedKey, value, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ulong> CreateAsync<T>(string key, T value, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.CreateAsync(encodedKey, value, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ulong> CreateAsync<T>(string key, T value, TimeSpan ttl, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.CreateAsync(encodedKey, value, ttl, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult<ulong>> TryCreateAsync<T>(string key, T value, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryCreateAsync(encodedKey, value, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult<ulong>> TryCreateAsync<T>(string key, T value, TimeSpan ttl, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryCreateAsync(encodedKey, value, ttl, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ulong> UpdateAsync<T>(string key, T value, ulong revision, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.UpdateAsync(encodedKey, value, revision, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult<ulong>> TryUpdateAsync<T>(string key, T value, ulong revision, INatsSerialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryUpdateAsync(encodedKey, value, revision, serializer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key, NatsKVDeleteOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.DeleteAsync(encodedKey, opts, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult> TryDeleteAsync(string key, NatsKVDeleteOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryDeleteAsync(encodedKey, opts, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask PurgeAsync(string key, NatsKVDeleteOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.PurgeAsync(encodedKey, opts, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask PurgeAsync(string key, TimeSpan ttl, NatsKVDeleteOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.PurgeAsync(encodedKey, ttl, opts, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult> TryPurgeAsync(string key, NatsKVDeleteOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryPurgeAsync(encodedKey, opts, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<NatsResult> TryPurgeAsync(string key, TimeSpan ttl, NatsKVDeleteOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return _store.TryPurgeAsync(encodedKey, ttl, opts, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<NatsKVEntry<T>> GetEntryAsync<T>(string key, ulong revision = default, INatsDeserialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        var entry = await _store.GetEntryAsync<T>(encodedKey, revision, serializer, cancellationToken).ConfigureAwait(false);
        return DecodeEntry(entry, key);
    }

    /// <inheritdoc/>
    public async ValueTask<NatsResult<NatsKVEntry<T>>> TryGetEntryAsync<T>(string key, ulong revision = default, INatsDeserialize<T>? serializer = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        var result = await _store.TryGetEntryAsync<T>(encodedKey, revision, serializer, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            return new NatsResult<NatsKVEntry<T>>(DecodeEntry(result.Value, key));
        }

        return result;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<NatsKVEntry<T>> WatchAsync<T>(string key, INatsDeserialize<T>? serializer = default, NatsKVWatchOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = EncodeFilter(key);
        return DecodeEntriesAsync(_store.WatchAsync<T>(encodedKey, serializer, opts, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<NatsKVEntry<T>> WatchAsync<T>(IEnumerable<string> keys, INatsDeserialize<T>? serializer = default, NatsKVWatchOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKeys = keys.Select(EncodeFilter);
        return DecodeEntriesAsync(_store.WatchAsync<T>(encodedKeys, serializer, opts, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<NatsKVEntry<T>> WatchAsync<T>(INatsDeserialize<T>? serializer = default, NatsKVWatchOpts? opts = default, CancellationToken cancellationToken = default)
    {
        // Watch all - no filter encoding needed, but we still need to decode keys in results
        return DecodeEntriesAsync(_store.WatchAsync<T>(serializer, opts, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<NatsKVEntry<T>> HistoryAsync<T>(string key, INatsDeserialize<T>? serializer = default, NatsKVWatchOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedKey = _keyCodec.EncodeKey(key);
        return DecodeEntriesAsync(_store.HistoryAsync<T>(encodedKey, serializer, opts, cancellationToken), cancellationToken, key);
    }

    /// <inheritdoc/>
    public ValueTask<NatsKVStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetStatusAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask PurgeDeletesAsync(NatsKVPurgeOpts? opts = default, CancellationToken cancellationToken = default)
    {
        return _store.PurgeDeletesAsync(opts, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> GetKeysAsync(NatsKVWatchOpts? opts = default, CancellationToken cancellationToken = default)
    {
        return DecodeKeysAsync(_store.GetKeysAsync(opts, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> GetKeysAsync(IEnumerable<string> filters, NatsKVWatchOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var encodedFilters = filters.Select(EncodeFilter);
        return DecodeKeysAsync(_store.GetKeysAsync(encodedFilters, opts, cancellationToken), cancellationToken);
    }

    private string EncodeFilter(string filter)
    {
        if (_keyCodec is IFilterableKeyCodec filterableCodec)
        {
            return filterableCodec.EncodeFilter(filter);
        }

        // Check if filter contains wildcards
        if (filter.Contains("*") || filter.Contains(">"))
        {
            throw new KeyCodecException($"Codec does not support wildcard filtering. Key: '{filter}'");
        }

        return _keyCodec.EncodeKey(filter);
    }

    private NatsKVEntry<T> DecodeEntry<T>(NatsKVEntry<T> entry, string? originalKey = null)
    {
        var decodedKey = originalKey ?? _keyCodec.DecodeKey(entry.Key);
        return new NatsKVEntry<T>(entry.Bucket, decodedKey)
        {
            Value = entry.Value,
            Revision = entry.Revision,
            Delta = entry.Delta,
            Created = entry.Created,
            Operation = entry.Operation,
            Error = entry.Error,
        };
    }

    private async IAsyncEnumerable<NatsKVEntry<T>> DecodeEntriesAsync<T>(
        IAsyncEnumerable<NatsKVEntry<T>> entries,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string? originalKey = null)
    {
        await foreach (var entry in entries.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return DecodeEntry(entry, originalKey);
        }
    }

    private async IAsyncEnumerable<string> DecodeKeysAsync(
        IAsyncEnumerable<string> keys,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var key in keys.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return _keyCodec.DecodeKey(key);
        }
    }
}
