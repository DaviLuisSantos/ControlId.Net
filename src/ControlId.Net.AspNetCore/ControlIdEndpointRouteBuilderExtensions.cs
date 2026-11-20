using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using ControlId;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using ControlId.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Rotas que os equipamentos Control iD chamam no modo Push e no Monitor.</summary>
public static class ControlIdEndpointRouteBuilderExtensions
{
    private const string LoggerCategory = "ControlId.AspNetCore";

    /// <summary>
    /// Mapeia <c>GET {prefix}/push</c> e <c>POST {prefix}/result</c> para o <see cref="PushServer"/> registrado com
    /// <c>services.AddControlIdPush()</c>. No equipamento, configure <c>push_remote_address</c> apontando para esta aplicação
    /// (o firmware chama <c>/push</c> na raiz; use <paramref name="prefix"/> só atrás de um proxy que reescreva o caminho).
    /// <para>
    /// As rotas são fechadas por padrão (401): encadeie <c>RequireControlIdToken</c> ou, só em rede isolada,
    /// <c>AllowAnonymousControlIdCallbacks()</c>. As respostas levam <c>Cache-Control: no-store</c>.
    /// </para>
    /// </summary>
    /// <returns>O grupo das rotas, para encadear <c>RequireHost</c>, <c>RequireControlIdToken</c>, filtros etc.</returns>
    /// <exception cref="InvalidOperationException">O <see cref="PushServer"/> não está registrado (falta <c>services.AddControlIdPush()</c>).</exception>
    public static RouteGroupBuilder MapControlIdPush(this IEndpointRouteBuilder endpoints, string prefix = "/")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        CallbackGuard.EnsureRegistered(
            endpoints, typeof(PushServer), "PushServer não registrado: chame services.AddControlIdPush() antes de MapControlIdPush().");

        var group = endpoints.MapGroup(prefix);
        group.MapMethods("push", new[] { HttpMethods.Get, HttpMethods.Post }, CallbackTelemetry.Measure(CallbackTelemetry.Push, HandlePushAsync));
        group.MapPost("result", CallbackTelemetry.Measure(CallbackTelemetry.Result, HandleResultAsync));
        CallbackGuard.AddRouteState(group);
        return group;
    }

    /// <summary>
    /// Mapeia <c>POST {path}/{endpoint}</c> e repassa cada notificação aos <see cref="IMonitorNotificationHandler"/>
    /// registrados com <c>services.AddControlIdMonitor(...)</c>. O <paramref name="path"/> deve ser o mesmo configurado
    /// no equipamento (padrão do firmware: <c>api/notifications</c>).
    /// <para>
    /// Se o caminho terminar em <c>notifications</c>, o singular (<c>notification</c>) também é aceito: o SDK oficial
    /// registra que o firmware posta <c>device_is_alive</c> em <c>api/notification</c>.
    /// </para>
    /// <para>
    /// As rotas são fechadas por padrão (401): encadeie <c>RequireControlIdToken</c> ou, só em rede isolada,
    /// <c>AllowAnonymousControlIdCallbacks()</c>. Todos os handlers rodam, mesmo que um falhe; se algum falhar, a resposta é 500.
    /// </para>
    /// </summary>
    /// <returns>O grupo das rotas (as duas grafias), para encadear <c>RequireHost</c>, <c>RequireControlIdToken</c>, filtros etc.</returns>
    public static RouteGroupBuilder MapControlIdMonitor(this IEndpointRouteBuilder endpoints, string path = "api/notifications")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var trimmed = (path ?? throw new ArgumentNullException(nameof(path))).Trim('/');
        var group = endpoints.MapGroup("/");
        var handler = CallbackTelemetry.Measure(CallbackTelemetry.Monitor, HandleMonitorAsync);
        group.MapPost(trimmed + "/{endpoint}", handler);
        if (trimmed.EndsWith("notifications", StringComparison.Ordinal))
        {
            group.MapPost(trimmed.Substring(0, trimmed.Length - 1) + "/{endpoint}", handler);
        }

        CallbackGuard.AddRouteState(group);
        return group;
    }

    private static async Task HandlePushAsync(HttpContext context)
    {
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        var hasDeviceId = TryGetDeviceId(context, out var deviceId);
        if (!CallbackGuard.Authorize(context, hasDeviceId ? deviceId : null, LoggerCategory))
        {
            return;
        }

        if (!hasDeviceId)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        CallbackGuard.RecordContact(context, deviceId, DeviceContactSource.Push);
        var push = GetPushServer(context);
        var uuid = GetUuid(context);
        var body = await CallbackTelemetry.MeasureHandlerAsync(
            CallbackTelemetry.Push, () => push.HandlePushAsync(deviceId, uuid, context.RequestAborted)).ConfigureAwait(false);
        context.Response.StatusCode = StatusCodes.Status200OK;
        if (body.Length > 0)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, System.Text.Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static async Task HandleResultAsync(HttpContext context)
    {
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        var hasDeviceId = TryGetDeviceId(context, out var deviceId);

        // O deviceId vem da query: mesmo com token por equipamento, autoriza antes de ler o corpo.
        if (!CallbackGuard.Authorize(context, hasDeviceId ? deviceId : null, LoggerCategory))
        {
            return;
        }

        if (!hasDeviceId)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        var bytes = await CallbackGuard.ReadBodyAsync(context, LoggerCategory).ConfigureAwait(false);
        if (bytes is null)
        {
            return;
        }

        CallbackGuard.RecordContact(context, deviceId, DeviceContactSource.Push);
        var push = GetPushServer(context);
        var uuid = GetUuid(context);
        var body = CallbackBody.ToText(bytes);
        var handled = await CallbackTelemetry.MeasureHandlerAsync(
            CallbackTelemetry.Result, () => push.HandleResultAsync(deviceId, uuid, body, context.RequestAborted)).ConfigureAwait(false);
        if (!handled)
        {
            Log.PushResultIgnored(GetLogger(context), deviceId);
        }

        // O equipamento espera resposta vazia, mesmo quando o resultado é descartado.
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleMonitorAsync(HttpContext context)
    {
        var endpoint = context.Request.RouteValues["endpoint"] as string ?? string.Empty;

        // Token único ou rota anônima/fechada: decide antes de ler o corpo. Token por equipamento: o deviceId está no corpo.
        var authorizeFirst = !CallbackGuard.NeedsBodyToAuthorize(context);
        if (authorizeFirst && !CallbackGuard.Authorize(context, null, LoggerCategory))
        {
            return;
        }

        var bytes = await CallbackGuard.ReadBodyAsync(context, LoggerCategory).ConfigureAwait(false);
        if (bytes is null)
        {
            return;
        }

        MonitorNotification notification;
        try
        {
            notification = MonitorNotifications.Parse(endpoint, CallbackBody.ToText(bytes));
        }
        catch (ControlIdException ex)
        {
            // Sem o deviceId da notificação, só vale o token único; o corpo inválido nunca vira 200.
            if (authorizeFirst || CallbackGuard.Authorize(context, null, LoggerCategory))
            {
                Log.InvalidMonitorNotification(GetLogger(context), ex, endpoint);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return;
        }

        if (!authorizeFirst && !CallbackGuard.Authorize(context, notification.DeviceId, LoggerCategory))
        {
            return;
        }

        if (!CallbackGuard.IsKnownDevice(context, notification.DeviceId, LoggerCategory))
        {
            return;
        }

        if (notification.DeviceId is { } monitored)
        {
            CallbackGuard.RecordContact(context, monitored, DeviceContactSource.Monitor);
        }

        // Cada handler isolado: a falha de um não impede os outros; se algum falhou, 500 no fim.
        var failed = false;
        foreach (var handler in context.RequestServices.GetServices<IMonitorNotificationHandler>())
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                await handler.HandleAsync(notification, context.RequestAborted).ConfigureAwait(false);
                CallbackTelemetry.RecordHandler(CallbackTelemetry.Monitor, start, null);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                CallbackTelemetry.RecordHandler(CallbackTelemetry.Monitor, start, ex);
                Log.MonitorHandlerFailed(GetLogger(context), ex, handler.GetType().FullName ?? handler.GetType().Name, endpoint);
                failed = true;
            }
        }

        context.Response.StatusCode = failed ? StatusCodes.Status500InternalServerError : StatusCodes.Status200OK;
    }

    private static bool TryGetDeviceId(HttpContext context, out long deviceId) =>
        long.TryParse(context.Request.Query["deviceId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out deviceId);

    private static string? GetUuid(HttpContext context)
    {
        var uuid = context.Request.Query["uuid"].ToString();
        return uuid.Length > 0 ? uuid : null;
    }

    private static PushServer GetPushServer(HttpContext context) =>
        context.RequestServices.GetService<PushServer>()
        ?? throw new InvalidOperationException("PushServer não registrado. Chame services.AddControlIdPush().");

    private static ILogger GetLogger(HttpContext context) => CallbackGuard.GetLogger(context, LoggerCategory);
}

/// <summary>Segurança, limites e último contato compartilhados pelas rotas de Push, Monitor e Online.</summary>
internal static class CallbackGuard
{
    /// <summary>Falha no mapeamento quando o serviço que as rotas usam não está registrado.</summary>
    public static void EnsureRegistered(IEndpointRouteBuilder endpoints, Type serviceType, string message)
    {
        var isService = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();
        if (isService is not null && !isService.IsService(serviceType))
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Uma instância de <see cref="CallbackRouteState"/> por rota do grupo (aviso 2006 uma vez por rota).</summary>
    public static void AddRouteState(IEndpointConventionBuilder group) =>
        group.Add(endpoint => endpoint.Metadata.Add(new CallbackRouteState()));

    /// <summary>
    /// <see langword="true"/> quando a autorização depende do <c>deviceId</c> que pode estar no corpo (token por equipamento).
    /// Nos demais casos (token único, rota anônima ou fechada) a rota decide antes de ler o corpo.
    /// </summary>
    public static bool NeedsBodyToAuthorize(HttpContext context) =>
        ControlIdTokenValidator.GetSecurity(context, out _) == CallbackSecurity.DeviceToken;

    /// <summary>
    /// Aplica a proteção da rota. Em falha, a resposta já está em 401 com <c>WWW-Authenticate: Bearer</c>; rota sem token nem
    /// <c>AllowAnonymousControlIdCallbacks</c> loga 2006 (uma vez por rota), token ausente ou inválido loga 2005
    /// (rota e equipamento; nunca o token).
    /// </summary>
    public static bool Authorize(HttpContext context, long? deviceId, string loggerCategory)
    {
        var security = ControlIdTokenValidator.GetSecurity(context, out var options);
        switch (security)
        {
            case CallbackSecurity.Anonymous:
                return true;

            case CallbackSecurity.Closed:
                ControlIdTokenValidator.Challenge(context);
                var state = context.GetEndpoint()?.Metadata.GetMetadata<CallbackRouteState>();
                if (state is null || state.TryMarkClosedWarned())
                {
                    Log.CallbackClosed(GetLogger(context, loggerCategory), Route(context));
                }

                return false;

            default:
                if (ControlIdTokenValidator.IsValid(context, options!, deviceId))
                {
                    return true;
                }

                ControlIdTokenValidator.Challenge(context);
                Log.CallbackUnauthorized(GetLogger(context, loggerCategory), context.Request.Path.Value ?? string.Empty, deviceId);
                return false;
        }
    }

    /// <summary>
    /// Confere <see cref="ControlIdCallbackOptions.IsKnownDevice"/>. Equipamento recusado: 403 e log 2009. Sem
    /// <paramref name="deviceId"/> ou sem a função, aceita.
    /// </summary>
    public static bool IsKnownDevice(HttpContext context, long? deviceId, string loggerCategory)
    {
        if (deviceId is not { } id || GetOptions(context).IsKnownDevice is not { } isKnown || isKnown(id))
        {
            return true;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        Log.CallbackUnknownDevice(GetLogger(context, loggerCategory), Route(context), id);
        return false;
    }

    /// <summary>
    /// Lê o corpo até <see cref="ControlIdCallbackOptions.MaxRequestBodySize"/>. Acima do limite: 413, log 2010 e
    /// <see langword="null"/>.
    /// </summary>
    public static async Task<byte[]?> ReadBodyAsync(HttpContext context, string loggerCategory)
    {
        var limit = GetOptions(context).MaxRequestBodySize;
        var body = await CallbackBody.ReadAsync(context, limit).ConfigureAwait(false);
        if (body is null)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            Log.CallbackBodyTooLarge(GetLogger(context, loggerCategory), Route(context), limit);
        }

        return body;
    }

    /// <summary>
    /// <c>device_id</c> da query e do corpo precisam coincidir quando os dois existem. Divergência: 403 e log 2007.
    /// </summary>
    public static bool SameDevice(HttpContext context, long? queryDeviceId, long? bodyDeviceId, string loggerCategory)
    {
        if (queryDeviceId is not { } query || bodyDeviceId is not { } body || query == body)
        {
            return true;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        Log.CallbackDeviceMismatch(GetLogger(context, loggerCategory), Route(context), query, body);
        return false;
    }

    /// <summary>Registra o contato no <see cref="DeviceContactTracker"/>, quando registrado no container.</summary>
    public static void RecordContact(HttpContext context, long deviceId, DeviceContactSource source) =>
        context.RequestServices.GetService<DeviceContactTracker>()?.Record(deviceId, source);

    public static ILogger GetLogger(HttpContext context, string loggerCategory) =>
        context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(loggerCategory) ?? NullLogger.Instance;

    private static ControlIdCallbackOptions GetOptions(HttpContext context) =>
        context.RequestServices.GetService<IOptions<ControlIdCallbackOptions>>()?.Value ?? new ControlIdCallbackOptions();

    private static string Route(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? string.Empty;
}
