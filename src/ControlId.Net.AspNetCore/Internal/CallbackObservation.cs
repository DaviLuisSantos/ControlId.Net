using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControlId.AspNetCore.Internal;

/// <summary>
/// Entrega aos <see cref="IControlIdCallbackObserver"/> uma cópia do callback antes do <c>Parse</c> e dos handlers. O
/// corpo e os cabeçalhos são mascarados; uma falha do observador é apenas registrada em log, nunca propagada.
/// </summary>
internal static class CallbackObservation
{
    private const string LoggerCategory = "ControlId.AspNetCore";
    private const string MaskValue = "***";
    private const string FormContentType = "x-www-form-urlencoded";

    private static readonly HashSet<string> ExcludedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
    };

    /// <summary>
    /// Mesma lista de <c>Cassette.DefaultMaskedFields</c> (src/ControlId.Net.Testing/Cassette.cs). Copiada para o pacote
    /// ASP.NET Core não depender do pacote de testes.
    /// </summary>
    private static readonly HashSet<string> MaskedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "session",
        "login",
        "password",
        "salt",
        "image",
        "template",
    };

    private static readonly object NotifiedKey = new();

    /// <summary>
    /// Entrega o callback antes do <c>Parse</c> quando o corpo não é necessário para aceitar a requisição: a rota já
    /// autorizou (token único ou anônima) e não há <see cref="ControlIdCallbackOptions.IsKnownDevice"/>, que pode depender
    /// do <c>device_id</c> do corpo. Nos demais casos não faz nada, e a rota notifica depois de aceitar a requisição.
    /// </summary>
    public static Task NotifyBeforeParseAsync(
        HttpContext context,
        ControlIdCallbackKind kind,
        bool authorized,
        long? queryDeviceId,
        byte[] body) =>
        authorized && GetOptions(context)?.IsKnownDevice is null
            ? NotifyAsync(context, kind, queryDeviceId ?? PeekDeviceId(body, context.Request.ContentType), body)
            : Task.CompletedTask;

    /// <summary>
    /// Chama, na ordem de registro, todos os observadores resolvidos no escopo da requisição, uma única vez por requisição.
    /// Sem observadores não faz nada. A exceção de um observador, ou o estouro de
    /// <see cref="ControlIdCallbackOptions.ObserverTimeout"/>, é logada (2015 ou 2016) e não impede os seguintes nem a
    /// resposta ao equipamento.
    /// </summary>
    public static async Task NotifyAsync(
        HttpContext context,
        ControlIdCallbackKind kind,
        long? deviceId,
        byte[] body)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        if (!context.Items.TryAdd(NotifiedKey, true))
        {
            return;
        }

        IControlIdCallbackObserver[] observers;
        try
        {
            observers = context.RequestServices.GetServices<IControlIdCallbackObserver>().ToArray();
        }
        catch (Exception ex)
        {
            Log.CallbackObserverFailed(
                GetLogger(context), ex, typeof(IControlIdCallbackObserver).FullName!, Route(context));
            return;
        }

        if (observers.Length == 0)
        {
            return;
        }

        var callback = CreateCallback(context, kind, deviceId, body);
        var logger = GetLogger(context);
        var timeout = GetOptions(context)?.ObserverTimeout ?? ControlIdCallbackOptions.DefaultObserverTimeout;
        foreach (var observer in observers)
        {
            try
            {
                if (!await ObserveAsync(observer, callback, timeout, context.RequestAborted).ConfigureAwait(false))
                {
                    Log.CallbackObserverTimedOut(logger, TypeName(observer), callback.Route, timeout);
                }
            }
            catch (Exception ex)
            {
                Log.CallbackObserverFailed(logger, ex, TypeName(observer), callback.Route);
            }
        }
    }

    private static ControlIdCallbackOptions? GetOptions(HttpContext context) =>
        context.RequestServices.GetService<IOptions<ControlIdCallbackOptions>>()?.Value;

    /// <summary>
    /// <see langword="false"/> quando o prazo vence. Com prazo, o observador roda fora do fluxo da requisição, para um
    /// bloqueio síncrono também respeitá-lo.
    /// </summary>
    private static async Task<bool> ObserveAsync(
        IControlIdCallbackObserver observer, ControlIdRawCallback callback, TimeSpan timeout, CancellationToken aborted)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await (observer.OnCallbackAsync(callback, aborted) ?? Task.CompletedTask).ConfigureAwait(false);
            return true;
        }

        // Sem Dispose: o observador que estourou o prazo ainda pode estar usando o token.
        var cts = CancellationTokenSource.CreateLinkedTokenSource(aborted);
        var token = cts.Token;
        var observation = Task.Run(() => observer.OnCallbackAsync(callback, token) ?? Task.CompletedTask, CancellationToken.None);
        using var delayCts = new CancellationTokenSource();
        var first = await Task.WhenAny(observation, Task.Delay(timeout, delayCts.Token)).ConfigureAwait(false);
        if (!ReferenceEquals(first, observation))
        {
            _ = observation.ContinueWith(
                static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            // CancelAfter não roda os registros do observador nesta thread.
            cts.CancelAfter(TimeSpan.Zero);
            return false;
        }

        delayCts.Cancel();
        await observation.ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// <c>device_id</c> lido do corpo (JSON ou formulário) sem validar o restante; <see langword="null"/> quando não dá
    /// para ler. Nunca lança.
    /// </summary>
    private static long? PeekDeviceId(byte[] body, string? contentType)
    {
        try
        {
            var text = CallbackBody.ToText(body);
            if (contentType is not null && contentType.IndexOf(FormContentType, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (var part in text.Split('&'))
                {
                    if (part.StartsWith("device_id=", StringComparison.Ordinal))
                    {
                        return ParseId(part.Substring("device_id=".Length));
                    }
                }

                return null;
            }

            using var json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("device_id", out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.TryGetInt64(out var number) ? number : null;
            }

            return value.ValueKind == JsonValueKind.String ? ParseId(value.GetString()) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long? ParseId(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static ControlIdRawCallback CreateCallback(
        HttpContext context,
        ControlIdCallbackKind kind,
        long? deviceId,
        byte[] body)
    {
        var request = context.Request;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            if (!ExcludedHeaders.Contains(header.Key))
            {
                headers[header.Key] = header.Value.ToString();
            }
        }

        return new ControlIdRawCallback(
            kind,
            Route(context),
            headers,
            deviceId,
            MaskBody(body, request.ContentType));
    }

    /// <summary>
    /// Mascara os campos de <see cref="MaskedFields"/> num JSON (recursivamente) ou num formulário
    /// <c>application/x-www-form-urlencoded</c>. Corpo vazio, sem nada a mascarar ou que não dá para interpretar volta na
    /// mesma instância, byte a byte.
    /// </summary>
    private static byte[] MaskBody(byte[] body, string? contentType)
    {
        if (body.Length == 0)
        {
            return body;
        }

        try
        {
            var text = CallbackBody.ToText(body);
            var trimmed = text.AsSpan().TrimStart();

            if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '['))
            {
                var node = JsonNode.Parse(text);
                if (node is null || !MaskNode(node))
                {
                    return body;
                }

                return Encoding.UTF8.GetBytes(node.ToJsonString());
            }

            if (contentType is not null
                && contentType.IndexOf(FormContentType, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var masked = MaskForm(text, out var replaced);
                return replaced ? Encoding.UTF8.GetBytes(masked) : body;
            }
        }
        catch (Exception)
        {
            // JSON inválido, chave duplicada ou qualquer outro corpo que não dá para interpretar: entrega o original.
            return body;
        }

        return body;
    }

    private static bool MaskNode(JsonNode node)
    {
        var replaced = false;
        if (node is JsonObject obj)
        {
            foreach (var name in obj.Select(property => property.Key).ToList())
            {
                if (obj[name] is JsonValue && MaskedFields.Contains(name))
                {
                    obj[name] = MaskValue;
                    replaced = true;
                }
                else if (obj[name] is { } child)
                {
                    replaced |= MaskNode(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    replaced |= MaskNode(item);
                }
            }
        }

        return replaced;
    }

    private static string MaskForm(string text, out bool replaced)
    {
        var parts = text.Split('&');
        replaced = false;

        for (var i = 0; i < parts.Length; i++)
        {
            var equals = parts[i].IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(parts[i].Substring(0, equals).Replace('+', ' '));
            if (MaskedFields.Contains(name))
            {
                parts[i] = parts[i].Substring(0, equals + 1) + MaskValue;
                replaced = true;
            }
        }

        return replaced ? string.Join("&", parts) : text;
    }

    private static ILogger GetLogger(HttpContext context)
    {
        try
        {
            return context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(LoggerCategory) ?? NullLogger.Instance;
        }
        catch (Exception)
        {
            return NullLogger.Instance;
        }
    }

    private static string TypeName(IControlIdCallbackObserver observer)
    {
        var type = observer.GetType();
        return type.FullName ?? type.Name;
    }

    private static string Route(HttpContext context) =>
        (context.Request.PathBase + context.Request.Path).ToString();
}
