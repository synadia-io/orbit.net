// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Synadia.Orbit.ParameterizedSubject;

namespace Synadia.Orbit.Benchmark;

/// <summary>
/// Compares ToNatsSubject against raw interpolation (unsafe) and interpolation
/// with manual sanitization (what you'd have to write yourself).
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ParameterizedSubjectVsStringConcatBenchmarks
{
    private readonly string _param1 = "alice";
    private readonly string _param2 = "login";
    private readonly string _param3 = "click";

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Single")]
    public string Single_Interpolation_Unsafe()
        => $"user.login.{_param1}";

    [Benchmark]
    [BenchmarkCategory("Single")]
    public string Single_Interpolation_ManualSanitize()
        => $"user.login.{ManualSanitize(_param1)}";

    [Benchmark]
    [BenchmarkCategory("Single")]
    public string Single_ToNatsSubject()
        => "user.login.?".ToNatsSubject(_param1);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Multi")]
    public string Multi_Interpolation_Unsafe()
        => $"a.{_param1}.b.{_param2}.c.{_param3}";

    [Benchmark]
    [BenchmarkCategory("Multi")]
    public string Multi_Interpolation_ManualSanitize()
        => $"a.{ManualSanitize(_param1)}.b.{ManualSanitize(_param2)}.c.{ManualSanitize(_param3)}";

    [Benchmark]
    [BenchmarkCategory("Multi")]
    public string Multi_ToNatsSubject()
        => "a.?.b.?.c.?".ToNatsSubject(_param1, _param2, _param3);

    /// <summary>
    /// What a user would typically write to sanitize a subject token by hand:
    /// chain of Replace calls for each unsafe character.
    /// </summary>
    private static string ManualSanitize(string value)
    {
        return value
            .Replace("%", "%25")
            .Replace(" ", "%20")
            .Replace("\t", "%09")
            .Replace("\r", "%0D")
            .Replace("\n", "%0A")
            .Replace(".", "%2E")
            .Replace("*", "%2A")
            .Replace(">", "%3E");
    }
}
