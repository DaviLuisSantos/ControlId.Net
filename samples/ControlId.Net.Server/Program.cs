// Servidor de exemplo para os modos Push, Monitor e Online, com os callbacks protegidos por token.
//
// 1. Segredos (nunca no appsettings.json, que é versionado). Sem eles a aplicação não sobe:
//      dotnet user-secrets set --id controlid-net-server-sample ControlId:CallbackToken "<token longo e aleatório, 16+ caracteres>"
//      dotnet user-secrets set --id controlid-net-server-sample ApiKey "<chave da API de operação, 16+ caracteres>"
//    (user-secrets só são lidos no ambiente Development; em outro ambiente use as variáveis ControlId__CallbackToken e ApiKey.)
//
// 2. Subir. Só nesta máquina:
//      dotnet run --project samples/ControlId.Net.Server -- --environment Development --urls http://localhost:5000
//    Para os equipamentos alcançarem o servidor, escute no IP da rede (ex.: --urls http://192.168.0.10:5000). Os callbacks
//    exigem o token e a API exige a chave, mas HTTP trafega em claro (inclusive o token): em produção use HTTPS (proxy
//    reverso ou Kestrel com certificado, e ConfigureCallbackSecurityAsync(token, pinnedCertificate: ...) no equipamento)
//    e autenticação de verdade na API. Não exponha em 0.0.0.0 numa rede que você não controla.
//
// 3. No equipamento (uma vez, pelo AccessClient ou pela interface web):
//      await device.ConfigureCallbackSecurityAsync("<o mesmo ControlId:CallbackToken>");   // Authorization: Bearer nos callbacks
//      Monitor: hostname = IP desta máquina, port = 5000, path = api/notifications
//      Push:    push_remote_address = IP desta máquina:5000
//      Online:  device.ConfigureOnlineModeAsync(new OnlineModeConfiguration { ServerAddress = "http://IP desta máquina:5000/api" })
//
// 4. Com o equipamento consultando o /push:
//      curl -X POST -H "X-Api-Key: <ApiKey>" http://localhost:5000/equipamentos/{deviceId}/abrir-porta
//      curl -H "X-Api-Key: <ApiKey>" http://localhost:5000/equipamentos/{deviceId}/usuarios
//
// Equipamentos acessados direto por HTTP (ex.: o simulador), com traces e métricas no dashboard do Aspire. As rotas /direto
// só existem se a seção ControlId:Access tiver pelo menos um equipamento, ex. no appsettings.json ou na linha de comando:
//      "ControlId": { "Access": { "simulador": { "BaseAddress": "http://localhost:8080" } } }   (senha: Password, por user-secrets)
//   docker compose -f samples/docker-compose.yml up -d
//   dotnet run --project samples/ControlId.Net.Server -- --environment Development --urls http://localhost:5000 \
//     --ControlId:Access:simulador:BaseAddress=http://localhost:8080 --OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
//   curl -H "X-Api-Key: <ApiKey>" http://localhost:5000/direto/simulador/usuarios      (traces em http://localhost:18888)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using ControlId;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using ControlId.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Id dos user-secrets deste sample (equivale ao <UserSecretsId> do csproj).
[assembly: UserSecretsId("controlid-net-server-sample")]

const string CallbackRateLimit = "controlid-callbacks";

var builder = WebApplication.CreateBuilder(args);

// Segredos: token dos callbacks (o mesmo gravado no equipamento) e chave da API de operação. Sem eles, não sobe.
var problems = new List<string>();
var callbackToken = ReadSecret(builder.Configuration, "ControlId:CallbackToken", "ControlId__CallbackToken", problems);
var previousCallbackToken = builder.Configuration["ControlId:PreviousCallbackToken"] is { Length: > 0 } previous
    ? previous
    : null;   // opcional: aceita também o token anterior durante a rotação
var apiKey = ReadSecret(builder.Configuration, "ApiKey", "ApiKey", problems);
if (problems.Count > 0)
{
    Console.Error.WriteLine("O servidor de exemplo não subiu:");
    problems.ForEach(p => Console.Error.WriteLine("  - " + p));
    Console.Error.WriteLine("Veja o comentário no topo de samples/ControlId.Net.Server/Program.cs.");
    return 1;
}

// Traces e métricas do ControlId.Net (e do ASP.NET Core e do HttpClient) via OTLP, se houver para onde mandar.
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("ControlId.Net.Server"))
        .WithTracing(t => t
            .AddSource(ControlIdTelemetry.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithMetrics(m => m
            .AddMeter(ControlIdTelemetry.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .UseOtlpExporter();
}

// Equipamentos acessados direto por HTTP, de ControlId:Access:{nome}. A seção é opcional neste sample:
// AddControlIdAccessDevices lança com a seção vazia ou inexistente (protege contra nome de seção errado).
var directDevices = builder.Configuration.GetSection("ControlId:Access");
var hasDirectDevices = directDevices.GetChildren().Any();
if (hasDirectDevices)
{
    builder.Services.AddControlIdAccessDevices(directDevices);
}

// GET /health: cada equipamento direto responde? Sem equipamento direto, o check não exige nenhum (só Push/Online).
builder.Services.AddHealthChecks().AddControlIdDevices(o =>
{
    o.RequireDevices = hasDirectDevices;
    o.CacheDuration = TimeSpan.FromSeconds(10);   // um /health consultado com frequência não sonda o equipamento a cada vez
});

// Limites dos callbacks: corpo máximo e, opcionalmente, só os equipamentos conhecidos (ControlId:KnownDevices: [935107, ...]).
var knownDevices = builder.Configuration.GetSection("ControlId:KnownDevices").Get<long[]>() ?? [];
builder.Services.Configure<ControlIdCallbackOptions>(o =>
{
    o.MaxRequestBodySize = ControlIdCallbackOptions.DefaultMaxRequestBodySize;   // 4 MiB; aumente se o Monitor mandar fotos grandes
    if (knownDevices.Length > 0)
    {
        var known = knownDevices.ToHashSet();
        o.IsKnownDevice = known.Contains;   // deviceId fora da lista: 403, sem tocar a fila nem os handlers
    }
});

// Rate limiting dos callbacks, por IP de origem: um equipamento consulta o /push a cada poucos segundos, então 300 por minuto
// sobra para ele e corta quem tenta adivinhar o token. Atrás de proxy reverso, o IP é o do proxy (use UseForwardedHeaders).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(CallbackRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Push: um comando sem resposta do equipamento em 30 s falha com TimeoutException e sai da fila; pendente com mais de
// 1 hora não é mais entregue (o equipamento ficou offline e a ordem perdeu o sentido).
builder.Services.AddControlIdPush(o =>
{
    o.CommandTimeout = TimeSpan.FromSeconds(30);
    o.CommandTtl = TimeSpan.FromHours(1);
});
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

app.UseRateLimiter();

// Health check: o status para todos; os dados de cada equipamento (série, IP, erros) só para quem chama desta máquina.
// Degraded vira 503 para um balanceador tirar a instância (o padrão do ASP.NET Core responde 200).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes = { [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable },
    ResponseWriter = (context, report) => IsLocal(context)
        ? context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), e.Value.Description, e.Value.Data }),
        })
        : context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() }),
});

// Callbacks dos equipamentos: Bearer obrigatório (sem ele, 401) e rate limiting. PreviousToken aceita o token antigo
// enquanto os equipamentos são atualizados.
app.MapControlIdPush()
    .RequireControlIdToken(o => { o.Token = callbackToken; o.PreviousToken = previousCallbackToken; })
    .RequireRateLimiting(CallbackRateLimit);
app.MapControlIdMonitor()
    .RequireControlIdToken(o => { o.Token = callbackToken; o.PreviousToken = previousCallbackToken; })
    .RequireRateLimiting(CallbackRateLimit);
app.MapControlIdOnline("/api")
    .RequireControlIdToken(o => { o.Token = callbackToken; o.PreviousToken = previousCallbackToken; })
    .RequireRateLimiting(CallbackRateLimit);

// API de operação, atrás de uma chave no cabeçalho X-Api-Key. ATENÇÃO: é só um exemplo para o sample não ficar aberto;
// em produção use autenticação de verdade (JWT/OIDC, certificado de cliente, AddAuthentication + RequireAuthorization).
var operacao = app.MapGroup("/").AddEndpointFilter(new ApiKeyFilter(apiKey));

// Comandos aos equipamentos via Push.
var equipamentos = operacao.MapGroup("/equipamentos/{deviceId:long}");

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

// Os mesmos comandos direto por HTTP, nos equipamentos configurados em ControlId:Access.
if (hasDirectDevices)
{
    var direto = operacao.MapGroup("/direto/{nome}");

    direto.MapGet("/usuarios", async (string nome, IControlIdClientFactory devices, CancellationToken ct) =>
        await RunAsync(ct, async token => Results.Ok(await devices.GetAccessClient(nome).GetUsersAsync(token))));

    direto.MapPost("/abrir-porta", async (string nome, IControlIdClientFactory devices, CancellationToken ct) =>
        await RunAsync(ct, async token =>
        {
            await devices.GetAccessClient(nome).OpenDoorAsync(1, token);
            return Results.Ok();
        }));
}

app.Run();
return 0;

// O comando Push só termina quando o equipamento busca e responde; o CommandTimeout (30 s, acima) corta a espera.
static async Task<IResult> RunAsync(CancellationToken requestAborted, Func<CancellationToken, Task<IResult>> action)
{
    try
    {
        return await action(requestAborted);
    }
    catch (TimeoutException)
    {
        return Results.Problem("O equipamento não buscou o comando a tempo (offline?).", statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (OperationCanceledException) when (!requestAborted.IsCancellationRequested)
    {
        return Results.Problem("O equipamento não respondeu a tempo.", statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (ControlIdException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}

// Segredo obrigatório, com pelo menos 16 caracteres. Nunca escreve o valor.
static string ReadSecret(IConfiguration configuration, string key, string environmentVariable, List<string> problems)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        problems.Add($"{key} não configurado: use dotnet user-secrets (ambiente Development) ou a variável de ambiente {environmentVariable}.");
        return string.Empty;
    }

    if (value.Length < 16)
    {
        problems.Add($"{key} curto demais: use pelo menos 16 caracteres aleatórios.");
    }

    return value;
}

static bool IsLocal(HttpContext context) =>
    context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote);

/// <summary>
/// Exige o cabeçalho <c>X-Api-Key</c> igual à chave configurada, comparando em tempo constante (os hashes têm sempre o
/// mesmo tamanho, então nem o comprimento da chave vaza). Exemplo simples: produção deve usar autenticação real.
/// </summary>
internal sealed class ApiKeyFilter(string apiKey) : IEndpointFilter
{
    private const string HeaderName = "X-Api-Key";
    private readonly byte[] _expected = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        if (supplied.Length == 0 || !CryptographicOperations.FixedTimeEquals(hash, _expected))
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                $"Informe a chave da API no cabeçalho {HeaderName}.", statusCode: StatusCodes.Status401Unauthorized));
        }

        return next(context);
    }
}
