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

// Load the selected context (from context.txt)
var ctx = NatsContext.Load();
Console.WriteLine($"Context: {ctx.Settings.Name}, URL: {ctx.Settings.Url}");

// Connect using the loaded context
await using var connection = await ctx.ConnectAsync();
await connection.PublishAsync("greet", "hello");

// Load a named context
var ctx = NatsContext.Load("my-context");

// Load from an absolute file path
var ctx = NatsContext.Load("/path/to/context.json");

// Customize options before connecting
await using var connection = await ctx.ConnectAsync(opts => opts with
{
    Name = "my-app",
});

// Access settings from the context
Console.WriteLine($"Connected to {ctx.Settings.Url}");
```

## Documentation

For more information, see the [orbit.net documentation](https://github.com/synadia-io/orbit.net).
