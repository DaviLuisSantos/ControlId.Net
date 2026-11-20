using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Tipo de canal seguro OSDP de um leitor (<c>osdp_set_secure_type.fcgi</c>).</summary>
public enum OsdpSecureType
{
    /// <summary>Canal inseguro (<c>0</c>).</summary>
    Insecure = 0,

    /// <summary>Modo de instalação (<c>1</c>): o equipamento negocia novas chaves.</summary>
    Installation = 1,

    /// <summary>Somente canal seguro (<c>2</c>).</summary>
    SecureOnly = 2,
}

/// <summary>
/// Resposta de <c>osdp_scbk.fcgi</c> sem <c>scbk</c> no corpo: a Chave Base de Canal Seguro (SCBK) do OSDP.
/// O <see cref="ToString"/> não mostra a chave.
/// </summary>
public sealed class OsdpScbkInfo
{
    /// <summary>Valor atual da SCBK, em hexadecimal. É um segredo: não registre em log.</summary>
    [JsonPropertyName("scbk")]
    public string Scbk { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string ToString() => "OsdpScbkInfo { Scbk = *** }";
}

/// <summary>Resposta de <c>osdp_get_output_capabilities.fcgi</c>: capacidades de saída de um leitor OSDP.</summary>
public sealed class OsdpOutputCapabilities
{
    /// <summary>
    /// Número de saídas suportadas pelo leitor. A documentação grafa a chave do equipamento como
    /// <c>nun_outputs</c> (sic, e não <c>num_outputs</c>); o nome literal é mapeado aqui de propósito.
    /// </summary>
    [JsonPropertyName("nun_outputs")]
    public int NumOutputs { get; set; }

    /// <summary>Nível de conformidade das capacidades de saída do leitor.</summary>
    [JsonPropertyName("compliance_level")]
    public int ComplianceLevel { get; set; }
}

/// <summary>Resposta de <c>camera_calibration_status.fcgi</c>: estado da calibração da câmera (V6).</summary>
public sealed class CameraCalibrationStatus
{
    /// <summary>O equipamento tem uma calibração da câmera gravada.</summary>
    [JsonPropertyName("calibrated")]
    public bool Calibrated { get; set; }

    /// <summary>Valor de calibração gravado; só é significativo quando <see cref="Calibrated"/> é <c>true</c>.</summary>
    [JsonPropertyName("discrepancy_offset")]
    public double DiscrepancyOffset { get; set; }

    /// <summary>Há uma calibração em andamento.</summary>
    [JsonPropertyName("running")]
    public bool Running { get; set; }
}
