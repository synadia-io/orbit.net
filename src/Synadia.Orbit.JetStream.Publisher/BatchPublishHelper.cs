// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable SA1600 // Elements should be documented (internal helpers)

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Synadia.Orbit.JetStream.Publisher;

internal static class BatchPublishHelper
{
    private const string ExpectedLastSequence = "Nats-Expected-Last-Sequence";
    private const string ExpectedLastMsgId = "Nats-Expected-Last-Msg-Id";

    [DoesNotReturn]
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
            NatsJSBatchPublishException.ErrCodeTooManyInflight => new NatsJSBatchPublishTooManyInflightException(apiError),
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
            // Server uses time.ParseDuration and enforces a 1-second minimum. Reject sub-second
            // values here so callers don't silently get "0s" (no TTL / immediate expire).
            if (opts.Ttl.Value < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException("Ttl must be at least 1 second", nameof(opts));
            }

            // Format as Go duration string (e.g. "5s") which the server parses via time.ParseDuration.
            headers["Nats-TTL"] = $"{(long)opts.Ttl.Value.TotalSeconds:D}s";
        }

        if (!string.IsNullOrEmpty(opts.Stream))
        {
            headers["Nats-Expected-Stream"] = opts.Stream;
        }

        if (opts.LastSeq.HasValue)
        {
            headers[ExpectedLastSequence] = opts.LastSeq.Value.ToString();
        }

        if (opts.LastSubjectSeq.HasValue)
        {
            headers["Nats-Expected-Last-Subject-Sequence"] = opts.LastSubjectSeq.Value.ToString();
        }

        if (!string.IsNullOrEmpty(opts.LastSubject))
        {
            headers["Nats-Expected-Last-Subject-Sequence-Subject"] = opts.LastSubject;
        }
    }

    // Atomic batches only. Fast-ingest batches carry no batch headers and ADR-50 allows
    // per-message expectation checks throughout, so none of this applies there.
    internal static void ValidateBatchHeaders(NatsHeaders headers, bool isFirstMessage)
    {
        // ADR-50: only the first message of a batch may carry this. The server kills the whole
        // batch for a later one (10071 when the value doesn't match what the batch has reached,
        // 10164 when it does), by which point there's nothing to say which message caused it.
        if (!isFirstMessage && headers.ContainsKey(ExpectedLastSequence))
        {
            throw new ArgumentException($"{ExpectedLastSequence} is only allowed on the first message of a batch");
        }

        // Refused by the server inside a batch with 10177.
        if (headers.ContainsKey(ExpectedLastMsgId))
        {
            throw new ArgumentException($"{ExpectedLastMsgId} is not supported inside a batch");
        }

        // Written by the publisher when the batch is committed. A user header of the same name
        // survives the clone on an add and would commit the batch early.
        if (headers.ContainsKey(NatsJSBatchHeaders.BatchCommit))
        {
            throw new ArgumentException($"{NatsJSBatchHeaders.BatchCommit} is set by the publisher and must not be supplied");
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

    internal static NatsHeaders CloneAndApplyMsgOpts(NatsHeaders? src, NatsJSBatchMsgOpts? opts)
    {
        var headers = CloneHeaders(src);
        ApplyBatchMessageOptions(headers, opts);
        return headers;
    }

    internal static Exception FastPublishExceptionFor(BatchPublishErrorResponse err)
    {
        var apiError = new ApiError { Code = err.Code, ErrCode = err.ErrCode, Description = err.Description };
        return err.ErrCode switch
        {
            NatsJSFastPublishException.ErrCodeNotEnabled => new NatsJSFastPublishException(apiError),
            NatsJSFastPublishException.ErrCodeInvalidPattern => new NatsJSFastPublishException(apiError),
            NatsJSFastPublishException.ErrCodeInvalidId => new NatsJSFastPublishException(apiError),
            NatsJSFastPublishException.ErrCodeUnknownId => new NatsJSFastPublishException(apiError),
            NatsJSFastPublishException.ErrCodeTooManyInflight => new NatsJSFastPublishException(apiError),
            _ => new NatsJSApiException(apiError),
        };
    }
}
