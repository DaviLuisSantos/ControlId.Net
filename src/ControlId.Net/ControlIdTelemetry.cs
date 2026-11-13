namespace ControlId;

/// <summary>
/// Nomes da instrumentação do pacote, para registrar no OpenTelemetry:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(ControlIdTelemetry.ActivitySourceName))
///     .WithMetrics(m => m.AddMeter(ControlIdTelemetry.MeterName));
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Traces: um span <c>Client</c> por comando enviado ao equipamento (nome = comando, ex.: <c>load_objects</c>), que cobre a
/// espera na fila, o re-login e a requisição HTTP. O login aparece como span filho. A sessão e as credenciais nunca vão para
/// os atributos.
/// </para>
/// <para>
/// Métricas: <c>controlid.client.operation.duration</c> (s), <c>controlid.client.relogins</c>, <c>controlid.client.retries</c>,
/// <c>controlid.client.requests.queued</c> e <c>controlid.client.requests.active</c>, com <c>server.address</c> e
/// <c>server.port</c> identificando o equipamento.
/// </para>
/// </remarks>
public static class ControlIdTelemetry
{
    /// <summary>Nome do <see cref="System.Diagnostics.ActivitySource"/> (traces).</summary>
    public const string ActivitySourceName = "ControlId.Net";

    /// <summary>Nome do <see cref="System.Diagnostics.Metrics.Meter"/> (métricas).</summary>
    public const string MeterName = "ControlId.Net";

    /// <summary>
    /// Categoria dos logs do pacote (<see cref="ControlIdDeviceOptions.LoggerFactory"/>). Serve para filtrar o nível, por
    /// exemplo <c>"Logging:LogLevel:ControlId.Net": "Warning"</c>. Os logs nunca incluem sessão, login, senha nem o corpo
    /// das mensagens.
    /// </summary>
    public const string LoggerCategory = "ControlId.Net";
}
