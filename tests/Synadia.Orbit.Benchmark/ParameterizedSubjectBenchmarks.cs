// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Synadia.Orbit.Benchmark.Baseline;
using Synadia.Orbit.ParameterizedSubject;

namespace Synadia.Orbit.Benchmark;

[ShortRunJob]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ParameterizedSubjectBenchmarks
{
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SingleClean")]
    public string SingleClean_Baseline()
        => ParameterizedSubjectBaseline.Parameterize("user.login.?", "alice");

    [Benchmark]
    [BenchmarkCategory("SingleClean")]
    public string SingleClean_Current()
        => "user.login.?".ToNatsSubject("alice");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MultiClean")]
    public string MultiClean_Baseline()
        => ParameterizedSubjectBaseline.Parameterize("a.?.b.?.c.?", "x", "y", "z");

    [Benchmark]
    [BenchmarkCategory("MultiClean")]
    public string MultiClean_Current()
        => "a.?.b.?.c.?".ToNatsSubject("x", "y", "z");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SingleEncode")]
    public string SingleEncode_Baseline()
        => ParameterizedSubjectBaseline.Parameterize("files.?", "v1.2");

    [Benchmark]
    [BenchmarkCategory("SingleEncode")]
    public string SingleEncode_Current()
        => "files.?".ToNatsSubject("v1.2");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MultiEncode")]
    public string MultiEncode_Baseline()
        => ParameterizedSubjectBaseline.Parameterize("a.?.b.?.c.?", "clean", "x*y>z%", "v1.2");

    [Benchmark]
    [BenchmarkCategory("MultiEncode")]
    public string MultiEncode_Current()
        => "a.?.b.?.c.?".ToNatsSubject("clean", "x*y>z%", "v1.2");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NoPlaceholders")]
    public string NoPlaceholders_Baseline()
        => ParameterizedSubjectBaseline.Parameterize("a.b.c");

    [Benchmark]
    [BenchmarkCategory("NoPlaceholders")]
    public string NoPlaceholders_Current()
        => "a.b.c".ToNatsSubject();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EnsureSanitized")]
    public void EnsureSanitized_Baseline()
        => ParameterizedSubjectBaseline.EnsureSanitized("hello-world_123");

    [Benchmark]
    [BenchmarkCategory("EnsureSanitized")]
    public void EnsureSanitized_Current()
        => "hello-world_123".EnsureValidNatsSubject();
}
