// Dashboard em tempo real (Blazor Server) para um equipamento Control iD.
//
//   dotnet run --project tools/ControlId.Net.Simulator -- --urls http://127.0.0.1:8080
//   dotnet user-secrets set --project samples/ControlId.Net.Dashboard Dashboard:DevicePassword admin   (uma vez; o simulador usa admin)
//   dotnet run --project samples/ControlId.Net.Dashboard -- --urls http://127.0.0.1:5080
//   (abra http://localhost:5080)
//
// A senha do equipamento não tem padrão nem fica no appsettings.json: use user-secrets (ambiente Development, o do
// launchSettings.json) ou a variável de ambiente Dashboard__DevicePassword. Sem ela, o dashboard não sobe.
//
// Ao iniciar, o dashboard grava no equipamento (Dashboard:DeviceAddress) o token dos callbacks (Dashboard:CallbackToken;
// vazio = um aleatório por execução), aponta o Monitor para si mesmo (Dashboard:MonitorHostname:MonitorPort), recebe as
// notificações em /api/notifications (só com Authorization: Bearer) e atualiza a página ao vivo. GET /health consulta o
// equipamento. A página não tem login: escute só onde o equipamento precisa alcançar e numa rede que você controla.
// Tudo com Docker: docker compose -f samples/docker-compose.yml up --build   (dashboard em http://localhost:8081)
using System.Net;
using System.Security.Cryptography;
using ControlId;
using ControlId.AspNetCore;
using ControlId.Dashboard;
using ControlId.Dashboard.Components;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Usado só se Dashboard:CallbackToken estiver vazio (fixo durante a execução; o equipamento recebe o novo a cada partida).
var generatedCallbackToken = RandomNumberGenerator.GetHexString(48, lowercase: true);

builder.Services.AddOptions<DashboardOptions>()
    .Bind(builder.Configuration.GetSection(DashboardOptions.SectionName))
    .PostConfigure(o =>
    {
        if (string.IsNullOrEmpty(o.CallbackToken))
        {
            o.CallbackToken = generatedCallbackToken;
        }
    })
    .Validate(
        o => Uri.TryCreate(o.DeviceAddress, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "Dashboard:DeviceAddress precisa ser um endereço http(s) absoluto, ex.: http://192.168.0.129.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.DeviceLogin), "Dashboard:DeviceLogin não pode ser vazio.")
    .Validate(
        o => !string.IsNullOrEmpty(o.DevicePassword),
        "Dashboard:DevicePassword não configurada: defina a variável de ambiente Dashboard__DevicePassword ou use " +
        "'dotnet user-secrets set --project samples/ControlId.Net.Dashboard Dashboard:DevicePassword <senha>' (o simulador usa admin).")
    .Validate(
        o => o.CallbackToken.Length >= DashboardOptions.MinCallbackTokenLength,
        $"Dashboard:CallbackToken curto demais: use pelo menos {DashboardOptions.MinCallbackTokenLength} caracteres (ou deixe vazio para gerar um por execução).")
    .Validate(o => o.MonitorPort is > 0 and <= 65535, "Dashboard:MonitorPort precisa estar entre 1 e 65535.")
    .Validate(
        o => o.MaxEvents > 0 && o.PollSeconds > 0 && o.DoorFallbackSeconds > 0,
        "Dashboard:MaxEvents, Dashboard:PollSeconds e Dashboard:DoorFallbackSeconds precisam ser maiores que zero.")
    .ValidateOnStart();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// O equipamento (ou o simulador), configurado a partir das opções já validadas, e o cliente HTTP da rota /simulator/identify.
builder.Services.AddControlIdAccess(DashboardOptions.DeviceName, _ => { });
builder.Services.AddOptions<ControlIdDeviceOptions>(DashboardOptions.DeviceName)
    .Configure<IOptions<DashboardOptions>>((device, dashboard) =>
    {
        var settings = dashboard.Value;
        device.BaseAddress = new Uri(settings.DeviceAddress);
        device.Login = settings.DeviceLogin;
        device.Password = settings.DevicePassword;
        device.Timeout = TimeSpan.FromSeconds(5);
    });
builder.Services.AddHttpClient(nameof(DeviceService), (services, client) =>
{
    client.BaseAddress = new Uri(services.GetRequiredService<IOptions<DashboardOptions>>().Value.DeviceAddress);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// GET /health: um check por equipamento (controlid:access:equipamento), com série, firmware, latência e último contato.
builder.Services.AddHealthChecks().AddControlIdDeviceChecks(o =>
{
    o.Timeout = TimeSpan.FromSeconds(3);
    o.CacheDuration = TimeSpan.FromSeconds(5);   // a página de status pode consultar à vontade
    // Com um equipamento real que notifica o Monitor periodicamente, sem contato há mais de um minuto vira Degraded.
    // O simulador só notifica quando algo acontece, então aqui fica desligado:
    // o.MaxContactAge = TimeSpan.FromMinutes(1);
});

builder.Services.AddSingleton<DashboardState>();
builder.Services.AddSingleton<DeviceService>();
builder.Services.AddHostedService<DeviceWorker>();
builder.Services.AddControlIdMonitor<DashboardMonitorHandler>();

var app = builder.Build();

// Configuração inválida (ex.: senha faltando): mensagem clara e código de saída, sem pilha de exceção.
DashboardOptions dashboardSettings;
try
{
    dashboardSettings = app.Services.GetRequiredService<IOptions<DashboardOptions>>().Value;
}
catch (OptionsValidationException ex)
{
    Console.Error.WriteLine("O dashboard não subiu: configuração inválida.");
    foreach (var failure in ex.Failures)
    {
        Console.Error.WriteLine("  - " + failure);
    }

    return 1;
}

if (dashboardSettings.CallbackToken == generatedCallbackToken)
{
    app.Logger.LogInformation("Dashboard:CallbackToken vazio: gerado um token aleatório para esta execução (gravado no equipamento na partida).");
}

app.UseAntiforgery();

app.MapStaticAssets();   // dashboard.css e o _framework/blazor.web.js (fora do Development, só por aqui)
app.MapControlIdMonitor().RequireControlIdToken(dashboardSettings.CallbackToken);
app.MapHealthChecks("/health", new HealthCheckOptions
{
    // O status para todos; os dados do equipamento (série, IP, erros) só para quem chama desta máquina.
    ResponseWriter = (context, report) => context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote)
        ? context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), e.Value.Description, e.Value.Data }),
        })
        : context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() }),
});
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
return 0;
