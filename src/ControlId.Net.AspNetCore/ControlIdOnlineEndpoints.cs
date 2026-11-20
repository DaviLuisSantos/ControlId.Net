using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Access.Online;
using ControlId.AspNetCore;
using ControlId.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Rotas que o equipamento chama no modo online (Pro/Enterprise).</summary>
public static class ControlIdOnlineEndpointRouteBuilderExtensions
{
    private const string LoggerCategory = "ControlId.AspNetCore.Online";

    /// <summary>
    /// Mapeia as rotas do modo online em <paramref name="prefix"/>, que deve corresponder ao caminho do
    /// <see cref="OnlineModeConfiguration.ServerAddress"/> (ex.: <c>"/api"</c> para <c>http://servidor:5000/api</c>):
    /// <list type="bullet">
    /// <item>eventos de identificação (<c>new_user_identified</c>, <c>new_card</c>, <c>new_qrcode</c>, <c>new_uhf_tag</c>,
    /// <c>new_user_id_and_password</c>, <c>new_biometric_image</c>, <c>new_biometric_template</c>, <c>new_pin</c>,
    /// <c>new_face_template</c>) → <see cref="IOnlineAccessHandler.AuthorizeAsync"/>;</item>
    /// <item><c>new_rex_log</c> → <see cref="IOnlineAccessHandler.OnExitButtonAsync"/>;</item>
    /// <item>resultados do cadastro remoto (<c>card_create</c>, <c>face_create</c>, <c>fingerprint_create</c>,
    /// <c>template_create</c>, <c>pin_create</c>, <c>password_create</c>, <c>face_template_create</c>, <c>tag_create</c>) →
    /// <see cref="IOnlineAccessHandler.OnRemoteEnrollAsync"/>;</item>
    /// <item><c>new_user_enrolled</c> (autocadastro facial no equipamento) → <see cref="IOnlineAccessHandler.OnKioskEnrollmentAsync"/>;</item>
    /// <item><c>GET user_get_image</c> → <see cref="IOnlineAccessHandler.GetUserImageAsync"/>;</item>
    /// <item><c>device_is_alive</c> e <c>session_is_valid</c> → 200, para o equipamento sair da contingência.</item>
    /// </list>
    /// As rotas são fechadas por padrão (401): encadeie <c>RequireControlIdToken</c> no grupo devolvido ou, só em rede isolada,
    /// <c>AllowAnonymousControlIdCallbacks()</c>. No cadastro remoto e no <c>new_rex_log</c>, o <c>device_id</c> da query e o do
    /// corpo precisam coincidir (senão, 403).
    /// </summary>
    /// <exception cref="InvalidOperationException">Nenhum <see cref="IOnlineAccessHandler"/> registrado (falta <c>services.AddControlIdOnline(...)</c>).</exception>
    public static RouteGroupBuilder MapControlIdOnline(this IEndpointRouteBuilder endpoints, string prefix = "/")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        CallbackGuard.EnsureRegistered(
            endpoints,
            typeof(IOnlineAccessHandler),
            "Nenhum IOnlineAccessHandler registrado: chame services.AddControlIdOnline(...) antes de MapControlIdOnline().");

        var group = endpoints.MapGroup("/" + prefix.Trim('/'));
        foreach (var endpoint in OnlineEvents.IdentificationEndpoints)
        {
            group.MapPost(endpoint + ".fcgi", Measure(HandleIdentificationAsync));
        }

        foreach (var endpoint in OnlineEvents.EnrollEndpoints)
        {
            group.MapPost(endpoint + ".fcgi", Measure(HandleEnrollAsync));
        }

        group.MapPost("new_rex_log.fcgi", Measure(HandleExitButtonAsync));
        group.MapPost("new_user_enrolled.fcgi", Measure(HandleKioskEnrollmentAsync));
        group.MapGet("user_get_image.fcgi", Measure(HandleUserImageAsync));
        group.MapPost("device_is_alive.fcgi", Measure(HandleAliveAsync));
        group.MapPost("session_is_valid.fcgi", Measure(HandleSessionValidAsync));
        CallbackGuard.AddRouteState(group);
        return group;
    }

    private static RequestDelegate Measure(RequestDelegate handler) => CallbackTelemetry.Measure(CallbackTelemetry.Online, handler);

    private static Task HandleAliveAsync(HttpContext context)
    {
        var deviceId = QueryDeviceId(context);
        if (CallbackGuard.Authorize(context, deviceId, LoggerCategory) && CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            Touch(context, deviceId);
            context.Response.StatusCode = StatusCodes.Status200OK;
        }

        return Task.CompletedTask;
    }

    private static async Task HandleSessionValidAsync(HttpContext context)
    {
        var deviceId = QueryDeviceId(context);
        if (!CallbackGuard.Authorize(context, deviceId, LoggerCategory) || !CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        Touch(context, deviceId);

        // Usado pelo equipamento para testar a comunicação; a documentação não define o corpo da resposta.
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"session_is_valid":true}""", context.RequestAborted).ConfigureAwait(false);
    }

    private static long? QueryDeviceId(HttpContext context) =>
        long.TryParse(context.Request.Query["device_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static void Touch(HttpContext context, long? deviceId)
    {
        if (deviceId is { } id)
        {
            CallbackGuard.RecordContact(context, id, DeviceContactSource.Online);
        }
    }

    private static async Task HandleIdentificationAsync(HttpContext context)
    {
        var endpoint = context.Request.Path.Value ?? string.Empty;
        var queryDeviceId = QueryDeviceId(context);

        // Token único ou rota anônima/fechada: decide antes de ler o corpo. Token por equipamento: o device_id pode estar no formulário.
        var authorizeFirst = !CallbackGuard.NeedsBodyToAuthorize(context);
        if (authorizeFirst && !CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
        {
            return;
        }

        var bytes = await CallbackGuard.ReadBodyAsync(context, LoggerCategory).ConfigureAwait(false);
        if (bytes is null)
        {
            return;
        }

        var values = context.Request.Query.Select(q => new KeyValuePair<string, string>(q.Key, q.Value.ToString())).ToList();
        byte[]? body = null;
        if (context.Request.HasFormContentType)
        {
            // O corpo já foi lido com limite; o formulário é interpretado a partir da cópia em memória.
            context.Request.Body = new MemoryStream(bytes, writable: false);
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            values.AddRange(form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value.ToString())));
        }
        else
        {
            // Biometria: parâmetros na query e a imagem/template no corpo (application/octet-stream).
            body = bytes;
        }

        var identification = OnlineEvents.Parse(endpoint, values, body)!;
        if (!authorizeFirst && !CallbackGuard.Authorize(context, identification.DeviceId, LoggerCategory))
        {
            return;
        }

        if (!CallbackGuard.IsKnownDevice(context, identification.DeviceId, LoggerCategory))
        {
            return;
        }

        Touch(context, identification.DeviceId);
        var handler = GetHandler(context);
        var authorization = await CallbackTelemetry.MeasureHandlerAsync(
                CallbackTelemetry.Online, () => handler.AuthorizeAsync(identification, context.RequestAborted)).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{nameof(IOnlineAccessHandler)}.{nameof(IOnlineAccessHandler.AuthorizeAsync)} devolveu null.");

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(authorization.ToResponseJson(), Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task HandleEnrollAsync(HttpContext context)
    {
        var endpoint = context.Request.Path.Value ?? string.Empty;
        var queryDeviceId = QueryDeviceId(context);
        var authorizeFirst = !CallbackGuard.NeedsBodyToAuthorize(context);
        if (authorizeFirst && !CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
        {
            return;
        }

        var bytes = await CallbackGuard.ReadBodyAsync(context, LoggerCategory).ConfigureAwait(false);
        if (bytes is null)
        {
            return;
        }

        OnlineEnrollEvent enrollment;
        try
        {
            enrollment = OnlineEvents.ParseEnroll(endpoint, CallbackBody.ToText(bytes))!;
        }
        catch (ControlId.ControlIdException ex)
        {
            if (authorizeFirst || CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
            {
                Log.InvalidEnrollResult(GetLogger(context), ex, endpoint);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return;
        }

        // O token autentica um equipamento; o cadastro entregue precisa ser dele.
        var bodyDeviceId = enrollment.Result.DeviceId;
        var deviceId = queryDeviceId ?? bodyDeviceId;
        if (!authorizeFirst && !CallbackGuard.Authorize(context, deviceId, LoggerCategory))
        {
            return;
        }

        if (!CallbackGuard.SameDevice(context, queryDeviceId, bodyDeviceId, LoggerCategory)
            || !CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        Touch(context, deviceId);
        var handler = GetHandler(context);
        await CallbackTelemetry.MeasureHandlerAsync(
            CallbackTelemetry.Online, () => handler.OnRemoteEnrollAsync(enrollment, context.RequestAborted)).ConfigureAwait(false);

        // A documentação define essas rotas sem retorno.
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleKioskEnrollmentAsync(HttpContext context)
    {
        var endpoint = context.Request.Path.Value ?? string.Empty;
        var queryDeviceId = QueryDeviceId(context);
        var authorizeFirst = !CallbackGuard.NeedsBodyToAuthorize(context);
        if (authorizeFirst && !CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
        {
            return;
        }

        var bytes = await CallbackGuard.ReadBodyAsync(context, LoggerCategory).ConfigureAwait(false);
        if (bytes is null)
        {
            return;
        }

        KioskEnrollmentEvent enrollment;
        try
        {
            enrollment = OnlineEvents.ParseKioskEnrollment(CallbackBody.ToText(bytes));
        }
        catch (ControlId.ControlIdException ex)
        {
            if (authorizeFirst || CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
            {
                Log.InvalidEnrollResult(GetLogger(context), ex, endpoint);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return;
        }

        // O token autentica um equipamento; o cadastro entregue precisa ser dele.
        var bodyDeviceId = enrollment.DeviceId;
        var deviceId = queryDeviceId ?? bodyDeviceId;
        if (!authorizeFirst && !CallbackGuard.Authorize(context, deviceId, LoggerCategory))
        {
            return;
        }

        if (!CallbackGuard.SameDevice(context, queryDeviceId, bodyDeviceId, LoggerCategory)
            || !CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        Touch(context, deviceId);
        var handler = GetHandler(context);
        await CallbackTelemetry.MeasureHandlerAsync(
            CallbackTelemetry.Online, () => handler.OnKioskEnrollmentAsync(enrollment, context.RequestAborted)).ConfigureAwait(false);

        // A documentação define a rota sem retorno.
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleExitButtonAsync(HttpContext context)
    {
        var queryDeviceId = QueryDeviceId(context);
        var authorizeFirst = !CallbackGuard.NeedsBodyToAuthorize(context);
        if (authorizeFirst && !CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
        {
            return;
        }

        var bytes = await CallbackGuard.ReadBodyAsync(context, LoggerCategory).ConfigureAwait(false);
        if (bytes is null)
        {
            return;
        }

        ExitButtonEvent? exitButton;
        try
        {
            exitButton = JsonSerializer.Deserialize<ExitButtonEvent>(CallbackBody.ToText(bytes), JsonOptions);
        }
        catch (JsonException ex)
        {
            if (authorizeFirst || CallbackGuard.Authorize(context, queryDeviceId, LoggerCategory))
            {
                Log.InvalidExitButton(GetLogger(context), ex);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            return;
        }

        var bodyDeviceId = exitButton?.DeviceId;
        var deviceId = queryDeviceId ?? bodyDeviceId;
        if (!authorizeFirst && !CallbackGuard.Authorize(context, deviceId, LoggerCategory))
        {
            return;
        }

        if (!CallbackGuard.SameDevice(context, queryDeviceId, bodyDeviceId, LoggerCategory)
            || !CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        Touch(context, deviceId);
        var handler = GetHandler(context);
        await CallbackTelemetry.MeasureHandlerAsync(
            CallbackTelemetry.Online, () => handler.OnExitButtonAsync(exitButton ?? new ExitButtonEvent(), context.RequestAborted)).ConfigureAwait(false);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleUserImageAsync(HttpContext context)
    {
        var deviceId = QueryDeviceId(context);
        if (!CallbackGuard.Authorize(context, deviceId, LoggerCategory) || !CallbackGuard.IsKnownDevice(context, deviceId, LoggerCategory))
        {
            return;
        }

        Touch(context, deviceId);
        if (!long.TryParse(context.Request.Query["user_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var handler = GetHandler(context);
        var image = await CallbackTelemetry.MeasureHandlerAsync(
            CallbackTelemetry.Online, () => handler.GetUserImageAsync(userId, context.RequestAborted)).ConfigureAwait(false);
        if (image is null || image.Length == 0)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/octet-stream";
        await context.Response.Body.WriteAsync(image, context.RequestAborted).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    private static IOnlineAccessHandler GetHandler(HttpContext context) =>
        context.RequestServices.GetService<IOnlineAccessHandler>()
        ?? throw new InvalidOperationException("Nenhum IOnlineAccessHandler registrado. Chame services.AddControlIdOnline<THandler>().");

    private static ILogger GetLogger(HttpContext context) => CallbackGuard.GetLogger(context, LoggerCategory);
}

/// <summary>Registro do handler do modo online.</summary>
public static class ControlIdOnlineServiceCollectionExtensions
{
    /// <summary>Registra as regras de acesso do modo online (scoped).</summary>
    public static IServiceCollection AddControlIdOnline<THandler>(this IServiceCollection services)
        where THandler : class, IOnlineAccessHandler
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<DeviceContactTracker>();
        services.TryAddScoped<IOnlineAccessHandler, THandler>();
        return services;
    }

    /// <summary>Registra uma função como regra de acesso do modo online (sem botoeira nem fotos).</summary>
    public static IServiceCollection AddControlIdOnline(
        this IServiceCollection services, Func<OnlineIdentificationEvent, CancellationToken, Task<OnlineAuthorization>> authorize)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (authorize is null)
        {
            throw new ArgumentNullException(nameof(authorize));
        }

        services.TryAddSingleton<DeviceContactTracker>();
        services.TryAddSingleton<IOnlineAccessHandler>(new DelegateOnlineAccessHandler(authorize));
        return services;
    }

    private sealed class DelegateOnlineAccessHandler : IOnlineAccessHandler
    {
        private readonly Func<OnlineIdentificationEvent, CancellationToken, Task<OnlineAuthorization>> _authorize;

        public DelegateOnlineAccessHandler(Func<OnlineIdentificationEvent, CancellationToken, Task<OnlineAuthorization>> authorize) =>
            _authorize = authorize;

        public Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken) =>
            _authorize(identification, cancellationToken);
    }
}
