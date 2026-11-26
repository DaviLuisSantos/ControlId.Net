using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ControlId.AspNetCore.Internal;

/// <summary>
/// Métricas dos callbacks recebidos (telemetria de entrada), no mesmo <c>Meter</c> do núcleo
/// (<see cref="ControlIdTelemetry.MeterName"/>). Sem <c>deviceId</c> nas tags (cardinalidade) nem corpo.
/// </summary>
internal static class CallbackTelemetry
{
    public const string KindTag = "controlid.callback.kind";
    public const string OutcomeTag = "controlid.callback.outcome";

    public const string Push = "push";
    public const string Result = "result";
    public const string Monitor = "monitor";
    public const string Online = "online";

    private static readonly Meter Meter = new(
        ControlIdTelemetry.MeterName, typeof(CallbackTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0");

    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "controlid.server.callback.requests",
        "{request}",
        "Callbacks recebidos dos equipamentos (tags controlid.callback.kind e controlid.callback.outcome).");

    public static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
        "controlid.server.callback.handler.duration",
        "s",
        "Duração do código da aplicação chamado por um callback (PushServer, handlers do Monitor e do modo online).");

    public const string FallbackReasonTag = "controlid.online.fallback.reason";

    public const string FallbackException = "exception";
    public const string FallbackNull = "null";
    public const string FallbackTimeout = "timeout";

    public static readonly Counter<long> OnlineFallbacks = Meter.CreateCounter<long>(
        "controlid.server.online.fallbacks",
        "{response}",
        "Respostas de contingência do modo online enviadas no lugar da decisão do handler (tag controlid.online.fallback.reason: exception, null ou timeout).");

    /// <summary>Conta uma resposta de contingência do modo online.</summary>
    public static void RecordOnlineFallback(string reason) =>
        OnlineFallbacks.Add(1, new TagList { { FallbackReasonTag, reason } });

    /// <summary>Envolve a rota: conta a requisição com o resultado deduzido do status (exceção = <c>error</c>).</summary>
    public static RequestDelegate Measure(string kind, RequestDelegate handler) => async context =>
    {
        try
        {
            await handler(context).ConfigureAwait(false);
        }
        catch
        {
            Count(kind, "error");
            throw;
        }

        Count(kind, Outcome(context.Response.StatusCode));
    };

    /// <summary>Registra a duração de um handler iniciado em <paramref name="startTimestamp"/> (<see cref="Stopwatch.GetTimestamp"/>).</summary>
    public static void RecordHandler(string kind, long startTimestamp, Exception? error)
    {
        var seconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        if (error is null)
        {
            HandlerDuration.Record(seconds, new TagList { { KindTag, kind } });
        }
        else
        {
            HandlerDuration.Record(seconds, new TagList { { KindTag, kind }, { "error.type", error.GetType().FullName } });
        }
    }

    /// <summary>Executa e mede um handler.</summary>
    public static async Task<T> MeasureHandlerAsync<T>(string kind, Func<Task<T>> handler)
    {
        var start = Stopwatch.GetTimestamp();
        T result;
        try
        {
            result = await handler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordHandler(kind, start, ex);
            throw;
        }

        RecordHandler(kind, start, null);
        return result;
    }

    /// <summary>Executa e mede um handler sem retorno.</summary>
    public static Task MeasureHandlerAsync(string kind, Func<Task> handler) =>
        MeasureHandlerAsync(kind, async () =>
        {
            await handler().ConfigureAwait(false);
            return true;
        });

    private static void Count(string kind, string outcome) =>
        Requests.Add(1, new TagList { { KindTag, kind }, { OutcomeTag, outcome } });

    private static string Outcome(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "bad_request",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status413PayloadTooLarge => "too_large",
        StatusCodes.Status404NotFound => "ok", // user_get_image sem foto: resposta prevista
        < 400 => "ok",
        _ => "error",
    };
}
