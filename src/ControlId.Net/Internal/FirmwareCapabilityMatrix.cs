using System;
using System.Collections.Generic;

namespace ControlId.Internal;

/// <summary>
/// Comandos que se sabe, por observação em equipamento real, que um firmware não tem (respondem "Invalid command").
/// A lista espelha as seções "Observado num iDFace real" de <c>docs/COBERTURA.md</c>; um teste garante que as duas
/// concordam. Só entram endpoints; objetos ("Invalid table") e seções de configuração ficam de fora porque não marcam o
/// comando no cache do transporte (ADR 0027).
/// </summary>
internal static class FirmwareCapabilityMatrix
{
    /// <summary>Comandos sem equivalente no iDFace 6.9.12.</summary>
    internal static readonly string[] Firmware6912 =
    {
        "remote_led_control",
        "create_or_modify_objects",
        "face_template_extract_list",
        "face_template_enroll",
        "face_template_destroy",
        "template_sync_init",
        "template_sync_end",
        "is_valid_biometry",
        "connection_test",
        "ping_test",
        "nslookup_test",
        "set_network_interlock",
        "send_video",
        "set_custom_video",
        "remove_custom_video",
        "set_osdp_installation_mode",
        "idflex_upgrade_enterprise",
    };

    /// <summary>Comandos sem equivalente no iDFace 6.13.1.</summary>
    internal static readonly string[] Firmware61301 =
    {
        "get_catra_info",
        "get_slideshow_images_list",
        "get_kiosk_icons_list",
    };

    private static readonly (string Firmware, string[] Commands)[] Entries =
    {
        ("6.9.12", Firmware6912),
        ("6.13.1", Firmware61301),
    };

    /// <summary>Firmwares com matriz conhecida, com os comandos não suportados de cada um.</summary>
    internal static IEnumerable<(string Firmware, string[] Commands)> All => Entries;

    /// <summary>Comandos sabidamente ausentes em <paramref name="firmware"/>; vazio se o firmware não está na matriz.</summary>
    internal static IReadOnlyCollection<string> UnsupportedFor(string? firmware)
    {
        var version = firmware?.Trim();
        if (string.IsNullOrEmpty(version))
        {
            return Array.Empty<string>();
        }

        foreach (var (known, commands) in Entries)
        {
            if (Matches(version!, known))
            {
                return commands;
            }
        }

        return Array.Empty<string>();
    }

    // "6.9.12" casa com "6.9.12" e "6.9.12-rc1", mas não com "6.9.123".
    private static bool Matches(string version, string known)
    {
        if (!version.StartsWith(known, StringComparison.Ordinal))
        {
            return false;
        }

        return version.Length == known.Length || (!char.IsDigit(version[known.Length]) && version[known.Length] != '.');
    }
}
