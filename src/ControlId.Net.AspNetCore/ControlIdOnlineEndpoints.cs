using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Online;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// <c>new_user_id_and_password</c>, <c>new_biometric_image</c>, <c>new_biometric_template</c>) →
    /// <see cref="IOnlineAccessHandler.AuthorizeAsync"/>;</item>
    /// <item><c>new_rex_log</c> → <see cref="IOnlineAccessHandler.OnExitButtonAsync"/>;</item>
    /// <item>resultados do cadastro remoto (<c>card_create</c>, <c>face_create</c>, <c>fingerprint_create</c>,
    /// <c>template_create</c>, <c>pin_create</c>, <c>password_create</c>) → <see cref="IOnlineAccessHandler.OnRemoteEnrollAsync"/>;</item>
    /// <item><c>GET user_get_image</c> → <see cref="IOnlineAccessHandler.GetUserImageAsync"/>;</item>
    /// <item><c>device_is_alive</c> e <c>session_is_valid</c> → 200, para o equipamento sair da contingência.</item>
    /// </list>
    /// </summary>
    public static RouteGroupBuilder MapControlIdOnline(this IEndpointRouteBuilder endpoints, string prefix = "/")
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup("/" + prefix.Trim('/'));
        foreach (var endpoint in OnlineEvents.IdentificationEndpoints)
        {
            group.MapPost(endpoint + ".fcgi", HandleIdentificationAsync);
        }

        foreach (var endpoint in OnlineEvents.EnrollEndpoints)
        {
            group.MapPost(endpoint + ".fcgi", HandleEnrollAsync);
        }

        group.MapPost("new_rex_log.fcgi", HandleExitButtonAsync);
        group.MapGet("user_get_image.fcgi", HandleUserImageAsync);
        group.MapPost("device_is_alive.fcgi", static context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        group.MapPost("session_is_valid.fcgi", static async context =>
        {
            // Usado pelo equipamento para testar a comunicação; a documentação não define o corpo da resposta.
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"session_is_valid":true}""", context.RequestAborted).ConfigureAwait(false);
        });
        return group;
    }

    private static async Task HandleIdentificationAsync(HttpContext context)
    {
        var endpoint = context.Request.Path.Value ?? string.Empty;
        var values = context.Request.Query.Select(q => new KeyValuePair<string, string>(q.Key, q.Value.ToString())).ToList();
        byte[]? body = null;

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            values.AddRange(form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value.ToString())));
        }
        else
        {
            // Biometria: parâmetros na query e a imagem/template no corpo (application/octet-stream).
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted).ConfigureAwait(false);
            body = buffer.ToArray();
        }

        var identification = OnlineEvents.Parse(endpoint, values, body)!;
        var authorization = await GetHandler(context).AuthorizeAsync(identification, context.RequestAborted).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{nameof(IOnlineAccessHandler)}.{nameof(IOnlineAccessHandler.AuthorizeAsync)} devolveu null.");

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(authorization.ToResponseJson(), Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task HandleEnrollAsync(HttpContext context)
    {
        var endpoint = context.Request.Path.Value ?? string.Empty;
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        OnlineEnrollEvent enrollment;
        try
        {
            enrollment = OnlineEvents.ParseEnroll(endpoint, body)!;
        }
        catch (ControlId.ControlIdException ex)
        {
            GetLogger(context).LogWarning(ex, "Resultado de cadastro remoto inválido em {Endpoint}.", endpoint);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await GetHandler(context).OnRemoteEnrollAsync(enrollment, context.RequestAborted).ConfigureAwait(false);

        // A documentação define essas rotas sem retorno.
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleExitButtonAsync(HttpContext context)
    {
        ExitButtonEvent? exitButton;
        try
        {
            exitButton = await JsonSerializer.DeserializeAsync<ExitButtonEvent>(
                context.Request.Body, JsonOptions, context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            GetLogger(context).LogWarning(ex, "new_rex_log inválido.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await GetHandler(context).OnExitButtonAsync(exitButton ?? new ExitButtonEvent(), context.RequestAborted).ConfigureAwait(false);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private static async Task HandleUserImageAsync(HttpContext context)
    {
        if (!long.TryParse(context.Request.Query["user_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var image = await GetHandler(context).GetUserImageAsync(userId, context.RequestAborted).ConfigureAwait(false);
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

    private static ILogger GetLogger(HttpContext context) =>
        context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(LoggerCategory) ?? NullLogger.Instance;
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
