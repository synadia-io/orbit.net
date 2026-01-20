// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable SuggestVarOrType_BuiltInTypes
using System.CommandLine;
using NATS.Client.Core;
using NATS.Net;
using Synadia.Orbit.PCGroups;
using Synadia.Orbit.PCGroups.Elastic;
using Synadia.Orbit.PCGroups.Static;

var serverOption = new Option<string>("--server", "-s")
{
    Description = "NATS server URL",
    DefaultValueFactory = _ => "nats://localhost:4222",
};

var rootCommand = new RootCommand("OrbitPcg - NATS Partitioned Consumer Groups CLI") { serverOption };

// ============================================================================
// STATIC COMMANDS
// ============================================================================
var staticCommand = new Command("static", "Manage static consumer groups");
rootCommand.Add(staticCommand);

// static list
var staticListStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var staticListCommand = new Command("list", "List all static consumer groups on a stream") { staticListStreamArg };
staticListCommand.Aliases.Add("ls");
staticListCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(staticListStreamArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    Console.WriteLine($"Static consumer groups on stream '{stream}':");
    var count = 0;
    await foreach (var group in js.ListPcgStaticAsync(stream, ct))
    {
        Console.WriteLine($"  - {group}");
        count++;
    }

    if (count == 0)
    {
        Console.WriteLine("  (none)");
    }
});
staticCommand.Add(staticListCommand);

// static info
var staticInfoStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var staticInfoNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var staticInfoCommand = new Command("info", "Get static consumer group configuration and active members") { staticInfoStreamArg, staticInfoNameArg };
staticInfoCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(staticInfoStreamArg)!;
    var name = parseResult.GetValue(staticInfoNameArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var config = await js.GetPcgStaticConfigAsync(stream, name, ct);
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
    await foreach (var member in js.ListPcgStaticActiveMembersAsync(stream, name, ct))
    {
        Console.WriteLine($"  - {member}");
        activeCount++;
    }

    if (activeCount == 0)
    {
        Console.WriteLine("  (none)");
    }
});
staticCommand.Add(staticInfoCommand);

// static create
var staticCreateStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var staticCreateNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var staticCreateMaxMembersArg = new Argument<uint>("max-members") { Description = "Maximum number of members (partitions)" };
var staticCreateFilterOption = new Option<string?>("--filter") { Description = "Filter subject pattern" };
var staticCreateMembersOption = new Option<string[]?>("--members") { Description = "Member names (space-separated)", AllowMultipleArgumentsPerToken = true };
var staticCreateMappingsOption = new Option<string[]?>("--mappings") { Description = "Member mappings in format member:p1,p2,p3", AllowMultipleArgumentsPerToken = true };
var staticCreateCommand = new Command("create", "Create a static consumer group")
{
    staticCreateStreamArg, staticCreateNameArg, staticCreateMaxMembersArg,
    staticCreateFilterOption, staticCreateMembersOption, staticCreateMappingsOption,
};
staticCreateCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(staticCreateStreamArg)!;
    var name = parseResult.GetValue(staticCreateNameArg)!;
    var maxMembers = parseResult.GetValue(staticCreateMaxMembersArg);
    var filter = parseResult.GetValue(staticCreateFilterOption);
    var members = parseResult.GetValue(staticCreateMembersOption);
    var mappingsStr = parseResult.GetValue(staticCreateMappingsOption);

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    NatsPcgMemberMapping[]? mappings = null;
    if (mappingsStr is { Length: > 0 })
    {
        mappings = ParseMappings(mappingsStr);
    }

    var config = await js.CreatePcgStaticAsync(stream, name, maxMembers,
        filter: filter, members: members, memberMappings: mappings, cancellationToken: ct);

    Console.WriteLine($"Created static consumer group '{name}' on stream '{stream}'");
    Console.WriteLine($"  MaxMembers: {config.MaxMembers}");
    if (config.Filter != null)
        Console.WriteLine($"  Filter: {config.Filter}");
});
staticCommand.Add(staticCreateCommand);

// static delete
var staticDeleteStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var staticDeleteNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var staticDeleteForceOption = new Option<bool>("--force", "-f") { Description = "Skip confirmation" };
var staticDeleteCommand = new Command("delete", "Delete a static consumer group") { staticDeleteStreamArg, staticDeleteNameArg, staticDeleteForceOption };
staticDeleteCommand.Aliases.Add("rm");
staticDeleteCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(staticDeleteStreamArg)!;
    var name = parseResult.GetValue(staticDeleteNameArg)!;
    var force = parseResult.GetValue(staticDeleteForceOption);

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
    var js = nats.CreateJetStreamContext();

    await js.DeletePcgStaticAsync(stream, name, ct);
    Console.WriteLine($"Deleted static consumer group '{name}'");
});
staticCommand.Add(staticDeleteCommand);

// static step-down
var staticStepDownStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var staticStepDownNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var staticStepDownMemberArg = new Argument<string>("member") { Description = "Member name" };
var staticStepDownCommand = new Command("step-down", "Force a member to step down") { staticStepDownStreamArg, staticStepDownNameArg, staticStepDownMemberArg };
staticStepDownCommand.Aliases.Add("sd");
staticStepDownCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(staticStepDownStreamArg)!;
    var name = parseResult.GetValue(staticStepDownNameArg)!;
    var member = parseResult.GetValue(staticStepDownMemberArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    await js.PcgStaticMemberStepDownAsync(stream, name, member, ct);
    Console.WriteLine($"Member '{member}' stepped down from group '{name}'");
});
staticCommand.Add(staticStepDownCommand);

// ============================================================================
// ELASTIC COMMANDS
// ============================================================================
var elasticCommand = new Command("elastic", "Manage elastic consumer groups");
rootCommand.Add(elasticCommand);

// elastic list
var elasticListStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticListCommand = new Command("list", "List all elastic consumer groups on a stream") { elasticListStreamArg };
elasticListCommand.Aliases.Add("ls");
elasticListCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticListStreamArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    Console.WriteLine($"Elastic consumer groups on stream '{stream}':");
    var count = 0;
    await foreach (var group in js.ListPcgElasticAsync(stream, ct))
    {
        Console.WriteLine($"  - {group}");
        count++;
    }

    if (count == 0)
    {
        Console.WriteLine("  (none)");
    }
});
elasticCommand.Add(elasticListCommand);

// elastic info
var elasticInfoStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticInfoNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticInfoCommand = new Command("info", "Get elastic consumer group configuration and active members") { elasticInfoStreamArg, elasticInfoNameArg };
elasticInfoCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticInfoStreamArg)!;
    var name = parseResult.GetValue(elasticInfoNameArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var config = await js.GetPcgElasticConfigAsync(stream, name, ct);
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
    await foreach (var member in js.ListPcgElasticActiveMembersAsync(stream, name, ct))
    {
        Console.WriteLine($"  - {member}");
        activeCount++;
    }

    if (activeCount == 0)
    {
        Console.WriteLine("  (none)");
    }
});
elasticCommand.Add(elasticInfoCommand);

// elastic create
var elasticCreateStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticCreateNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticCreateMaxMembersArg = new Argument<uint>("max-members") { Description = "Maximum number of members (partitions)" };
var elasticCreateFilterArg = new Argument<string>("filter") { Description = "Filter subject pattern" };
var elasticCreateWildcardsArg = new Argument<int[]>("wildcards") { Description = "Partitioning wildcard indexes (1-based)" };
var elasticCreateMaxMsgsOption = new Option<long?>("--max-buffered-msgs") { Description = "Max buffered messages" };
var elasticCreateMaxBytesOption = new Option<long?>("--max-buffered-bytes") { Description = "Max buffered bytes" };
var elasticCreateCommand = new Command("create", "Create an elastic consumer group")
{
    elasticCreateStreamArg, elasticCreateNameArg, elasticCreateMaxMembersArg,
    elasticCreateFilterArg, elasticCreateWildcardsArg, elasticCreateMaxMsgsOption, elasticCreateMaxBytesOption,
};
elasticCreateCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticCreateStreamArg)!;
    var name = parseResult.GetValue(elasticCreateNameArg)!;
    var maxMembers = parseResult.GetValue(elasticCreateMaxMembersArg);
    var filter = parseResult.GetValue(elasticCreateFilterArg)!;
    var wildcards = parseResult.GetValue(elasticCreateWildcardsArg)!;
    var maxMsgs = parseResult.GetValue(elasticCreateMaxMsgsOption);
    var maxBytes = parseResult.GetValue(elasticCreateMaxBytesOption);

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var config = await js.CreatePcgElasticAsync(stream, name, maxMembers, filter, wildcards,
        maxBufferedMessages: maxMsgs, maxBufferedBytes: maxBytes, cancellationToken: ct);

    Console.WriteLine($"Created elastic consumer group '{name}' on stream '{stream}'");
    Console.WriteLine($"  MaxMembers:            {config.MaxMembers}");
    Console.WriteLine($"  Filter:                {config.Filter}");
    Console.WriteLine($"  PartitioningWildcards: [{string.Join(", ", config.PartitioningWildcards)}]");
});
elasticCommand.Add(elasticCreateCommand);

// elastic delete
var elasticDeleteStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticDeleteNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticDeleteForceOption = new Option<bool>("--force", "-f") { Description = "Skip confirmation" };
var elasticDeleteCommand = new Command("delete", "Delete an elastic consumer group") { elasticDeleteStreamArg, elasticDeleteNameArg, elasticDeleteForceOption };
elasticDeleteCommand.Aliases.Add("rm");
elasticDeleteCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticDeleteStreamArg)!;
    var name = parseResult.GetValue(elasticDeleteNameArg)!;
    var force = parseResult.GetValue(elasticDeleteForceOption);

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
    var js = nats.CreateJetStreamContext();

    await js.DeletePcgElasticAsync(stream, name, ct);
    Console.WriteLine($"Deleted elastic consumer group '{name}'");
});
elasticCommand.Add(elasticDeleteCommand);

// elastic add
var elasticAddStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticAddNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticAddMembersArg = new Argument<string[]>("members") { Description = "Member names to add" };
var elasticAddCommand = new Command("add", "Add members to an elastic consumer group") { elasticAddStreamArg, elasticAddNameArg, elasticAddMembersArg };
elasticAddCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticAddStreamArg)!;
    var name = parseResult.GetValue(elasticAddNameArg)!;
    var members = parseResult.GetValue(elasticAddMembersArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var updatedMembers = await js.AddPcgElasticMembersAsync(stream, name, members, ct);
    Console.WriteLine($"Added members to '{name}'");
    Console.WriteLine($"Current members: {string.Join(", ", updatedMembers)}");
});
elasticCommand.Add(elasticAddCommand);

// elastic drop
var elasticDropStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticDropNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticDropMembersArg = new Argument<string[]>("members") { Description = "Member names to remove" };
var elasticDropCommand = new Command("drop", "Remove members from an elastic consumer group") { elasticDropStreamArg, elasticDropNameArg, elasticDropMembersArg };
elasticDropCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticDropStreamArg)!;
    var name = parseResult.GetValue(elasticDropNameArg)!;
    var members = parseResult.GetValue(elasticDropMembersArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var updatedMembers = await js.DeletePcgElasticMembersAsync(stream, name, members, ct);
    Console.WriteLine($"Removed members from '{name}'");
    Console.WriteLine($"Current members: {(updatedMembers.Length > 0 ? string.Join(", ", updatedMembers) : "(none)")}");
});
elasticCommand.Add(elasticDropCommand);

// elastic set-mappings
var elasticSetMappingsStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticSetMappingsNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticSetMappingsMappingsArg = new Argument<string[]>("mappings") { Description = "Member mappings in format member:p1,p2,p3" };
var elasticSetMappingsCommand = new Command("set-mappings", "Set member mappings for an elastic consumer group") { elasticSetMappingsStreamArg, elasticSetMappingsNameArg, elasticSetMappingsMappingsArg };
elasticSetMappingsCommand.Aliases.Add("sm");
elasticSetMappingsCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticSetMappingsStreamArg)!;
    var name = parseResult.GetValue(elasticSetMappingsNameArg)!;
    var mappingsStr = parseResult.GetValue(elasticSetMappingsMappingsArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var mappings = ParseMappings(mappingsStr);
    await js.SetPcgElasticMemberMappingsAsync(stream, name, mappings, ct);

    Console.WriteLine($"Set mappings for '{name}':");
    foreach (var mapping in mappings)
    {
        Console.WriteLine($"  {mapping.Member}: [{string.Join(", ", mapping.Partitions)}]");
    }
});
elasticCommand.Add(elasticSetMappingsCommand);

// elastic delete-mappings
var elasticDeleteMappingsStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticDeleteMappingsNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticDeleteMappingsCommand = new Command("delete-mappings", "Delete member mappings for an elastic consumer group") { elasticDeleteMappingsStreamArg, elasticDeleteMappingsNameArg };
elasticDeleteMappingsCommand.Aliases.Add("dm");
elasticDeleteMappingsCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticDeleteMappingsStreamArg)!;
    var name = parseResult.GetValue(elasticDeleteMappingsNameArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    await js.DeletePcgElasticMemberMappingsAsync(stream, name, ct);
    Console.WriteLine($"Deleted mappings for '{name}'");
});
elasticCommand.Add(elasticDeleteMappingsCommand);

// elastic step-down
var elasticStepDownStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticStepDownNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticStepDownMemberArg = new Argument<string>("member") { Description = "Member name" };
var elasticStepDownCommand = new Command("step-down", "Force a member to step down") { elasticStepDownStreamArg, elasticStepDownNameArg, elasticStepDownMemberArg };
elasticStepDownCommand.Aliases.Add("sd");
elasticStepDownCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticStepDownStreamArg)!;
    var name = parseResult.GetValue(elasticStepDownNameArg)!;
    var member = parseResult.GetValue(elasticStepDownMemberArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    await js.PcgElasticMemberStepDownAsync(stream, name, member, ct);
    Console.WriteLine($"Member '{member}' stepped down from group '{name}'");
});
elasticCommand.Add(elasticStepDownCommand);

// elastic member-info
var elasticMemberInfoStreamArg = new Argument<string>("stream") { Description = "Stream name" };
var elasticMemberInfoNameArg = new Argument<string>("name") { Description = "Consumer group name" };
var elasticMemberInfoMemberArg = new Argument<string>("member") { Description = "Member name" };
var elasticMemberInfoCommand = new Command("member-info", "Get member info (membership and active status)") { elasticMemberInfoStreamArg, elasticMemberInfoNameArg, elasticMemberInfoMemberArg };
elasticMemberInfoCommand.Aliases.Add("minfo");
elasticMemberInfoCommand.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverOption)!;
    var stream = parseResult.GetValue(elasticMemberInfoStreamArg)!;
    var name = parseResult.GetValue(elasticMemberInfoNameArg)!;
    var member = parseResult.GetValue(elasticMemberInfoMemberArg)!;

    await using var nats = new NatsConnection(new NatsOpts { Url = server });
    var js = nats.CreateJetStreamContext();

    var (isInMembership, isActive) = await js.IsInPcgElasticMembershipAndActiveAsync(stream, name, member, ct);
    Console.WriteLine($"Member: {member}");
    Console.WriteLine($"  In Membership: {isInMembership}");
    Console.WriteLine($"  Active:        {isActive}");
});
elasticCommand.Add(elasticMemberInfoCommand);

// Run
return await rootCommand.Parse(args).InvokeAsync();

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
