// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using NATS.Client.Core;

namespace Synadia.Orbit.NatsContext;

/// <summary>
/// Represents the result of loading a NATS CLI context, containing the configured options and settings.
/// </summary>
/// <param name="Opts">The <see cref="NatsOpts"/> configured from the context.</param>
/// <param name="Settings">The parsed <see cref="NatsContextSettings"/>.</param>
public sealed record NatsContextResult(NatsOpts Opts, NatsContextSettings Settings);
