// Dashboard em tempo real (Blazor Server) para um equipamento Control iD.
//
//   dotnet run --project tools/ControlId.Net.Simulator -- --urls http://127.0.0.1:8080
//   dotnet run --project samples/ControlId.Net.Dashboard -- --urls http://127.0.0.1:5080
//   (abra http://localhost:5080)
//
// Ao iniciar, o dashboard aponta o Monitor do equipamento (Dashboard:DeviceAddress) para si mesmo
// (Dashboard:MonitorHostname:MonitorPort), recebe as notificações em /api/notifications e atualiza a página ao vivo.
// Tudo com Docker: docker compose -f samples/docker-compose.yml up --build   (dashboard em http://localhost:8081)
using ControlId.AspNetCore;
using ControlId.Dashboard;
using ControlId.Dashboard.Components;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection("Dashboard").Get<DashboardOptions>() ?? new DashboardOptions();
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// O equipamento (ou o simulador) e o cliente HTTP da rota /simulator/identify.
builder.Services.AddControlIdAccess(DashboardOptions.DeviceName, options =>
{
    options.BaseAddress = new Uri(settings.DeviceAddress);
    options.Login = settings.DeviceLogin;
    options.Password = settings.DevicePassword;
    options.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient(nameof(DeviceService), client =>
{
    client.BaseAddress = new Uri(settings.DeviceAddress);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<DashboardState>();
builder.Services.AddSingleton<DeviceService>();
builder.Services.AddHostedService<DeviceWorker>();
builder.Services.AddControlIdMonitor<DashboardMonitorHandler>();

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();   // dashboard.css e o _framework/blazor.web.js (fora do Development, só por aqui)
app.MapControlIdMonitor();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
