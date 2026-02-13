// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable VSTHRD103
#pragma warning disable VSTHRD105

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Synadia.Orbit.Testing.NatsServerProcessManager;

/// <summary>
/// Manages the lifecycle of a NATS server process for testing and development.
/// Handles starting, monitoring, and stopping <c>nats-server</c> processes with
/// automatic port discovery and health validation.
/// </summary>
public class NatsServerProcess : IAsyncDisposable, IDisposable
{
    private readonly Action<string> _logger;
    private readonly Process _process;
    private readonly string _scratch;
    private readonly bool _withJs;
    private readonly string? _config;
    private readonly int _port;
    private bool _stopped;

    private NatsServerProcess(Action<string> logger, Process process, string url, string scratch, bool withJs, string? config, int port)
    {
        Url = url;
        _logger = logger;
        _process = process;
        _scratch = scratch;
        _withJs = withJs;
        _config = config;
        _port = port;
    }

    /// <summary>
    /// Gets the URL of the running NATS server (e.g. <c>nats://127.0.0.1:4222</c>).
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the process ID of the running NATS server.
    /// </summary>
    public int Pid => _process.Id;

    /// <summary>
    /// Gets the configuration file path, if one was provided at startup.
    /// </summary>
    public string? Config => _config;

    /// <summary>
    /// Gets the port the NATS server is listening on.
    /// </summary>
    public int Port => new Uri(Url).Port;

    /// <summary>
    /// Starts a new NATS server process asynchronously.
    /// </summary>
    /// <param name="logger">Optional logging callback for server output.</param>
    /// <param name="config">Optional path to a NATS server configuration file.</param>
    /// <param name="withJs">Whether to enable JetStream. Defaults to <c>true</c>.</param>
    /// <param name="port">Port for the server to listen on. Use <c>-1</c> (default) for automatic port assignment.</param>
    /// <param name="scratch">Optional scratch directory for server data. If not specified, a temporary directory is created.</param>
    /// <returns>A <see cref="NatsServerProcess"/> managing the started server.</returns>
    public static ValueTask<NatsServerProcess> StartAsync(Action<string>? logger = null, string? config = null, bool withJs = true, int port = -1, string? scratch = null)
        => new(Start(logger, config, withJs, port, scratch));

    /// <summary>
    /// Starts a new NATS server process.
    /// </summary>
    /// <param name="logger">Optional logging callback for server output.</param>
    /// <param name="config">Optional path to a NATS server configuration file.</param>
    /// <param name="withJs">Whether to enable JetStream. Defaults to <c>true</c>.</param>
    /// <param name="port">Port for the server to listen on. Use <c>-1</c> (default) for automatic port assignment.</param>
    /// <param name="scratch">Optional scratch directory for server data. If not specified, a temporary directory is created.</param>
    /// <returns>A <see cref="NatsServerProcess"/> managing the started server.</returns>
    /// <exception cref="Exception">Thrown when the server fails to start or respond to health checks.</exception>
    public static NatsServerProcess Start(Action<string>? logger = null, string? config = null, bool withJs = true, int port = -1, string? scratch = null)
    {
        var isLoggingEnabled = logger != null;
        var log = logger ?? (_ => { });

        scratch ??= Path.Combine(Path.GetTempPath(), "nats.net.tests", Guid.NewGuid().ToString());

        var portsFileDir = Path.Combine(scratch, "port");
        Directory.CreateDirectory(portsFileDir);

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var natsServerExe = isWindows ? "nats-server.exe" : "nats-server";
        var configFlag = config == null ? string.Empty : $"-c \"{config}\"";
        var portsFileDirEsc = portsFileDir.Replace(@"\", @"\\");

        string? sdEsc = null;
        if (withJs)
        {
            var sd = Path.Combine(scratch, "data");
            Directory.CreateDirectory(sd);
            sdEsc = sd.Replace(@"\", @"\\");
        }

        var info = new ProcessStartInfo
        {
            FileName = natsServerExe,
            Arguments = withJs
                ? $"{configFlag} -a 127.0.0.1 -p {port} -m -1 -js -sd \"{sdEsc}\" --ports_file_dir \"{portsFileDirEsc}\""
                : $"{configFlag} -a 127.0.0.1 -p {port} -m -1 --ports_file_dir \"{portsFileDirEsc}\"",
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var process = new Process { StartInfo = info, };

        if (isLoggingEnabled)
        {
#pragma warning disable CS8604 // Possible null reference argument.
            DataReceivedEventHandler outputHandler = (_, e) => log(e.Data);
#pragma warning restore CS8604 // Possible null reference argument.
            process.OutputDataReceived += outputHandler;
            process.ErrorDataReceived += outputHandler;
        }
        else
        {
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
        }

        process.Start();

        if (isWindows)
        {
            ChildProcessTracker.AddProcess(process);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var portsFile = Path.Combine(portsFileDir, $"{natsServerExe}_{process.Id}.ports");
        log($"portsFile={portsFile}");

        string? ports = null;
        Exception? exception = null;
        for (var i = 0; i < 10; i++)
        {
            try
            {
                ports = File.ReadAllText(portsFile);
                break;
            }
            catch (Exception e)
            {
                exception = e;
                Thread.Sleep(100 + (500 * i));
            }
        }

        if (ports == null)
        {
            throw new Exception("Failed to read ports file", exception);
        }

        using var portsJson = JsonDocument.Parse(ports);
        var url = new Uri(portsJson.RootElement.GetProperty("nats")[0].GetString()!);
        var monitorUrl = new Uri(portsJson.RootElement.GetProperty("monitoring")[0].GetString()!);
        log($"ports={ports}");
        log($"url={url}");
        log($"monitorUrl={monitorUrl}");

        using var httpClient = new HttpClient();
        for (var i = 0; i < 10; i++)
        {
            try
            {
                using var response = httpClient.GetAsync(new Uri(monitorUrl, "/healthz")).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    return new NatsServerProcess(log, process, url.ToString(), scratch, withJs, config, url.Port);
                }
            }
            catch (Exception e)
            {
                exception = e;
                Thread.Sleep(1_000 + (i * 500));
            }
        }

        throw new Exception("Failed to setup the server", exception);
    }

    /// <summary>
    /// Stops the NATS server process and restarts it with the same configuration.
    /// </summary>
    /// <returns>A new <see cref="NatsServerProcess"/> managing the restarted server.</returns>
    public ValueTask<NatsServerProcess> RestartAsync()
    {
        Stop();
        return StartAsync(_logger, _config, _withJs, _port, _scratch);
    }

    /// <summary>
    /// Stops the NATS server process asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public ValueTask StopAsync()
    {
        Stop();
        return default;
    }

    /// <summary>
    /// Stops the NATS server process.
    /// </summary>
    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        for (var i = 0; i < 10; i++)
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // best effort
            }

            if (_process.WaitForExit(1_000))
            {
                break;
            }
        }

        _stopped = true;

        // Give OS some time to clean up
        Thread.Sleep(500);
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
        Stop();

        for (var i = 0; i < 3; i++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
                break;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }

        _process.Dispose();
    }
}
