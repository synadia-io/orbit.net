// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable SA1600 // Elements should be documented (internal helpers)

using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

internal static class BatchPublishHelper
{
    internal static void ThrowBatchPublishException(BatchPublishErrorResponse error)
    {
        var apiError = new ApiError
        {
            Code = error.Code,
            ErrCode = error.ErrCode,
            Description = error.Description,
        };

        throw error.ErrCode switch
        {
            NatsJSBatchPublishException.ErrCodeNotEnabled => new NatsJSBatchPublishNotEnabledException(apiError),
            NatsJSBatchPublishException.ErrCodeMissingSeq => new NatsJSBatchPublishMissingSeqException(apiError),
            NatsJSBatchPublishException.ErrCodeIncomplete => new NatsJSBatchPublishIncompleteException(apiError),
            NatsJSBatchPublishException.ErrCodeUnsupportedHeader => new NatsJSBatchPublishUnsupportedHeaderException(apiError),
            NatsJSBatchPublishException.ErrCodeExceedsLimit => new NatsJSBatchPublishExceedsLimitException(apiError),
            _ => new NatsJSBatchPublishException(apiError),
        };
    }

    internal static NatsHeaders CloneHeaders(NatsHeaders? source)
    {
        var clone = new NatsHeaders();
        if (source == null)
        {
            return clone;
        }

        foreach (var kv in source)
        {
            clone[kv.Key] = kv.Value;
        }

        return clone;
    }

    internal static void ApplyBatchMessageOptions(NatsHeaders headers, NatsJSBatchMsgOpts? opts)
    {
        if (opts == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(opts.LastSubject) && !opts.LastSubjectSeq.HasValue)
        {
            throw new ArgumentException("LastSubjectSeq is required when LastSubject is set", nameof(opts));
        }

        if (opts.Ttl.HasValue)
        {
            // Format as Go duration string (e.g. "5s") which the server parses via time.ParseDuration.
            // Minimum TTL is 1 second; matches the format used by NATS .NET KV store.
            headers["Nats-TTL"] = $"{(long)opts.Ttl.Value.TotalSeconds:D}s";
        }

        if (!string.IsNullOrEmpty(opts.Stream))
        {
            headers["Nats-Expected-Stream"] = opts.Stream;
        }

        if (opts.LastSeq.HasValue)
        {
            headers["Nats-Expected-Last-Sequence"] = opts.LastSeq.Value.ToString();
        }

        if (opts.LastSubjectSeq.HasValue)
        {
            headers["Nats-Expected-Last-Subject-Sequence"] = opts.LastSubjectSeq.Value.ToString();
        }

        if (!string.IsNullOrEmpty(opts.LastSubject))
        {
            headers["Nats-Expected-Last-Subject-Sequence-Subject"] = opts.LastSubject;
            headers["Nats-Expected-Last-Subject-Sequence"] = opts.LastSubjectSeq!.Value.ToString();
        }
    }

    internal static CancellationTokenSource CreateCommitCancellationTokenSource(CancellationToken cancellationToken, TimeSpan requestTimeout)
    {
        var cts = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();
        cts.CancelAfter(requestTimeout);
        return cts;
    }

    internal static BatchPublishApiResponse? DeserializeApiResponse(byte[]? data)
    {
        if (data == null || data.Length == 0)
        {
            return null;
        }

#if NET8_0_OR_GREATER
        return JsonSerializer.Deserialize(data, BatchPublishJsonSerializerContext.Default.BatchPublishApiResponse);
#else
        return JsonSerializer.Deserialize<BatchPublishApiResponse>(data);
#endif
    }

    internal static BatchPublishAckResponse? DeserializeAckResponse(byte[]? data)
    {
        if (data == null || data.Length == 0)
        {
            return null;
        }

#if NET8_0_OR_GREATER
        return JsonSerializer.Deserialize(data, BatchPublishJsonSerializerContext.Default.BatchPublishAckResponse);
#else
        return JsonSerializer.Deserialize<BatchPublishAckResponse>(data);
#endif
    }
}
