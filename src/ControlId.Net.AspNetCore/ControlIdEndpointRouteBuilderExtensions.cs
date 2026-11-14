using System;
using System.Globalization;
using System.IO;
using System.Text;
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

namespace Microsoft.AspNetCore.Builder;

/// <summary>Rotas que os equipamentos Control iD chamam no modo Push e no Monitor.</summary>
public static class ControlIdEndpointRouteBuilderExtensions
{
    private const string LoggerCategory = "ControlId.AspNetCore";

    /// <summary>
    /// Mapeia <c>GET {prefix}/push</c> e <c>POST {prefix}/result</c> para o <see cref="PushServer"/> registrado com
    /// <c>services.AddControlIdPush()</c>. No equipamento, configure <c>push_remote_address</c> apontando para esta aplicação
    /// (o firmware chama <c>/push</c> na raiz; use <paramref name="prefix"/> só atrás de um proxy que reescreva o caminho).
    /// </summary>
    /// <returns>O grupo das rotas, para encadear <c>RequireHost</c>, <c>RequireControlIdToken</c>, filtros etc.</returns>
    public static RouteGroupBuilder MapControlIdPush(this IEndpointRouteBuilder endpoints, string prefix = "/")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup(prefix);
        group.MapMethods("push", new[] { HttpMethods.Get, HttpMethods.Post }, HandlePushAsync);
        group.MapPost("result", HandleResultAsync);
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
        group.MapPost(trimmed + "/{endpoint}", HandleMonitorAsync);
        if (trimmed.EndsWith("notifications", StringComparison.Ordinal))
        {
            group.MapPost(trimmed.Substring(0, trimmed.Length - 1) + "/{endpoint}", HandleMonitorAsync);
        }

        return group;
    }

    private static async Task HandlePushAsync(HttpContext context)
    {
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

        CallbackGuard.RecordContact(context, deviceId, DeviceContactSource.Push);
        var body = await GetPushServer(context).HandlePushAsync(deviceId, GetUuid(context), context.RequestAborted).ConfigureAwait(false);
        context.Response.StatusCode = StatusCodes.Status200OK;
        if (body.Length > 0)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static async Task HandleResultAsync(HttpContext context)
    {
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

        CallbackGuard.RecordContact(context, deviceId, DeviceContactSource.Push);
        var body = await ReadBodyAsync(context).ConfigureAwait(false);
        if (!await GetPushServer(context).HandleResultAsync(deviceId, GetUuid(context), body, context.RequestAborted).ConfigureAwait(false))
        {
            Log.PushResultIgnored(GetLogger(context), deviceId);
        }

        // O equipamento espera resposta vazia, mesmo quando o resultado é descartado.
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleMonitorAsync(HttpContext context)
    {
        var endpoint = context.Request.RouteValues["endpoint"] as string ?? string.Empty;
        var body = await ReadBodyAsync(context).ConfigureAwait(false);

        MonitorNotification notification;
        try
        {
            notification = MonitorNotifications.Parse(endpoint, body);
        }
        catch (ControlIdException ex)
        {
            // Sem o deviceId da notificação, só vale o token único; o corpo inválido nunca vira 200.
            if (CallbackGuard.Authorize(context, null, LoggerCategory))
            {
                Log.InvalidMonitorNotification(GetLogger(context), ex, endpoint);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return;
        }

        if (!CallbackGuard.Authorize(context, notification.DeviceId, LoggerCategory))
        {
            return;
        }

        if (notification.DeviceId is { } monitored)
        {
            CallbackGuard.RecordContact(context, monitored, DeviceContactSource.Monitor);
        }

        foreach (var handler in context.RequestServices.GetServices<IMonitorNotificationHandler>())
        {
            await handler.HandleAsync(notification, context.RequestAborted).ConfigureAwait(false);
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
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

    private static ILogger GetLogger(HttpContext context) =>
        context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(LoggerCategory) ?? NullLogger.Instance;

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
    }
}

/// <summary>Token e último contato compartilhados pelas rotas de Push, Monitor e Online.</summary>
internal static class CallbackGuard
{
    /// <summary>
    /// Aplica o token exigido pelo grupo (se houver). Em falha, a resposta já está em 401, com o aviso 2005 no log
    /// (rota e equipamento; nunca o token).
    /// </summary>
    public static bool Authorize(HttpContext context, long? deviceId, string loggerCategory)
    {
        if (ControlIdTokenValidator.Authorize(context, deviceId))
        {
            return true;
        }

        var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(loggerCategory) ?? NullLogger.Instance;
        Log.CallbackUnauthorized(logger, context.Request.Path.Value ?? string.Empty, deviceId);
        return false;
    }

    /// <summary>Registra o contato no <see cref="DeviceContactTracker"/>, quando registrado no container.</summary>
    public static void RecordContact(HttpContext context, long deviceId, DeviceContactSource source) =>
        context.RequestServices.GetService<DeviceContactTracker>()?.Record(deviceId, source);
}
