// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable

using System.Reflection;

if (args.Length != 1)
{
    string commands = string.Join("|",
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.Name.StartsWith("Example"))
            .Select(t => t.Name.Replace("Example", string.Empty).ToLowerInvariant())
            .ToArray());
    Console.Error.WriteLine($"Usage: DocsExamples [{commands}]");
    return 2;
}

string cmd = args[0];

foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
{
    if (string.Equals(type.Name, $"Example{cmd}", StringComparison.OrdinalIgnoreCase))
    {
        if (Activator.CreateInstance(type) is { } runner)
        {
            MethodInfo? methodInfo = type.GetMethod("Run");
            if (methodInfo is { } runMethod)
            {
                if (runMethod.Invoke(runner, null) is { } invoke)
                {
                    await (Task)invoke;
                    return 0;
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
