# Synadia.Orbit.NatsContext

Connect to NATS using CLI context files (`~/.config/nats/context/*.json`). This package reads
NATS CLI context configurations and produces `NatsOpts` for creating connections with the NATS .NET client.

## Features

- Load NATS CLI context files by name, file path, or selected context
- Maps context settings to `NatsOpts` (auth, TLS, inbox prefix)
- Supports user/password, credentials file, NKey, token, and JWT authentication
- TLS certificate configuration (client cert, CA, TLS-first)
- Environment variable expansion for credentials paths
- Home directory (`~`) expansion for file paths
- AOT/trimming compatible

## Installation

```bash
dotnet add package Synadia.Orbit.NatsContext
```

## Quick Start

```csharp
using Synadia.Orbit.NatsContext;

// Connect using the selected context (from context.txt)
NatsContextConnection result = await NatsContext.ConnectAsync();
await using var connection = result.Connection;

// Connect using a named context
var result = await NatsContext.ConnectAsync("my-context");

// Connect using an absolute file path
var result = await NatsContext.ConnectAsync("/path/to/context.json");

// Load settings without connecting
NatsContextResult loaded = NatsContext.Load("my-context");

// Customize options before connecting
var result = await NatsContext.ConnectAsync("my-context", opts => opts with
{
    Name = "my-app",
});

// Deconstruct results for convenience
var (connection, settings) = await NatsContext.ConnectAsync();
var (opts, settings) = NatsContext.Load();
```

## Documentation

For more information, see the [orbit.net documentation](https://github.com/synadia-io/orbit.net).
