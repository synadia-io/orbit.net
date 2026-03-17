// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable VSTHRD103
#pragma warning disable VSTHRD105

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Synadia.Orbit.Testing.GoHarness;

/// <summary>
/// Manages a Go process compiled from inline source code, providing stdin/stdout
/// communication for cross-language testing scenarios.
/// </summary>
public class GoProcess : IAsyncDisposable, IDisposable
{
    private readonly Process _process;
    private readonly string _tempDir;
    private readonly Action<string> _logger;
    private bool _disposed;

    private GoProcess(Process process, string tempDir, Action<string> logger)
    {
        _process = process;
        _tempDir = tempDir;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether the Go process has exited.
    /// </summary>
    public bool HasExited => _process.HasExited;

    /// <summary>
    /// Gets the exit code of the Go process. Only valid after the process has exited.
    /// </summary>
    public int ExitCode => _process.ExitCode;

    /// <summary>
    /// Gets the process ID of the running Go process.
    /// </summary>
    public int Pid => _process.Id;

    /// <summary>
    /// Compiles and runs inline Go source code, returning a <see cref="GoProcess"/>
    /// that can communicate with the running program via stdin/stdout.
    /// </summary>
    /// <param name="goCode">The Go source code to compile and run.</param>
    /// <param name="logger">Optional logging callback.</param>
    /// <param name="goModules">Optional list of Go module dependencies (e.g. <c>"github.com/nats-io/nats.go@latest"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GoProcess"/> managing the running program.</returns>
    /// <exception cref="GoNotFoundException">Thrown when <c>go</c> is not found on PATH.</exception>
    /// <exception cref="GoCompilationException">Thrown when the Go code fails to compile.</exception>
    public static async Task<GoProcess> RunCodeAsync(
        string goCode,
        Action<string>? logger = null,
        string[]? goModules = null,
        CancellationToken cancellationToken = default)
    {
        GoToolchain.EnsureAvailable();

        var log = logger ?? (_ => { });
        var tempDir = Path.Combine(Path.GetTempPath(), "orbit-go-harness", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        log($"TempDir: {tempDir}");

        try
        {
            // Write Go source file
            var mainGoPath = Path.Combine(tempDir, "main.go");
            File.WriteAllText(mainGoPath, goCode);

            // Initialize Go module
            await RunGoCommandAsync("mod", "init testharness", tempDir, log, cancellationToken);

            // Add module dependencies if specified
            if (goModules != null)
            {
                foreach (var module in goModules)
                {
                    await RunGoCommandAsync("get", module, tempDir, log, cancellationToken);
                }
            }

            // Run go mod tidy
            await RunGoCommandAsync("mod", "tidy", tempDir, log, cancellationToken);

            // Build the binary
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var binaryName = isWindows ? "testharness.exe" : "testharness";
            var binaryPath = Path.Combine(tempDir, binaryName);

            await RunGoCommandAsync("build", $"-o \"{binaryPath}\" .", tempDir, log, cancellationToken);

            log($"Binary: {binaryPath}");

            // Start the compiled binary
            var psi = new ProcessStartInfo
            {
                FileName = binaryPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = tempDir,
            };

            var process = new Process { StartInfo = psi };
            process.Start();

            log($"Started Go process PID={process.Id}");

            return new GoProcess(process, tempDir, log);
        }
        catch
        {
            // Clean up temp dir on failure
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // best effort
            }

            throw;
        }
    }

    /// <summary>
    /// Writes a line to the Go process's standard input.
    /// </summary>
    /// <param name="line">The line to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger($"-> {line}");
        await _process.StandardInput.WriteLineAsync(line);
        await _process.StandardInput.FlushAsync();
    }

    /// <summary>
    /// Reads a line from the Go process's standard output.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The line read, or <c>null</c> if the stream has ended.</returns>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var line = await _process.StandardOutput.ReadLineAsync();
        _logger($"<- {line}");
        return line;
    }

    /// <summary>
    /// Reads all remaining standard error output from the Go process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stderr content.</returns>
    public async Task<string> ReadStdErrAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _process.StandardError.ReadToEndAsync();
    }

    /// <summary>
    /// Closes the standard input stream, signaling EOF to the Go process.
    /// </summary>
    public void CloseInput()
    {
        _process.StandardInput.Close();
    }

    /// <summary>
    /// Waits for the Go process to exit.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _process.WaitForExit();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_process.HasExited)
        {
            try
            {
                _process.Kill();
                _process.WaitForExit(5_000);
            }
            catch
            {
                // best effort
            }
        }

        _process.Dispose();

        for (var i = 0; i < 3; i++)
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
                break;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }
    }

    private static async Task RunGoCommandAsync(
        string command,
        string arguments,
        string workingDirectory,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var goExe = GoToolchain.FindGo()!;
        var psi = new ProcessStartInfo
        {
            FileName = goExe,
            Arguments = $"{command} {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };

        log($"Running: go {command} {arguments}");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start go process");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            log($"stdout: {stdout}");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            log($"stderr: {stderr}");
        }

        if (process.ExitCode != 0)
        {
            throw new GoCompilationException($"'go {command} {arguments}' failed with exit code {process.ExitCode}:\n{stderr}");
        }
    }
}
