[![Test](https://github.com/synadia-io/orbit.net/actions/workflows/test.yml/badge.svg)](https://github.com/synadia-io/orbit.net/actions/workflows/test.yml)

<p align="center">
  <img src="orbit-small.png">
</p>

Orbit .NET is a set of independent utilities around NATS ecosystem that aims to
boost productivity and provide higher abstraction layer for NATS .NET
clients. Note that these libraries will evolve rapidly and API guarantees are
not made until the specific project has a v1.0.0 version.

# Packages

| NuGet | Description | Docs |
|-------|-------------|------|
| Synadia.Orbit.JetStream.Extensions | Direct batch retrieval and scheduled messages | [README](src/Synadia.Orbit.JetStream.Extensions/PACKAGE.md) |
| Synadia.Orbit.JetStream.Publisher | High-performance JetStream publishing | [README](src/Synadia.Orbit.JetStream.Publisher/PACKAGE.md) |
| Synadia.Orbit.KeyValueStore.Extensions | Key encoding codecs for KV stores | [README](src/Synadia.Orbit.KeyValueStore.Extensions/PACKAGE.md) |
| Synadia.Orbit.PCGroups | Partitioned Consumer Groups for horizontal scaling | [README](src/Synadia.Orbit.PCGroups/PACKAGE.md) |
