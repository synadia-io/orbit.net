using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Synadia.Orbit.TestUtils;

namespace Synadia.Orbit.DirectGet.Test;

[Collection("nats-server")]
public class MultiDirectGetTest
{
    private readonly ITestOutputHelper _output;
    private readonly NatsServerFixture _server;

    public MultiDirectGetTest(ITestOutputHelper output, NatsServerFixture server)
    {
        _output = output;
        _server = server;
    }

    [Fact]
    public async Task Get_many_messages()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = _server.Url });
        INatsJSContext js = connection.CreateJetStreamContext();
        string prefix = _server.GetNextId();
        string name = $"{prefix}S1";
        string suject = $"{prefix}s1";

        CancellationToken ct = TestContext.Current.CancellationToken;

        await js.CreateStreamAsync(new StreamConfig(name, [suject]){AllowDirect = true}, ct);

        for (int i = 0; i < 10; i++)
        {
            await js.PublishAsync(subject: suject, i, cancellationToken: ct);
        }

        StreamMsgBatchGetRequest request = new()
        {
            Batch = 10,
            Seq = 1,
        };

        int count = 0;
        await foreach (NatsMsg<int> msg in js.GetBatchDirectAsync<int>(name, request, cancellationToken: ct))
        {
            Assert.Equal(count++, msg.Data);
            _output.WriteLine($"GetBatchDirectAsync: {msg.Data}");
        }
    }
}
