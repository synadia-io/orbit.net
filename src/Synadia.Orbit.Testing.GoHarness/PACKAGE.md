# Synadia.Orbit.Testing.GoHarness

A test harness for running inline Go code from .NET tests with stdin/stdout communication.

Useful for cross-language testing scenarios where you need to verify .NET code behavior against Go implementations.

## Requirements

- Go toolchain installed and available on PATH (`go` executable)

## Usage

```csharp
await using var go = await GoProcess.RunCodeAsync("""
    package main

    import (
        "bufio"
        "fmt"
        "os"
    )

    func main() {
        scanner := bufio.NewScanner(os.Stdin)
        for scanner.Scan() {
            fmt.Println("echo: " + scanner.Text())
        }
    }
    """);

await go.WriteLineAsync("hello");
var response = await go.ReadLineAsync();
Assert.Equal("echo: hello", response);
```
