using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Adaptador Wi-Fi selecionado em <c>set_wifi_source.fcgi</c>. Firmware V8.</summary>
public enum WifiSource
{
    /// <summary>Wi-Fi desligado (<c>disabled</c>). Qualquer valor desconhecido também desliga.</summary>
    Disabled = 0,

    /// <summary>Adaptador interno (<c>internal</c>).</summary>
    Internal = 1,

    /// <summary>Adaptador USB externo (<c>usb</c>).</summary>
    Usb = 2,
}

/// <summary>Resultado da tentativa de conexão de <c>set_wifi_settings.fcgi</c>.</summary>
public enum WifiConnectionResult
{
    /// <summary>Código que esta versão da biblioteca não conhece (ex.: de um firmware mais novo). Vale -1 porque o 0 já é <see cref="Connected"/>.</summary>
    Unknown = -1,

    /// <summary>0: conectado com sucesso.</summary>
    Connected = 0,

    /// <summary>1: senha incorreta.</summary>
    WrongPassword = 1,

    /// <summary>2: rede não encontrada.</summary>
    NetworkNotFound = 2,

    /// <summary>3: tempo limite da tentativa de conexão esgotado.</summary>
    Timeout = 3,

    /// <summary>4: erro desconhecido informado pelo equipamento.</summary>
    UnknownError = 4,
}

/// <summary>Ponto de acesso encontrado por <c>wifi_scan.fcgi</c>.</summary>
public sealed class WifiNetwork
{
    /// <summary>Nome da rede.</summary>
    [JsonPropertyName("ssid")]
    public string? Ssid { get; set; }

    /// <summary>Endereço MAC do ponto de acesso, no formato <c>xx:xx:xx:xx:xx:xx</c>.</summary>
    [JsonPropertyName("bssid")]
    public string? Bssid { get; set; }

    /// <summary>Frequência de operação, em MHz.</summary>
    [JsonPropertyName("freq")]
    public int Frequency { get; set; }

    /// <summary>Nível de sinal, em dBm (quanto mais perto de zero, mais forte).</summary>
    [JsonPropertyName("signal")]
    public int Signal { get; set; }
}

/// <summary>Configuração e estado da conexão sem fio (<c>get_wifi_data.fcgi</c>). Firmware V8.</summary>
public sealed class WifiData
{
    /// <summary>Adaptadores detectados (<c>"internal"</c> e/ou <c>"usb"</c>).</summary>
    [JsonPropertyName("available_adapters")]
    public List<string> AvailableAdapters { get; set; } = new();

    /// <summary>Adaptador selecionado, como o equipamento informa (<c>"internal"</c>, <c>"usb"</c> ou <c>"disabled"</c>).</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Nome da rede configurada.</summary>
    [JsonPropertyName("ssid")]
    public string? Ssid { get; set; }

    /// <summary>Senha da rede configurada (segredo: não aparece em <see cref="ToString"/>).</summary>
    [JsonPropertyName("psk")]
    public string? Psk { get; set; }

    /// <summary>Estado da conexão, como o equipamento informa (<c>"connected"</c> ou <c>"not_connected"</c>).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Nome da rede conectada agora; vazio quando não há conexão.</summary>
    [JsonPropertyName("connected_ssid")]
    public string? ConnectedSsid { get; set; }

    /// <summary>Interface com a prioridade de roteamento, como o equipamento informa (<c>"wired"</c> ou <c>"wireless"</c>).</summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    /// <summary>O equipamento está conectado a uma rede sem fio.</summary>
    [JsonIgnore]
    public bool IsConnected => string.Equals(Status, "connected", StringComparison.OrdinalIgnoreCase);

    /// <summary>Adaptador selecionado (<see cref="WifiSource.Disabled"/> quando desligado ou desconhecido).</summary>
    [JsonIgnore]
    public WifiSource SourceKind => Source?.ToLowerInvariant() switch
    {
        "internal" => WifiSource.Internal,
        "usb" => WifiSource.Usb,
        _ => WifiSource.Disabled,
    };

    /// <summary>Interface com prioridade de roteamento; <c>null</c> se o equipamento não informou ou informou um valor desconhecido.</summary>
    [JsonIgnore]
    public NetworkInterfaceKind? PriorityInterface => Priority?.ToLowerInvariant() switch
    {
        "wired" => NetworkInterfaceKind.Wired,
        "wireless" => NetworkInterfaceKind.Wireless,
        _ => null,
    };

    /// <inheritdoc/>
    public override string ToString() =>
        $"WifiData {{ Source = {Source}, Ssid = {Ssid}, Psk = ***, Status = {Status}, ConnectedSsid = {ConnectedSsid}, Priority = {Priority} }}";
}
