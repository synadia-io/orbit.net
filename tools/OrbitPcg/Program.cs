// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable SuggestVarOrType_BuiltInTypes
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Synadia.Orbit.PCGroups;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.PCGroups.Static;

var serverOption = new Option<string>(
    aliases: ["--server", "-s"],
    getDefaultValue: () => "nats://localhost:4222",
    description: "NATS server URL");

var rootCommand = new RootCommand("OrbitPcg - NATS Partitioned Consumer Groups CLI")
{
    serverOption,
};

// ============================================================================
// STATIC COMMANDS
// ============================================================================
var staticCommand = new Command("static", "Manage static consumer groups");
rootCommand.AddCommand(staticCommand);

// static list
var staticListCommand = new Command("list", "List all static consumer groups on a stream");
staticListCommand.AddAlias("ls");
var staticListStreamArg = new Argument<string>("stream", "Stream name");
staticListCommand.AddArgument(staticListStreamArg);
staticListCommand.SetHandler(async (server, stream) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    Console.WriteLine($"Static consumer groups on stream '{stream}':");
    var count = 0;
    await foreach (var group in js.ListPcgStaticAsync(stream))
    {
        Console.WriteLine($"  - {group}");
        count++;
    }

    if (count == 0)
    {
        Console.WriteLine("  (none)");
    }
}, serverOption, staticListStreamArg);
staticCommand.AddCommand(staticListCommand);

// static info
var staticInfoCommand = new Command("info", "Get static consumer group configuration and active members");
var staticInfoStreamArg = new Argument<string>("stream", "Stream name");
var staticInfoNameArg = new Argument<string>("name", "Consumer group name");
staticInfoCommand.AddArgument(staticInfoStreamArg);
staticInfoCommand.AddArgument(staticInfoNameArg);
staticInfoCommand.SetHandler(async (server, stream, name) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var config = await js.GetPcgStaticConfigAsync(stream, name);
    Console.WriteLine($"Static Consumer Group: {name}");
    Console.WriteLine($"  Stream:      {stream}");
    Console.WriteLine($"  MaxMembers:  {config.MaxMembers}");
    Console.WriteLine($"  Filter:      {config.Filter ?? "(none)"}");

    if (config.Members != null)
    {
        Console.WriteLine($"  Members:     {string.Join(", ", config.Members)}");
    }

    if (config.MemberMappings != null)
    {
        Console.WriteLine("  Mappings:");
        foreach (var mapping in config.MemberMappings)
        {
            Console.WriteLine($"    {mapping.Member}: [{string.Join(", ", mapping.Partitions)}]");
        }
    }

    Console.WriteLine("\nActive Members:");
    var activeCount = 0;
    await foreach (var member in js.ListPcgStaticActiveMembersAsync(stream, name))
    {
        Console.WriteLine($"  - {member}");
        activeCount++;
    }

    if (activeCount == 0)
    {
        Console.WriteLine("  (none)");
    }
}, serverOption, staticInfoStreamArg, staticInfoNameArg);
staticCommand.AddCommand(staticInfoCommand);

// static create
var staticCreateCommand = new Command("create", "Create a static consumer group");
var staticCreateStreamArg = new Argument<string>("stream", "Stream name");
var staticCreateNameArg = new Argument<string>("name", "Consumer group name");
var staticCreateMaxMembersArg = new Argument<uint>("max-members", "Maximum number of members (partitions)");
var staticCreateFilterOption = new Option<string?>("--filter", "Filter subject pattern");
var staticCreateMembersOption = new Option<string[]?>("--members", "Member names (space-separated)") { AllowMultipleArgumentsPerToken = true };
var staticCreateMappingsOption = new Option<string[]?>("--mappings", "Member mappings in format member:p1,p2,p3") { AllowMultipleArgumentsPerToken = true };
staticCreateCommand.AddArgument(staticCreateStreamArg);
staticCreateCommand.AddArgument(staticCreateNameArg);
staticCreateCommand.AddArgument(staticCreateMaxMembersArg);
staticCreateCommand.AddOption(staticCreateFilterOption);
staticCreateCommand.AddOption(staticCreateMembersOption);
staticCreateCommand.AddOption(staticCreateMappingsOption);
staticCreateCommand.SetHandler(async (server, stream, name, maxMembers, filter, members, mappingsStr) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    NatsPcgMemberMapping[]? mappings = null;
    if (mappingsStr is { Length: > 0 })
    {
        mappings = ParseMappings(mappingsStr);
    }

    var config = await js.CreatePcgStaticAsync(stream, name, maxMembers,
        filter: filter,
        members: members,
        memberMappings: mappings);

    Console.WriteLine($"Created static consumer group '{name}' on stream '{stream}'");
    Console.WriteLine($"  MaxMembers: {config.MaxMembers}");
    if (config.Filter != null)
        Console.WriteLine($"  Filter: {config.Filter}");
}, serverOption, staticCreateStreamArg, staticCreateNameArg, staticCreateMaxMembersArg,
   staticCreateFilterOption, staticCreateMembersOption, staticCreateMappingsOption);
staticCommand.AddCommand(staticCreateCommand);

// static delete
var staticDeleteCommand = new Command("delete", "Delete a static consumer group");
staticDeleteCommand.AddAlias("rm");
var staticDeleteStreamArg = new Argument<string>("stream", "Stream name");
var staticDeleteNameArg = new Argument<string>("name", "Consumer group name");
var staticDeleteForceOption = new Option<bool>(["--force", "-f"], "Skip confirmation");
staticDeleteCommand.AddArgument(staticDeleteStreamArg);
staticDeleteCommand.AddArgument(staticDeleteNameArg);
staticDeleteCommand.AddOption(staticDeleteForceOption);
staticDeleteCommand.SetHandler(async (server, stream, name, force) =>
{
    if (!force)
    {
        Console.Write($"Delete static consumer group '{name}' on stream '{stream}'? [y/N] ");
        var response = Console.ReadLine();
        if (response?.ToLowerInvariant() != "y")
        {
            Console.WriteLine("Cancelled.");
            return;
        }
    }

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    await js.DeletePcgStaticAsync(stream, name);
    Console.WriteLine($"Deleted static consumer group '{name}'");
}, serverOption, staticDeleteStreamArg, staticDeleteNameArg, staticDeleteForceOption);
staticCommand.AddCommand(staticDeleteCommand);

// static step-down
var staticStepDownCommand = new Command("step-down", "Force a member to step down");
staticStepDownCommand.AddAlias("sd");
var staticStepDownStreamArg = new Argument<string>("stream", "Stream name");
var staticStepDownNameArg = new Argument<string>("name", "Consumer group name");
var staticStepDownMemberArg = new Argument<string>("member", "Member name");
staticStepDownCommand.AddArgument(staticStepDownStreamArg);
staticStepDownCommand.AddArgument(staticStepDownNameArg);
staticStepDownCommand.AddArgument(staticStepDownMemberArg);
staticStepDownCommand.SetHandler(async (server, stream, name, member) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    await js.PcgStaticMemberStepDownAsync(stream, name, member);
    Console.WriteLine($"Member '{member}' stepped down from group '{name}'");
}, serverOption, staticStepDownStreamArg, staticStepDownNameArg, staticStepDownMemberArg);
staticCommand.AddCommand(staticStepDownCommand);

// ============================================================================
// ELASTIC COMMANDS
// ============================================================================
var elasticCommand = new Command("elastic", "Manage elastic consumer groups");
rootCommand.AddCommand(elasticCommand);

// elastic list
var elasticListCommand = new Command("list", "List all elastic consumer groups on a stream");
elasticListCommand.AddAlias("ls");
var elasticListStreamArg = new Argument<string>("stream", "Stream name");
elasticListCommand.AddArgument(elasticListStreamArg);
elasticListCommand.SetHandler(async (server, stream) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    Console.WriteLine($"Elastic consumer groups on stream '{stream}':");
    var count = 0;
    await foreach (var group in js.ListPcgElasticAsync(stream))
    {
        Console.WriteLine($"  - {group}");
        count++;
    }

    if (count == 0)
    {
        Console.WriteLine("  (none)");
    }
}, serverOption, elasticListStreamArg);
elasticCommand.AddCommand(elasticListCommand);

// elastic info
var elasticInfoCommand = new Command("info", "Get elastic consumer group configuration and active members");
var elasticInfoStreamArg = new Argument<string>("stream", "Stream name");
var elasticInfoNameArg = new Argument<string>("name", "Consumer group name");
elasticInfoCommand.AddArgument(elasticInfoStreamArg);
elasticInfoCommand.AddArgument(elasticInfoNameArg);
elasticInfoCommand.SetHandler(async (server, stream, name) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var config = await js.GetPcgElasticConfigAsync(stream, name);
    Console.WriteLine($"Elastic Consumer Group: {name}");
    Console.WriteLine($"  Stream:                {stream}");
    Console.WriteLine($"  MaxMembers:            {config.MaxMembers}");
    Console.WriteLine($"  Filter:                {config.Filter}");
    Console.WriteLine($"  PartitioningWildcards: [{string.Join(", ", config.PartitioningWildcards)}]");

    if (config.MaxBufferedMsgs.HasValue)
        Console.WriteLine($"  MaxBufferedMsgs:       {config.MaxBufferedMsgs}");
    if (config.MaxBufferedBytes.HasValue)
        Console.WriteLine($"  MaxBufferedBytes:      {config.MaxBufferedBytes}");

    if (config.Members != null)
    {
        Console.WriteLine($"  Members:               {string.Join(", ", config.Members)}");
    }

    if (config.MemberMappings != null)
    {
        Console.WriteLine("  Mappings:");
        foreach (var mapping in config.MemberMappings)
        {
            Console.WriteLine($"    {mapping.Member}: [{string.Join(", ", mapping.Partitions)}]");
        }
    }

    Console.WriteLine("\nActive Members:");
    var activeCount = 0;
    await foreach (var member in js.ListPcgElasticActiveMembersAsync(stream, name))
    {
        Console.WriteLine($"  - {member}");
        activeCount++;
    }

    if (activeCount == 0)
    {
        Console.WriteLine("  (none)");
    }
}, serverOption, elasticInfoStreamArg, elasticInfoNameArg);
elasticCommand.AddCommand(elasticInfoCommand);

// elastic create
var elasticCreateCommand = new Command("create", "Create an elastic consumer group");
var elasticCreateStreamArg = new Argument<string>("stream", "Stream name");
var elasticCreateNameArg = new Argument<string>("name", "Consumer group name");
var elasticCreateMaxMembersArg = new Argument<uint>("max-members", "Maximum number of members (partitions)");
var elasticCreateFilterArg = new Argument<string>("filter", "Filter subject pattern");
var elasticCreateWildcardsArg = new Argument<int[]>("wildcards", "Partitioning wildcard indexes (1-based)");
var elasticCreateMaxMsgsOption = new Option<long?>("--max-buffered-msgs", "Max buffered messages");
var elasticCreateMaxBytesOption = new Option<long?>("--max-buffered-bytes", "Max buffered bytes");
elasticCreateCommand.AddArgument(elasticCreateStreamArg);
elasticCreateCommand.AddArgument(elasticCreateNameArg);
elasticCreateCommand.AddArgument(elasticCreateMaxMembersArg);
elasticCreateCommand.AddArgument(elasticCreateFilterArg);
elasticCreateCommand.AddArgument(elasticCreateWildcardsArg);
elasticCreateCommand.AddOption(elasticCreateMaxMsgsOption);
elasticCreateCommand.AddOption(elasticCreateMaxBytesOption);
elasticCreateCommand.SetHandler(async context =>
{
    var server = context.ParseResult.GetValueForOption(serverOption)!;
    var stream = context.ParseResult.GetValueForArgument(elasticCreateStreamArg);
    var name = context.ParseResult.GetValueForArgument(elasticCreateNameArg);
    var maxMembers = context.ParseResult.GetValueForArgument(elasticCreateMaxMembersArg);
    var filter = context.ParseResult.GetValueForArgument(elasticCreateFilterArg);
    var wildcards = context.ParseResult.GetValueForArgument(elasticCreateWildcardsArg);
    var maxMsgs = context.ParseResult.GetValueForOption(elasticCreateMaxMsgsOption);
    var maxBytes = context.ParseResult.GetValueForOption(elasticCreateMaxBytesOption);

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var config = await js.CreatePcgElasticAsync(stream, name, maxMembers, filter, wildcards,
        maxBufferedMessages: maxMsgs,
        maxBufferedBytes: maxBytes);

    Console.WriteLine($"Created elastic consumer group '{name}' on stream '{stream}'");
    Console.WriteLine($"  MaxMembers:            {config.MaxMembers}");
    Console.WriteLine($"  Filter:                {config.Filter}");
    Console.WriteLine($"  PartitioningWildcards: [{string.Join(", ", config.PartitioningWildcards)}]");
});
elasticCommand.AddCommand(elasticCreateCommand);

// elastic delete
var elasticDeleteCommand = new Command("delete", "Delete an elastic consumer group");
elasticDeleteCommand.AddAlias("rm");
var elasticDeleteStreamArg = new Argument<string>("stream", "Stream name");
var elasticDeleteNameArg = new Argument<string>("name", "Consumer group name");
var elasticDeleteForceOption = new Option<bool>(["--force", "-f"], "Skip confirmation");
elasticDeleteCommand.AddArgument(elasticDeleteStreamArg);
elasticDeleteCommand.AddArgument(elasticDeleteNameArg);
elasticDeleteCommand.AddOption(elasticDeleteForceOption);
elasticDeleteCommand.SetHandler(async (server, stream, name, force) =>
{
    if (!force)
    {
        Console.Write($"Delete elastic consumer group '{name}' on stream '{stream}'? [y/N] ");
        var response = Console.ReadLine();
        if (response?.ToLowerInvariant() != "y")
        {
            Console.WriteLine("Cancelled.");
            return;
        }
    }

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    await js.DeletePcgElasticAsync(stream, name);
    Console.WriteLine($"Deleted elastic consumer group '{name}'");
}, serverOption, elasticDeleteStreamArg, elasticDeleteNameArg, elasticDeleteForceOption);
elasticCommand.AddCommand(elasticDeleteCommand);

// elastic add
var elasticAddCommand = new Command("add", "Add members to an elastic consumer group");
var elasticAddStreamArg = new Argument<string>("stream", "Stream name");
var elasticAddNameArg = new Argument<string>("name", "Consumer group name");
var elasticAddMembersArg = new Argument<string[]>("members", "Member names to add");
elasticAddCommand.AddArgument(elasticAddStreamArg);
elasticAddCommand.AddArgument(elasticAddNameArg);
elasticAddCommand.AddArgument(elasticAddMembersArg);
elasticAddCommand.SetHandler(async (server, stream, name, members) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var updatedMembers = await js.AddPcgElasticMembersAsync(stream, name, members);
    Console.WriteLine($"Added members to '{name}'");
    Console.WriteLine($"Current members: {string.Join(", ", updatedMembers)}");
}, serverOption, elasticAddStreamArg, elasticAddNameArg, elasticAddMembersArg);
elasticCommand.AddCommand(elasticAddCommand);

// elastic drop
var elasticDropCommand = new Command("drop", "Remove members from an elastic consumer group");
var elasticDropStreamArg = new Argument<string>("stream", "Stream name");
var elasticDropNameArg = new Argument<string>("name", "Consumer group name");
var elasticDropMembersArg = new Argument<string[]>("members", "Member names to remove");
elasticDropCommand.AddArgument(elasticDropStreamArg);
elasticDropCommand.AddArgument(elasticDropNameArg);
elasticDropCommand.AddArgument(elasticDropMembersArg);
elasticDropCommand.SetHandler(async (server, stream, name, members) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var updatedMembers = await js.DeletePcgElasticMembersAsync(stream, name, members);
    Console.WriteLine($"Removed members from '{name}'");
    Console.WriteLine($"Current members: {(updatedMembers.Length > 0 ? string.Join(", ", updatedMembers) : "(none)")}");
}, serverOption, elasticDropStreamArg, elasticDropNameArg, elasticDropMembersArg);
elasticCommand.AddCommand(elasticDropCommand);

// elastic set-mappings
var elasticSetMappingsCommand = new Command("set-mappings", "Set member mappings for an elastic consumer group");
elasticSetMappingsCommand.AddAlias("sm");
var elasticSetMappingsStreamArg = new Argument<string>("stream", "Stream name");
var elasticSetMappingsNameArg = new Argument<string>("name", "Consumer group name");
var elasticSetMappingsMappingsArg = new Argument<string[]>("mappings", "Member mappings in format member:p1,p2,p3");
elasticSetMappingsCommand.AddArgument(elasticSetMappingsStreamArg);
elasticSetMappingsCommand.AddArgument(elasticSetMappingsNameArg);
elasticSetMappingsCommand.AddArgument(elasticSetMappingsMappingsArg);
elasticSetMappingsCommand.SetHandler(async (server, stream, name, mappingsStr) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var mappings = ParseMappings(mappingsStr);
    await js.SetPcgElasticMemberMappingsAsync(stream, name, mappings);

    Console.WriteLine($"Set mappings for '{name}':");
    foreach (var mapping in mappings)
    {
        Console.WriteLine($"  {mapping.Member}: [{string.Join(", ", mapping.Partitions)}]");
    }
}, serverOption, elasticSetMappingsStreamArg, elasticSetMappingsNameArg, elasticSetMappingsMappingsArg);
elasticCommand.AddCommand(elasticSetMappingsCommand);

// elastic delete-mappings
var elasticDeleteMappingsCommand = new Command("delete-mappings", "Delete member mappings for an elastic consumer group");
elasticDeleteMappingsCommand.AddAlias("dm");
var elasticDeleteMappingsStreamArg = new Argument<string>("stream", "Stream name");
var elasticDeleteMappingsNameArg = new Argument<string>("name", "Consumer group name");
elasticDeleteMappingsCommand.AddArgument(elasticDeleteMappingsStreamArg);
elasticDeleteMappingsCommand.AddArgument(elasticDeleteMappingsNameArg);
elasticDeleteMappingsCommand.SetHandler(async (server, stream, name) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    await js.DeletePcgElasticMemberMappingsAsync(stream, name);
    Console.WriteLine($"Deleted mappings for '{name}'");
}, serverOption, elasticDeleteMappingsStreamArg, elasticDeleteMappingsNameArg);
elasticCommand.AddCommand(elasticDeleteMappingsCommand);

// elastic step-down
var elasticStepDownCommand = new Command("step-down", "Force a member to step down");
elasticStepDownCommand.AddAlias("sd");
var elasticStepDownStreamArg = new Argument<string>("stream", "Stream name");
var elasticStepDownNameArg = new Argument<string>("name", "Consumer group name");
var elasticStepDownMemberArg = new Argument<string>("member", "Member name");
elasticStepDownCommand.AddArgument(elasticStepDownStreamArg);
elasticStepDownCommand.AddArgument(elasticStepDownNameArg);
elasticStepDownCommand.AddArgument(elasticStepDownMemberArg);
elasticStepDownCommand.SetHandler(async (server, stream, name, member) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    await js.PcgElasticMemberStepDownAsync(stream, name, member);
    Console.WriteLine($"Member '{member}' stepped down from group '{name}'");
}, serverOption, elasticStepDownStreamArg, elasticStepDownNameArg, elasticStepDownMemberArg);
elasticCommand.AddCommand(elasticStepDownCommand);

// elastic member-info
var elasticMemberInfoCommand = new Command("member-info", "Get member info (membership and active status)");
elasticMemberInfoCommand.AddAlias("minfo");
var elasticMemberInfoStreamArg = new Argument<string>("stream", "Stream name");
var elasticMemberInfoNameArg = new Argument<string>("name", "Consumer group name");
var elasticMemberInfoMemberArg = new Argument<string>("member", "Member name");
elasticMemberInfoCommand.AddArgument(elasticMemberInfoStreamArg);
elasticMemberInfoCommand.AddArgument(elasticMemberInfoNameArg);
elasticMemberInfoCommand.AddArgument(elasticMemberInfoMemberArg);
elasticMemberInfoCommand.SetHandler(async (server, stream, name, member) =>
{
    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = new NatsJSContext(nats);

    var (isInMembership, isActive) = await js.IsInPcgElasticMembershipAndActiveAsync(stream, name, member);
    Console.WriteLine($"Member: {member}");
    Console.WriteLine($"  In Membership: {isInMembership}");
    Console.WriteLine($"  Active:        {isActive}");
}, serverOption, elasticMemberInfoStreamArg, elasticMemberInfoNameArg, elasticMemberInfoMemberArg);
elasticCommand.AddCommand(elasticMemberInfoCommand);

// Build and run
var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .Build();

return await parser.InvokeAsync(args);

// Helper functions
static NatsPcgMemberMapping[] ParseMappings(string[] mappingsStr)
{
    var mappings = new List<NatsPcgMemberMapping>();
    foreach (var mappingStr in mappingsStr)
    {
        var parts = mappingStr.Split(':');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid mapping format: '{mappingStr}'. Expected 'member:p1,p2,p3'");
        }

        var member = parts[0];
        var partitions = parts[1].Split(',').Select(p => int.Parse(p.Trim())).ToArray();
        mappings.Add(new NatsPcgMemberMapping(member, partitions));
    }

    return mappings.ToArray();
}
