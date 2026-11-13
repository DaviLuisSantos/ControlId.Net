using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Internal;

/// <summary>
/// Quais comandos podem ser repetidos e quando (ADR 0013). Só leituras: na API da Control iD tudo é POST, então a lista é
/// explícita, por nome de comando.
/// </summary>
internal static class ReadRetry
{
    private static readonly HashSet<string> ReadOnlyCommands = new(StringComparer.Ordinal)
    {
        // Linha de acesso.
        "load_objects", "system_information", "get_configuration", "doors_state", "gpio_state", "enroller_state",
        "session_is_valid", "user_get_image", "user_list_images", "user_get_image_list", "export_objects",
        "report_generate", "export_afd", "export_audit_logs", "logo", "has_audio_access_messages",
        "get_audio_access_message", "get_sip_status", "get_pjsip_audio_message",

        // REP iDClass.
        "count_users", "export_users_csv", "facial_enroller_state", "get_about", "get_about_facial", "get_afd",
        "get_coil_paper", "get_facial_module_config", "get_idcloud", "get_info", "get_public_key",
        "get_system_configuration", "get_system_date_time", "get_system_daylight_saving_time", "get_system_information",
        "get_system_network", "load_company", "load_users",
    };

#if NET6_0_OR_GREATER
    private static Random Jitter => Random.Shared;
#else
    [ThreadStatic]
    private static Random? _jitter;

    private static Random Jitter => _jitter ??= new Random(Guid.NewGuid().GetHashCode());
#endif

    public static bool IsReadOnly(string command) => ReadOnlyCommands.Contains(command);

    /// <summary>HTTP que vale repetir: o equipamento (ou algo no caminho) está sobrecarregado ou indisponível.</summary>
    public static bool IsTransient(HttpStatusCode status) =>
        (int)status >= 500 || status == HttpStatusCode.RequestTimeout || (int)status == 429;

    /// <summary>Falha de rede ou timeout do cliente (não um cancelamento pedido por quem chamou).</summary>
    public static bool IsTransient(ControlIdException exception) =>
        exception.StatusCode is null && exception.InnerException is HttpRequestException or TaskCanceledException;

    /// <summary>Espera antes da tentativa <paramref name="retry"/> (1, 2, ...): o dobro da anterior, ±20%.</summary>
    public static TimeSpan Delay(TimeSpan baseDelay, int retry)
    {
        var factor = Math.Pow(2, retry - 1) * (0.8 + (Jitter.NextDouble() * 0.4));
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor);
    }

    public static Task WaitAsync(TimeSpan baseDelay, int retry, CancellationToken cancellationToken) =>
        baseDelay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(Delay(baseDelay, retry), cancellationToken);
}
