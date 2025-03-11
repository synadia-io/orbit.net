// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using System.Reflection;

if (args.Length != 1)
{
    string commands = string.Join("|",
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.Name.StartsWith("Cmd"))
            .Select(t => t.Name.ToLowerInvariant())
            .ToArray());
    Console.Error.WriteLine($"Usage: OrbitPublish [{commands}]");
    return 2;
}

string cmd = args[0];

foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
{
    if (string.Equals(type.Name, $"Cmd{cmd}", StringComparison.OrdinalIgnoreCase))
    {
        if (Activator.CreateInstance(type) is { } runner)
        {
            MethodInfo? methodInfo = type.GetMethod("Run");
            if (methodInfo is { } runMethod)
            {
                if (runMethod.Invoke(runner, null) is { } invoke)
                {
                    return await (Task<int>)invoke;
                }

                Console.Error.WriteLine($"Cannot invoke {type.Name}");
                return 1;
            }

            Console.Error.WriteLine($"Cannot run {type.Name}");
            return 1;
        }

        Console.Error.WriteLine($"Cannot activate {type.Name}");
        return 1;
    }
}

Console.Error.WriteLine("Can't find command");

return 1;
