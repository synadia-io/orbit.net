// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Synadia.Orbit.Testing.GoHarness.Test;

public class GoProcessTest
{
    [Fact]
    public async Task Echo_via_stdin_stdout()
    {
        // lang=go
        await using var go = await GoProcess.RunCodeAsync(
            """
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

        await go.WriteLineAsync("world");
        response = await go.ReadLineAsync();
        Assert.Equal("echo: world", response);
    }

    [Fact]
    public async Task Go_process_exits_on_stdin_close()
    {
        // lang=go
        await using var go = await GoProcess.RunCodeAsync(
            """
            package main

            import (
                "bufio"
                "fmt"
                "os"
            )

            func main() {
                scanner := bufio.NewScanner(os.Stdin)
                count := 0
                for scanner.Scan() {
                    count++
                }
                fmt.Printf("processed %d lines\n", count)
            }
            """);

        await go.WriteLineAsync("line1");
        await go.WriteLineAsync("line2");
        await go.WriteLineAsync("line3");
        go.CloseInput();

        var result = await go.ReadLineAsync();
        Assert.Equal("processed 3 lines", result);

        await go.WaitForExitAsync();
        Assert.Equal(0, go.ExitCode);
    }

    [Fact]
    public async Task Go_process_json_communication()
    {
        // lang=go
        await using var go = await GoProcess.RunCodeAsync(
            """
            package main

            import (
                "bufio"
                "encoding/json"
                "fmt"
                "os"
            )

            type Request struct {
                Name string `json:"name"`
                Value int    `json:"value"`
            }

            type Response struct {
                Greeting string `json:"greeting"`
                Doubled  int    `json:"doubled"`
            }

            func main() {
                scanner := bufio.NewScanner(os.Stdin)
                for scanner.Scan() {
                    var req Request
                    if err := json.Unmarshal([]byte(scanner.Text()), &req); err != nil {
                        fmt.Fprintf(os.Stderr, "error: %v\n", err)
                        continue
                    }
                    resp := Response{
                        Greeting: "Hello, " + req.Name + "!",
                        Doubled:  req.Value * 2,
                    }
                    out, _ := json.Marshal(resp)
                    fmt.Println(string(out))
                }
            }
            """);

        await go.WriteLineAsync("""{"name":"Alice","value":21}""");
        var response = await go.ReadLineAsync();
        Assert.Equal("""{"greeting":"Hello, Alice!","doubled":42}""", response);
    }

    [Fact]
    public async Task Go_compilation_error_throws()
    {
        var ex = await Assert.ThrowsAsync<GoCompilationException>(async () =>
        {
            // lang=go
            await using var go = await GoProcess.RunCodeAsync(
                """
                package main

                func main() {
                    this is not valid go code
                }
                """);
        });

        Assert.Contains("failed with exit code", ex.Message);
    }

    [Fact]
    public async Task Go_process_with_module_dependency()
    {
        // lang=go
        await using var go = await GoProcess.RunCodeAsync(
            """
            package main

            import (
                "fmt"
                "github.com/nats-io/nuid"
            )

            func main() {
                id := nuid.Next()
                fmt.Println(id)
            }
            """,
            goModules: ["github.com/nats-io/nuid@latest"]);

        var id = await go.ReadLineAsync();
        Assert.NotNull(id);
        Assert.True(id!.Length > 0, "nuid should generate a non-empty ID");
    }

    [Fact]
    public async Task Go_process_with_logging()
    {
        var logs = new List<string>();

        // lang=go
        await using var go = await GoProcess.RunCodeAsync(
            """
            package main

            import "fmt"

            func main() {
                fmt.Println("ready")
            }
            """,
            logger: msg => logs.Add(msg));

        var line = await go.ReadLineAsync();
        Assert.Equal("ready", line);
        Assert.True(logs.Count > 0, "Logger should have received messages");
    }
}
