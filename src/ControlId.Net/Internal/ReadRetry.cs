using System;
using System.Collections.Generic;
using System.Net;
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

        // Biometria, schema, rede e hardware (leituras: não alteram o equipamento).
        "face_template_count_distinct", "face_template_match", "template_match", "object_metadata", "export_object",
        "export_custom_tables_metadata", "get_ntp_server", "get_ntp_server_status", "check_port_availability",
        "get_vpn_ip", "get_ssl_certificate", "get_802_1X_status", "get_ac_log", "get_openvpn_log", "get_wpa_log",
        "get_wpasupplicant_log", "io_state", "is_identifying", "get_catra_info", "get_last_snapshot",
        "device_rfid_module", "get_hid_last_card", "get_wiegand_card_read",

        // REP iDClass.
        "count_users", "export_users_csv", "facial_enroller_state", "get_about", "get_about_facial", "get_afd",
        "get_coil_paper", "get_facial_module_config", "get_idcloud", "get_info", "get_public_key",
        "get_system_configuration", "get_system_date_time", "get_system_daylight_saving_time", "get_system_information",
        "get_system_network", "load_company", "load_users",

        // Onda 3: leituras puras (algumas são POST na API, mas não alteram o equipamento).
        "get_wifi_data", "check_ip_conflict", "camera_calibration_status", "get_slideshow_images_list",
        "get_slideshow_image", "get_kiosk_icon", "get_kiosk_icons_list", "get_phone_icon", "get_streaming_logo",
        "device_has_secbox", "secbox_is_active", "secbox_serial_number", "update_secbox_firmware_status",
        "update_secbox_firmware_version", "get_dual_sided_status", "get_osdp_installation_mode",
        "osdp_get_connection_status", "osdp_get_output_capabilities",

        // Onda 4: leituras do cliente de provisionamento (ADR 0024).
        "hid_get_key_info", "hid_get_oid", "get_oem_code", "get_oem_downgrade_approved_versions",
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

    /// <summary>
    /// Comandos cuja falha de rede ou timeout não deixa dúvida sobre o desfecho: as leituras e o login (que só abre uma sessão).
    /// Os demais viram <see cref="ControlIdOutcomeUnknownException"/> quando a resposta não chega.
    /// </summary>
    public static bool IsSafeToRepeat(string command) =>
        IsReadOnly(command) || string.Equals(command, "login", StringComparison.Ordinal) || string.Equals(command, "logout", StringComparison.Ordinal);

    /// <summary>Falha de rede ou timeout do cliente de uma leitura (não um cancelamento pedido por quem chamou).</summary>
    public static bool IsTransient(ControlIdException exception) =>
        exception is ControlIdTimeoutException or ControlIdConnectionException;

    /// <summary>Espera antes da tentativa <paramref name="retry"/> (1, 2, ...): o dobro da anterior, ±20%.</summary>
    public static TimeSpan Delay(TimeSpan baseDelay, int retry)
    {
        var factor = Math.Pow(2, retry - 1) * (0.8 + (Jitter.NextDouble() * 0.4));
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor);
    }

    public static Task WaitAsync(TimeSpan baseDelay, int retry, CancellationToken cancellationToken) =>
        baseDelay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(Delay(baseDelay, retry), cancellationToken);
}
