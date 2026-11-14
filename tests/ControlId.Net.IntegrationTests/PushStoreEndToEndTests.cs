using System.Net.Http.Json;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Push;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Net.IntegrationTests;

/// <summary>A fila do Push vive no <see cref="IPushCommandStore"/>: um novo <c>PushServer</c> sobre o mesmo store enxerga o que ficou.</summary>
public sealed class PushStoreEndToEndTests
{
    private const long SimulatorDeviceId = 935107;

    private sealed class CapturingHandler : IPushResultHandler
    {
        public TaskCompletionSource<PushCompletion> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            Completion.TrySetResult(completion);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_pending_command_survives_a_new_push_server_over_the_same_store()
    {
        var store = new InMemoryPushCommandStore();
        var first = new PushServer(store);
        var id = await first.EnqueueAsync(SimulatorDeviceId, PushCommand.ShowMessage("Sobreviveu"));
        Assert.Equal(1, await first.GetPendingCountAsync(SimulatorDeviceId));

        // "Reinício": o servidor antigo some, o novo usa o mesmo store e o handler recebe o resultado de quem ninguém espera.
        var handler = new CapturingHandler();
        var second = new PushServer(store, handler);
        Assert.Equal(1, await second.GetPendingCountAsync(SimulatorDeviceId));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.Services.AddSingleton(second);
        await using var server = builder.Build();
        server.MapControlIdPush();
        await server.StartAsync();
        var address = new Uri(server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First());

        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = address.ToString().TrimEnd('/'), RequestPeriod = TimeSpan.FromSeconds(1) });

        var completion = await handler.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(id, completion.CommandId);
        Assert.Equal(SimulatorDeviceId, completion.DeviceId);
        Assert.Null(completion.Error);
        Assert.Single(completion.Results);
        Assert.True(completion.Results[0].Success);
        Assert.Equal(0, await second.GetPendingCountAsync(SimulatorDeviceId));
        var state = await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");
        Assert.Equal("Sobreviveu", state.GetProperty("screenMessage").GetString());
    }
}
