[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Test](https://github.com/synadia-io/orbit.net/actions/workflows/test.yml/badge.svg)](https://github.com/synadia-io/orbit.net/actions/workflows/test.yml)

<p align="center">
  <img src="orbit-small.png">
</p>

Orbit .NET is a set of independent NuGet packages that build on [NATS .NET](https://github.com/nats-io/nats.net).
Each one solves a problem the core client doesn't cover:

- **Scaling consumers across instances?** -> Partitioned consumer groups with auto-balancing and self-healing
- **Publishing at high throughput?** -> Pipelined ack publisher + atomic batch commits
- **Need delayed or scheduled delivery?** -> Schedule messages for future publish on JetStream
- **Distributed counters without a separate store?** -> Atomic inc/dec backed by JetStream streams
- **KV keys with special characters?** -> Base64 and path-style key codecs
- **Sharing NATS CLI context with your app?** -> Read `~/.config/nats/context/` files into `NatsOpts`
- **Integration testing?** -> Spin up `nats-server` with auto port assignment and JetStream

Use what you need. APIs may change until a package reaches v1.0.0.

# Packages

| Package | Description | Docs | NuGet |
|---------|-------------|------|-------|
| Synadia.Orbit.Core.Extensions | Request-many with custom sentinel support | [README](src/Synadia.Orbit.Core.Extensions/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.Core.Extensions.svg)](https://www.nuget.org/packages/Synadia.Orbit.Core.Extensions) |
| Synadia.Orbit.Counters | Distributed counters on JetStream streams | [README](src/Synadia.Orbit.Counters/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.Counters.svg)](https://www.nuget.org/packages/Synadia.Orbit.Counters) |
| Synadia.Orbit.JetStream.Extensions | Direct batch retrieval and scheduled messages | [README](src/Synadia.Orbit.JetStream.Extensions/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.JetStream.Extensions.svg)](https://www.nuget.org/packages/Synadia.Orbit.JetStream.Extensions) |
| Synadia.Orbit.JetStream.Publisher | Backpressure publisher and atomic batch publishing for JetStream | [README](src/Synadia.Orbit.JetStream.Publisher/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.JetStream.Publisher.svg)](https://www.nuget.org/packages/Synadia.Orbit.JetStream.Publisher) |
| Synadia.Orbit.KeyValueStore.Extensions | Key encoding codecs for KV stores | [README](src/Synadia.Orbit.KeyValueStore.Extensions/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.KeyValueStore.Extensions.svg)](https://www.nuget.org/packages/Synadia.Orbit.KeyValueStore.Extensions) |
| Synadia.Orbit.NatsContext | Connect to NATS using CLI context files | [README](src/Synadia.Orbit.NatsContext/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.NatsContext.svg)](https://www.nuget.org/packages/Synadia.Orbit.NatsContext) |
| Synadia.Orbit.PCGroups | Partitioned Consumer Groups for horizontal scaling | [README](src/Synadia.Orbit.PCGroups/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.PCGroups.svg)](https://www.nuget.org/packages/Synadia.Orbit.PCGroups) |
| Synadia.Orbit.Testing.NatsServerProcessManager | NATS server process management for testing | [README](src/Synadia.Orbit.Testing.NatsServerProcessManager/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.Testing.NatsServerProcessManager.svg)](https://www.nuget.org/packages/Synadia.Orbit.Testing.NatsServerProcessManager) |

# Experimental Packages

The following packages are **not part of the official NATS ecosystem**. They are published exclusively
in Orbit .NET to gauge interest and address NATS .NET community requests. Their APIs may change or
the packages may be removed in future releases. If you're interested in any of these packages or have feedback, please [open an issue](https://github.com/synadia-io/orbit.net/issues) and we'll consider promoting it to a fully supported package.

| Package | Description | Docs | NuGet |
|---------|-------------|------|-------|
| Synadia.Orbit.NatsCli.Plugin | NATS CLI plugin framework for AOT-compiled binaries | [README](src/Synadia.Orbit.NatsCli.Plugin/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.NatsCli.Plugin.svg)](https://www.nuget.org/packages/Synadia.Orbit.NatsCli.Plugin) |
| Synadia.Orbit.ParameterizedSubject | Safe, parameterized NATS subject building with percent-encoding | [README](src/Synadia.Orbit.ParameterizedSubject/PACKAGE.md) | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.ParameterizedSubject.svg)](https://www.nuget.org/packages/Synadia.Orbit.ParameterizedSubject) |
