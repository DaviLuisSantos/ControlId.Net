using System;
using System.Collections.Generic;

namespace ControlId.DependencyInjection;

/// <summary>
/// Opções do guardião de configuração (<c>AddControlIdConfigurationGuard</c>): de tempos em tempos ele confere cada equipamento
/// com a configuração desejada e corrige o que divergir, inclusive o relógio.
/// </summary>
public sealed class ControlIdConfigurationGuardOptions
{
    /// <summary>Intervalo entre os ciclos de conferência. Padrão: 5 minutos. Precisa ser positivo.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Diferença máxima aceita entre o relógio do equipamento e a hora atual no <see cref="TimeZone"/>; além dela o guardião
    /// chama <c>SetSystemTimeAsync</c>. Padrão: 60 segundos. <c>null</c> desliga o acerto do relógio. Não pode ser negativa.
    /// </summary>
    public TimeSpan? ClockTolerance { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Fuso usado para calcular a hora local que o equipamento deveria mostrar (ele guarda a hora de parede, sem fuso).
    /// Padrão: <see cref="TimeZoneInfo.Local"/>.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    /// <summary>
    /// Configuração desejada por equipamento. A chave é o nome com que o equipamento foi registrado em <c>AddControlIdAccess</c>
    /// (<c>""</c> para o padrão). Equipamento sem entrada aqui não é conferido.
    /// </summary>
    public IDictionary<string, DesiredDeviceConfiguration> Devices { get; } =
        new Dictionary<string, DesiredDeviceConfiguration>(StringComparer.Ordinal);
}

/// <summary>
/// Configuração que o guardião mantém num equipamento. O que ficar <c>null</c> (ou vazio) não é conferido.
/// Parâmetros que o firmware não devolve na leitura são ignorados, em vez de arriscar um <c>set_configuration</c> recusado.
/// </summary>
public sealed class DesiredDeviceConfiguration
{
    /// <summary>Modo online ligado (<c>true</c>) ou desligado (<c>false</c>): <c>general.online</c>.</summary>
    public bool? Online { get; set; }

    /// <summary>Id do servidor na tabela <c>devices</c> do equipamento: <c>online_client.server_id</c>.</summary>
    public long? OnlineServerId { get; set; }

    /// <summary>Destino do Monitor: <c>monitor.hostname</c>, <c>monitor.port</c>, <c>monitor.path</c> e <c>monitor.request_timeout</c>.</summary>
    public MonitorTarget? Monitor { get; set; }

    /// <summary>
    /// Seções extras, no formato do <c>set_configuration</c>: nome da seção e, dentro dela, parâmetro e valor (sempre texto).
    /// Ex.: <c>["general"] = { ["antipassback"] = "1" }</c>. Os campos tipados acima têm precedência sobre estes.
    /// </summary>
    public IDictionary<string, IDictionary<string, string>> Settings { get; } =
        new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal);
}

/// <summary>Para onde o equipamento envia as notificações do Monitor. Só o que for preenchido é conferido.</summary>
public sealed class MonitorTarget
{
    /// <summary>IP ou nome do servidor (<c>monitor.hostname</c>).</summary>
    public string? Hostname { get; set; }

    /// <summary>Porta do servidor (<c>monitor.port</c>).</summary>
    public int? Port { get; set; }

    /// <summary>Caminho base, sem barras nas pontas (<c>monitor.path</c>). O padrão do equipamento é <c>api/notifications</c>.</summary>
    public string? Path { get; set; }

    /// <summary>Prazo de cada notificação (<c>monitor.request_timeout</c>, gravado em milissegundos).</summary>
    public TimeSpan? RequestTimeout { get; set; }
}
