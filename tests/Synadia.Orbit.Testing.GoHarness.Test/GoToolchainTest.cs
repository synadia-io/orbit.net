// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.Testing.GoHarness.Test;

public class GoToolchainTest
{
    [Fact]
    public void FindGo_returns_path()
    {
        var goPath = GoToolchain.FindGo();
        Assert.NotNull(goPath);
        Assert.Contains("go", goPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAvailable_does_not_throw()
    {
        GoToolchain.EnsureAvailable();
    }
}
