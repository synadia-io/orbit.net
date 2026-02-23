# Synadia.Orbit.Testing.NatsServerProcessManager

A library for managing NATS server processes in testing and development scenarios.

## Features

- **NatsServerProcess**: Start, monitor, and stop `nats-server` processes programmatically
  - Automatic dynamic port assignment and discovery
  - JetStream support with configurable data directories
  - Health check validation before returning
  - Automatic cleanup of temporary files on disposal
- **ChildProcessTracker** (Windows): Ensures child processes are terminated when the parent process exits

## Usage

```csharp
// Start a NATS server with JetStream enabled
using var server = NatsServerProcess.Start(logger: Console.WriteLine);

Console.WriteLine($"NATS server running at: {server.Url}");

// Or start without JetStream
using var serverNoJs = NatsServerProcess.Start(withJs: false);

// Async factory method also available
await using var serverAsync = await NatsServerProcess.StartAsync();
```

## Requirements

- `nats-server` binary must be available on the system `PATH`

## Documentation

For more information, see the [orbit.net documentation](https://github.com/synadia-io/orbit.net).
