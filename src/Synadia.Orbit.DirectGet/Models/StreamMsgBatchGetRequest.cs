﻿using System.Text.Json.Serialization;

public record StreamMsgBatchGetRequest
{
    /// <summary>
    /// The maximum amount of messages to be returned for this request
    /// </summary>
    [JsonPropertyName("batch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, ulong.MaxValue)]
    public ulong Batch { get; set; }

    /// <summary>
    /// The maximum amount of returned bytes for this request.
    /// </summary>
    [JsonPropertyName("max_bytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, long.MaxValue)]
    public ulong MaxBytes { get; set; }

    /// <summary>
    /// The minimum sequence for returned message
    /// </summary>
    [JsonPropertyName("seq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, ulong.MaxValue)]
    public ulong Seq { get; set; }

    /// <summary>
    /// The minimum start time for returned message
    /// </summary>
    [JsonPropertyName("start_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// The subject used filter messages that should be returned
    /// </summary>
    [JsonPropertyName("next_by_subj")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#if NET6_0
    public string? NextBySubject { get; set; } = default!;
#else
#pragma warning disable SA1206
    public string? NextBySubject { get; set; }

#pragma warning restore SA1206
#endif

    /// <summary>
    /// Return last messages mathing the subjects
    /// </summary>
    [JsonPropertyName("multi_last")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? MultiLastBySubjects { get; set; }

    /// <summary>
    /// Return message after sequence
    /// </summary>
    [JsonPropertyName("up_to_seq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [System.ComponentModel.DataAnnotations.Range(ulong.MinValue, ulong.MaxValue)]
    public ulong UpToSequence { get; set; }

    /// <summary>
    /// Return message after time
    /// </summary>
    [JsonPropertyName("up_to_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTimeOffset UpToTime { get; set; }
}
