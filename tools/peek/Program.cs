// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.CommandLine;
using System.Text;
using NATS.Client.Core;
using Spectre.Console;
using Synadia.Orbit.NatsCli.Plugin;
using Synadia.Orbit.NatsContext;

var serverOption = new Option<string?>("--server", "-s")
{
    Description = "NATS server URL",
};

var contextOption = new Option<string?>("--context")
{
    Description = "NATS CLI context name",
};

var root = new RootCommand("NATS server peek tool")
{
    serverOption,
    contextOption,
};

// INFO
var infoCmd = new Command("info", "Display NATS server information");
root.Add(infoCmd);

infoCmd.SetAction(async (parseResult, ct) =>
{
    await using var nats = await ConnectAsync(
        parseResult.GetValue(serverOption),
        parseResult.GetValue(contextOption));

    var info = nats.ServerInfo;
    if (info is null)
    {
        AnsiConsole.MarkupLine("[red]Could not retrieve server info[/]");
        return;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .Title("[bold blue]NATS Server Info[/]")
        .AddColumn(new TableColumn("[bold]Property[/]").NoWrap())
        .AddColumn("[bold]Value[/]");

    table.AddRow("Name", E(info.Name));
    table.AddRow("Version", $"[green]{E(info.Version)}[/]");
    table.AddRow("Host", $"{E(info.Host)}:{info.Port}");
    table.AddRow("Server ID", E(info.Id));
    table.AddRow("Git Commit", E(info.GitCommit));
    table.AddRow("Go", E(info.GoVersion));
    table.AddRow("Max Payload", $"{info.MaxPayload:N0} bytes");
    table.AddRow("Proto", info.ProtocolVersion.ToString());
    table.AddRow("JetStream", info.JetStreamAvailable ? "[green]enabled[/]" : "[grey]disabled[/]");
    table.AddRow("Headers", info.HeadersSupported ? "[green]yes[/]" : "[grey]no[/]");
    table.AddRow("TLS Required", info.TlsRequired ? "[yellow]yes[/]" : "no");
    table.AddRow("TLS Available", info.TlsAvailable ? "[green]yes[/]" : "no");
    table.AddRow("Auth Required", info.AuthRequired ? "[yellow]yes[/]" : "no");
    table.AddRow("Client ID", info.ClientId.ToString());
    table.AddRow("Client IP", E(info.ClientIp));

    if (info.Cluster is not null)
    {
        table.AddRow("Cluster", Markup.Escape(info.Cluster));
    }

    if (info.LameDuckMode)
    {
        table.AddRow("Lame Duck", "[red]yes[/]");
    }

    AnsiConsole.Write(table);
});

// SUB
var subSubjectArg = new Argument<string>("subject") { Description = "Subject to subscribe to" };
var subCmd = new Command("sub", "Subscribe and display messages") { subSubjectArg };
root.Add(subCmd);

subCmd.SetAction(async (parseResult, ct) =>
{
    var subject = parseResult.GetValue(subSubjectArg)!;
    await using var nats = await ConnectAsync(
        parseResult.GetValue(serverOption),
        parseResult.GetValue(contextOption));

    AnsiConsole.Write(
        new Rule($"[blue]Subscribing to[/] [bold yellow]{Markup.Escape(subject)}[/]")
            .LeftJustified());
    AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop[/]\n");

    var count = 0;
    var table = new Table()
        .Border(TableBorder.Simple)
        .AddColumn(new TableColumn("[bold]#[/]").Width(6))
        .AddColumn(new TableColumn("[bold]Time[/]").Width(14))
        .AddColumn("[bold]Subject[/]")
        .AddColumn(new TableColumn("[bold]Size[/]").Width(8))
        .AddColumn("[bold]Data[/]");

    try
    {
        await AnsiConsole.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async display =>
            {
                await foreach (var msg in nats.SubscribeAsync<string>(subject, cancellationToken: ct))
                {
                    count++;
                    var data = msg.Data ?? string.Empty;
                    var preview = data.Length > 60 ? data[..60] + "..." : data;

                    table.AddRow(
                        count.ToString(),
                        $"[grey]{DateTime.Now:HH:mm:ss.fff}[/]",
                        $"[cyan]{Markup.Escape(msg.Subject)}[/]",
                        $"[grey]{Encoding.UTF8.GetByteCount(data)}B[/]",
                        Markup.Escape(preview));
                    display.Refresh();
                }
            });
    }
    catch (OperationCanceledException)
    {
    }

    AnsiConsole.MarkupLine($"\n[grey]Received {count} messages[/]");
});

// PUB
var pubSubjectArg = new Argument<string>("subject") { Description = "Subject to publish to" };
var pubDataArg = new Argument<string>("data") { Description = "Message payload" };
var pubCmd = new Command("pub", "Publish a message") { pubSubjectArg, pubDataArg };
root.Add(pubCmd);

pubCmd.SetAction(async (parseResult, ct) =>
{
    var subject = parseResult.GetValue(pubSubjectArg)!;
    var data = parseResult.GetValue(pubDataArg)!;
    await using var nats = await ConnectAsync(
        parseResult.GetValue(serverOption),
        parseResult.GetValue(contextOption));

    await nats.PublishAsync(subject, data, cancellationToken: ct);

    AnsiConsole.MarkupLine(
        $"[green]Published[/] [bold]{Encoding.UTF8.GetByteCount(data)}[/] bytes to [cyan]{Markup.Escape(subject)}[/]");
});

// REQ
var reqSubjectArg = new Argument<string>("subject") { Description = "Subject to send request to" };
var reqDataArg = new Argument<string>("data") { Description = "Request payload" };
var reqCmd = new Command("req", "Send a request and display the reply") { reqSubjectArg, reqDataArg };
root.Add(reqCmd);

reqCmd.SetAction(async (parseResult, ct) =>
{
    var subject = parseResult.GetValue(reqSubjectArg)!;
    var data = parseResult.GetValue(reqDataArg)!;
    await using var nats = await ConnectAsync(
        parseResult.GetValue(serverOption),
        parseResult.GetValue(contextOption));

    var reply = await nats.RequestAsync<string, string>(subject, data, cancellationToken: ct);

    var panel = new Panel(Markup.Escape(reply.Data ?? "(empty)"))
        .Header($"[bold green]Reply[/] [grey]from {Markup.Escape(reply.Subject)}[/]")
        .Border(BoxBorder.Rounded);

    AnsiConsole.Write(panel);
});

return await NatsCliPlugin.RunAsync(args, root, new NatsCliPluginOptions
{
    Name = "peek",
    Version = "0.0.1",
    Author = "Synadia Orbit .NET Examples",
});

string E(string? value) => Markup.Escape(value ?? "-");

async Task<NatsConnection> ConnectAsync(string? server, string? contextName)
{
    if (server != null)
    {
        var nats = new NatsConnection(new NatsOpts { Url = server });
        await nats.ConnectAsync();
        return nats;
    }

    try
    {
        var ctx = NatsContext.Load(contextName);
        return await ctx.ConnectAsync();
    }
    catch (NatsContextException)
    {
        var nats = new NatsConnection();
        await nats.ConnectAsync();
        return nats;
    }
}
