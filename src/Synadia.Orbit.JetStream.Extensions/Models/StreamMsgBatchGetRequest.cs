// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Synadia.Orbit.JetStream.Extensions.Models;

/// <summary>
/// Represents a request for batch retrieval of stream messages.
/// </summary>
/// <remarks>
/// This class is used to specify parameters for retrieving multiple messages from a stream in batch mode.
/// It includes options to define the number of messages, message size, starting sequence, and other optional filters.
/// </remarks>
public record StreamMsgBatchGetRequest
{
    /// <summary>
    /// Gets or sets the maximum number of messages to be returned for this request.
    /// </summary>
    [JsonPropertyName("batch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, ulong.MaxValue)]
    public ulong Batch { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of returned bytes for this request.
    /// </summary>
    [JsonPropertyName("max_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, long.MaxValue)]
    public ulong MaxBytes { get; set; }

    /// <summary>
    /// Gets or sets the minimum sequence for returned messages.
    /// </summary>
    [JsonPropertyName("seq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, ulong.MaxValue)]
    public ulong Seq { get; set; }

    /// <summary>
    /// Gets or sets the minimum start time for returned messages.
    /// </summary>
    [JsonPropertyName("start_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the subject used filter messages that should be returned.
    /// </summary>
    [JsonPropertyName("next_by_subj")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextBySubject { get; set; }

    /// <summary>
    /// Gets or sets the list of subjects for which the last message in each category will be retrieved.
    /// </summary>
    [JsonPropertyName("multi_last")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? MultiLastBySubjects { get; set; }

    /// <summary>
    /// Gets or sets the maximum sequence number up to which messages should be retrieved in a batch request.
    /// </summary>
    [JsonPropertyName("up_to_seq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, ulong.MaxValue)]
    public ulong UpToSequence { get; set; }

    /// <summary>
    /// Gets or sets the maximum timestamp up to which messages should be retrieved in the batch request.
    /// </summary>
    [JsonPropertyName("up_to_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTimeOffset UpToTime { get; set; }
}
