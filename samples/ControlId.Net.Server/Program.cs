// Servidor de exemplo para os modos Push e Monitor.
//
//   dotnet run --project samples/ControlId.Net.Server -- --urls http://0.0.0.0:5000
//
// No equipamento (uma vez, pelo AccessClient ou pela interface web):
//   Monitor: hostname = IP desta máquina, port = 5000, path = api/notifications
//   Push:    push_remote_address = IP desta máquina:5000
//   Online:  device.ConfigureOnlineModeAsync(new OnlineModeConfiguration { ServerAddress = "http://IP desta máquina:5000/api" })
//
// Depois, com o equipamento consultando o /push:
//   curl -X POST http://localhost:5000/equipamentos/{deviceId}/abrir-porta
//   curl http://localhost:5000/equipamentos/{deviceId}/usuarios
using System;
using System.Threading;
using System.Threading.Tasks;
using ControlId;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.Access.Push;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControlIdPush();
builder.Services.AddControlIdMonitor((notification, _) =>
{
    switch (notification)
    {
        case DaoNotification dao:
            foreach (var log in dao.GetInsertedAccessLogs())
            {
                Console.WriteLine($"[{dao.DeviceId}] acesso: usuário {log.UserId}, evento {log.Event}, portal {log.PortalId}");
            }

            break;
        case DoorNotification door:
            Console.WriteLine($"[{door.DeviceId}] porta {door.Door?.Id} {(door.Door?.Open == true ? "aberta" : "fechada")}");
            break;
        case RemoteEnrollNotification enroll:
            Console.WriteLine(enroll.Result.Success
                ? $"[{enroll.DeviceId}] cadastro remoto de {enroll.EnrollType} concluído para o usuário {enroll.Result.UserId}"
                : $"[{enroll.DeviceId}] cadastro remoto de {enroll.EnrollType} falhou: {enroll.Result.Error}");
            break;
        case OperationModeNotification mode:
            Console.WriteLine($"[{mode.DeviceId}] modo de operação: {mode.OperationMode?.ModeName}");
            break;
        default:
            Console.WriteLine($"[{notification.DeviceId}] {notification.Endpoint}: {notification.Raw}");
            break;
    }

    return Task.CompletedTask;
});

// Modo online (Pro): regra de demonstração. Libera quem o equipamento identificou, exceto o usuário 13.
builder.Services.AddControlIdOnline((identification, _) =>
{
    OnlineAuthorization decision = identification switch
    {
        UserIdentifiedEvent { UserId: 13 } user => OnlineAuthorization.Deny(user.UserId, user.UserName, "Acesso bloqueado"),
        UserIdentifiedEvent { UserId: > 0 } user => OnlineAuthorization.Grant(user.UserId, user.UserName, DeviceAction.OpenDoor(1)),
        _ => OnlineAuthorization.NotIdentified(),
    };

    Console.WriteLine($"[{identification.DeviceId}] online {identification.Kind}: {decision.Event}");
    return Task.FromResult(decision);
});

var app = builder.Build();

app.MapControlIdPush();
app.MapControlIdMonitor();
app.MapControlIdOnline("/api");

// API de exemplo que comanda os equipamentos via Push.
var equipamentos = app.MapGroup("/equipamentos/{deviceId:long}");

equipamentos.MapPost("/abrir-porta", async (long deviceId, PushServer push, CancellationToken ct) =>
    await RunAsync(ct, async token =>
    {
        await push.Device(deviceId).OpenDoorAsync(1, token);
        return Results.Ok();
    }));

equipamentos.MapGet("/usuarios", async (long deviceId, PushServer push, CancellationToken ct) =>
    await RunAsync(ct, async token =>
        Results.Ok(await push.Device(deviceId).LoadObjectsAsync<AccessUser>(new LoadObjectsRequest("users"), token))));

equipamentos.MapPost("/usuarios/{userId:long}/cartao", async (long deviceId, long userId, PushServer push, CancellationToken ct) =>
    await RunAsync(ct, async token =>
    {
        var request = new RemoteEnrollRequest(RemoteEnrollType.Card) { UserId = userId, Save = true, Message = "Aproxime o cartão" };
        var result = (await push.Device(deviceId).SendAsync(PushCommand.RemoteEnroll(request), token)).ResponseAs<RemoteEnrollResult>();
        return result.Success ? Results.Ok(new { cartao = result.CardValue }) : Results.UnprocessableEntity(new { erro = result.Error });
    }));

equipamentos.MapGet("/status", (long deviceId, PushServer push) =>
    Results.Ok(new { ultimaConsulta = push.GetLastSeen(deviceId), pendentes = push.GetPendingCount(deviceId) }));

app.Run();

// O comando só termina quando o equipamento busca e responde; sem prazo, a requisição ficaria pendurada.
static async Task<IResult> RunAsync(CancellationToken requestAborted, Func<CancellationToken, Task<IResult>> action)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    timeout.CancelAfter(TimeSpan.FromSeconds(30));
    try
    {
        return await action(timeout.Token);
    }
    catch (OperationCanceledException) when (!requestAborted.IsCancellationRequested)
    {
        return Results.Problem("O equipamento não buscou o comando a tempo (offline?).", statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (ControlIdException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}
