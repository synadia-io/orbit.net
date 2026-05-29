// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.PCGroups.Test;

internal static class TaskTestHelpers
{
    public static async Task AssertCompletesWithinAsync(Task task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        Assert.True(ReferenceEquals(task, completed), $"Task did not complete within {timeout}.");
        await Task.WhenAll(task).ConfigureAwait(false);
    }
}
