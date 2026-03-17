// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Synadia.Orbit.Testing.GoHarness;

/// <summary>
/// Checks for the Go toolchain on PATH and caches the result.
/// </summary>
public static class GoToolchain
{
    private static string? _goPath;

    /// <summary>
    /// Gets the path to the <c>go</c> executable, or <c>null</c> if not found.
    /// </summary>
    /// <returns>The full path to the Go executable, or <c>null</c>.</returns>
    public static string? FindGo()
    {
        if (_goPath != null)
        {
            return _goPath;
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var fileName = isWindows ? "where" : "which";
        var arguments = "go";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                // 'where' on Windows may return multiple lines; take the first
                var firstLine = output.Split('\n')[0].Trim();
                _goPath = firstLine;
                return _goPath;
            }
        }
        catch
        {
            // go not found
        }

        return null;
    }

    /// <summary>
    /// Ensures the Go toolchain is available on PATH.
    /// </summary>
    /// <exception cref="GoNotFoundException">Thrown when <c>go</c> is not found on PATH.</exception>
    public static void EnsureAvailable()
    {
        if (FindGo() == null)
        {
            throw new GoNotFoundException("Go toolchain not found on PATH. Install Go from https://go.dev/dl/ and ensure 'go' is available.");
        }
    }
}
