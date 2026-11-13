namespace ControlId.Simulator;

/// <summary>Monta o simulador. Separado do <c>Program</c> para os testes de integração subirem instâncias próprias.</summary>
public static class SimulatorApp
{
    /// <summary>
    /// Cria a aplicação com os argumentos de linha de comando (<c>--urls</c>, <c>--Simulator:Password</c>...) e, opcionalmente,
    /// ajusta as opções em código.
    /// </summary>
    public static WebApplication Build(string[] args, Action<SimulatorOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection("Simulator"));
        if (configure is not null)
        {
            builder.Services.PostConfigure(configure);
        }

        // O tipo decide quais serviços e rotas existem; por isso as opções são lidas já aqui.
        var options = new SimulatorOptions();
        builder.Configuration.GetSection("Simulator").Bind(options);
        configure?.Invoke(options);
        var rep = options.IsRep();

        if (rep)
        {
            builder.Services.AddSingleton<RepState>();
        }
        else
        {
            builder.Services.AddSingleton<DeviceState>();
            builder.Services.AddSingleton<MonitorDispatcher>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<MonitorDispatcher>());
            builder.Services.AddHttpClient(nameof(MonitorDispatcher), c => c.Timeout = TimeSpan.FromSeconds(5));
        }

        builder.Services.AddHealthChecks();

        var app = builder.Build();

        if (rep)
        {
            app.MapRepApi();
        }
        else
        {
            app.MapAccessApi();
            app.MapSimulatorControl();
        }

        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Redirect("/simulator/state"));

        return app;
    }
}
