# Synadia.Orbit.NatsCli.Plugin

**Note:** NATS CLI plugins are experimental and may change in future releases.

A library for building NATS CLI plugins as AOT-compiled native binaries.
Handles the fisk introspection protocol (JSON handshake) and provides NATS CLI context file reading.

## Features

### Plugin Entry Point

Define commands with System.CommandLine and call `NatsCliPlugin.RunAsync()`:

```csharp
var root = new RootCommand("My plugin");
root.Add(listCommand);

return await NatsCliPlugin.RunAsync(args, root, new NatsCliPluginOptions
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
4. Use `NatsCliPlugin.RunAsync()` in your `Main` method to handle the plugin execution.
5. Publish your application
6. Register your plugin with NATS CLI using the `nats plugins register` command.

### Example Plugin `peek`

An example NATS CLI plugin is available in the Synadia Orbit .NET repository under `tools/peek`.

```
> dotnet publish -o ~\.natscli-plugins
> nats plugins register peek ~\.natscli-plugins\peek.exe
> nats peek info
```

## Documentation

For more information, see the [orbit.net documentation](https://github.com/synadia-io/orbit.net).
