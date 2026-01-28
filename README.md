[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Test](https://github.com/synadia-io/orbit.net/actions/workflows/test.yml/badge.svg)](https://github.com/synadia-io/orbit.net/actions/workflows/test.yml)

<p align="center">
  <img src="orbit-small.png">
</p>

Orbit .NET is a set of independent utilities around NATS ecosystem that aims to
boost productivity and provide higher abstraction layer for NATS .NET
clients. Note that these libraries will evolve rapidly and API guarantees are
not made until the specific project has a v1.0.0 version.

# Packages

| Package | NuGet | Description | Docs |
|---------|-------|-------------|------|
| Synadia.Orbit.Core.Extensions | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.Core.Extensions.svg)](https://www.nuget.org/packages/Synadia.Orbit.Core.Extensions) | Request-many with custom sentinel support | [README](src/Synadia.Orbit.Core.Extensions/PACKAGE.md) |
| Synadia.Orbit.JetStream.Extensions | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.JetStream.Extensions.svg)](https://www.nuget.org/packages/Synadia.Orbit.JetStream.Extensions) | Direct batch retrieval and scheduled messages | [README](src/Synadia.Orbit.JetStream.Extensions/PACKAGE.md) |
| Synadia.Orbit.JetStream.Publisher | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.JetStream.Publisher.svg)](https://www.nuget.org/packages/Synadia.Orbit.JetStream.Publisher) | High-performance JetStream publishing | [README](src/Synadia.Orbit.JetStream.Publisher/PACKAGE.md) |
| Synadia.Orbit.KeyValueStore.Extensions | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.KeyValueStore.Extensions.svg)](https://www.nuget.org/packages/Synadia.Orbit.KeyValueStore.Extensions) | Key encoding codecs for KV stores | [README](src/Synadia.Orbit.KeyValueStore.Extensions/PACKAGE.md) |
| Synadia.Orbit.PCGroups | [![NuGet](https://img.shields.io/nuget/v/Synadia.Orbit.PCGroups.svg)](https://www.nuget.org/packages/Synadia.Orbit.PCGroups) | Partitioned Consumer Groups for horizontal scaling | [README](src/Synadia.Orbit.PCGroups/PACKAGE.md) |
