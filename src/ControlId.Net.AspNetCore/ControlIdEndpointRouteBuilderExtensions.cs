using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ControlId;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
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
    /// <returns>O grupo das rotas, para encadear <c>RequireHost</c>, filtros etc.</returns>
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
    /// </summary>
    /// <returns>O grupo das rotas, para encadear <c>RequireHost</c>, filtros etc.</returns>
    public static RouteGroupBuilder MapControlIdMonitor(this IEndpointRouteBuilder endpoints, string path = "api/notifications")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup("/" + path.Trim('/'));
        group.MapPost("{endpoint}", HandleMonitorAsync);
        return group;
    }

    private static async Task HandlePushAsync(HttpContext context)
    {
        if (!TryGetDeviceId(context, out var deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var body = GetPushServer(context).HandlePush(deviceId, GetUuid(context));
        context.Response.StatusCode = StatusCodes.Status200OK;
        if (body.Length > 0)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static async Task HandleResultAsync(HttpContext context)
    {
        if (!TryGetDeviceId(context, out var deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var body = await ReadBodyAsync(context).ConfigureAwait(false);
        if (!GetPushServer(context).HandleResult(deviceId, GetUuid(context), body))
        {
            GetLogger(context).LogWarning("Resultado de Push do equipamento {DeviceId} sem comando pendente; ignorado.", deviceId);
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
            GetLogger(context).LogWarning(ex, "Notificação '{Endpoint}' do Monitor inválida.", endpoint);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
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
