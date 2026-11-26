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
/// <c>controlid.client.requests.queued</c>, <c>controlid.client.requests.active</c>,
/// <c>controlid.client.request.wait.duration</c> (s, espera por uma vaga do limite por equipamento),
/// <c>controlid.client.circuit.transitions</c> (tag <c>controlid.circuit.state</c>: <c>open</c>, <c>half_open</c> ou
/// <c>closed</c>) e <c>controlid.client.circuit.rejected</c>, com <c>server.address</c> e <c>server.port</c> identificando o
/// equipamento. Uma requisição recusada pelo circuit breaker aberto termina o span com <c>error.type=circuit_open</c>.
/// </para>
/// <para>
/// Push: <c>controlid.push.commands</c>, um por envio do <c>PushServer</c>, com a tag <c>controlid.push.outcome</c>
/// (<c>enqueued</c>, <c>delivered</c>, <c>completed</c>, <c>orphaned</c>, <c>expired</c>, <c>cancelled</c>, <c>rejected</c> ou
/// <c>timeout</c>).
/// </para>
/// <para>
/// Rotas do pacote <c>ControlId.Net.AspNetCore</c> (no mesmo <c>Meter</c>): <c>controlid.server.callback.requests</c>, com as
/// tags <c>controlid.callback.kind</c> (<c>push</c>, <c>result</c>, <c>monitor</c> ou <c>online</c>) e
/// <c>controlid.callback.outcome</c> (<c>ok</c>, <c>unauthorized</c>, <c>forbidden</c>, <c>too_large</c>, <c>bad_request</c> ou
/// <c>error</c>), e <c>controlid.server.callback.handler.duration</c> (s, código da aplicação chamado pelo callback, com
/// <c>controlid.callback.kind</c> e, em caso de exceção, <c>error.type</c>). O <c>deviceId</c> não vai para as tags.
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
