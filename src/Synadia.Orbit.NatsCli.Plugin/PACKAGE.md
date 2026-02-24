# Synadia.Orbit.NatsCli.Plugin [EXPERIMENTAL]

> **This package is experimental.** It is not part of the official NATS ecosystem and is published
> exclusively in Orbit .NET to gauge interest and address community requests. The API may change
> or the package may be removed in a future release.

A library for building NATS CLI plugins as AOT-compiled native binaries.
Extends System.CommandLine with an entry point that handles the fisk introspection protocol (JSON handshake)
and provides NATS CLI context file reading.

## Features

### Plugin Entry Point

Define commands with System.CommandLine and call `RunNatsCliPluginAsync()`:

```csharp
var root = new RootCommand("My plugin");
root.Add(listCommand);

return await root.RunNatsCliPluginAsync(args, new NatsCliPluginOptions
{
    Name = "myplugin",
    Version = "1.0.0",
});
```

### NATS CLI Context

Load NATS CLI context files for connection settings via the `Synadia.Orbit.NatsContext` dependency:

```csharp
var ctx = NatsContext.Load();
await using var nats = await ctx.ConnectAsync();
```

### Creating Plugins Installation

To create a NATS CLI plugin, follow these steps:
1. Create a new .NET console application.
2. Add a reference to the `Synadia.Orbit.NatsCli.Plugin` NuGet package.
3. Define your commands using System.CommandLine.
4. Use `RunNatsCliPluginAsync()` extension method on your `RootCommand` to handle the plugin execution.
5. Publish your application.
6. Register your plugin with NATS CLI using the `nats plugins register` command.

### Example Plugin `peek`

An example NATS CLI plugin is available in the Synadia Orbit .NET repository under `tools/peek`.

```
> dotnet publish -o /path/to/.natscli-plugins
> nats plugins register peek /path/to/.natscli-plugins/peek.exe
> nats peek info
```

## Documentation

For more information, see the [orbit.net documentation](https://github.com/synadia-io/orbit.net).
