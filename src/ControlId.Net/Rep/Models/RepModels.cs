using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Rep;

/// <summary>Resposta de <c>get_about.fcgi</c>.</summary>
public sealed class RepAbout
{
    /// <summary>MAC.</summary>
    [JsonPropertyName("mac")] public string? Mac { get; set; }

    /// <summary>Número de série.</summary>
    [JsonPropertyName("nSerie")] public string? SerialNumber { get; set; }

    /// <summary>Versão do firmware.</summary>
    [JsonPropertyName("versionFW")] public long FirmwareVersion { get; set; }

    /// <summary>Versão da MRP.</summary>
    [JsonPropertyName("versionMRP")] public long MrpVersion { get; set; }

    /// <summary>Modelo facial.</summary>
    [JsonPropertyName("isFacial")] public bool IsFacial { get; set; }
}

/// <summary>Resposta de <c>get_system_information.fcgi</c>.</summary>
public sealed class RepSystemInformation
{
    /// <summary>Tempo ligado.</summary>
    [JsonPropertyName("uptime")] public long Uptime { get; set; }

    /// <summary>Usuários cadastrados.</summary>
    [JsonPropertyName("user_count")] public int UserCount { get; set; }

    /// <summary>Templates biométricos (modelos não faciais).</summary>
    [JsonPropertyName("template_count")] public int? TemplateCount { get; set; }

    /// <summary>Faces cadastradas (modelos faciais).</summary>
    [JsonPropertyName("number_of_faces")] public string? NumberOfFaces { get; set; }

    /// <summary>Papel OK.</summary>
    [JsonPropertyName("paper_ok")] public bool PaperOk { get; set; }

    /// <summary>Pouco papel (<c>null</c> se não houver sensor).</summary>
    [JsonPropertyName("low_paper")] public bool? LowPaper { get; set; }

    /// <summary>Uso de memória.</summary>
    [JsonPropertyName("memory")] public long Memory { get; set; }

    /// <summary>MRP utilizada.</summary>
    [JsonPropertyName("used_mrp")] public long UsedMrp { get; set; }

    /// <summary>Último NSR gravado.</summary>
    [JsonPropertyName("last_nsr")] public long LastNsr { get; set; }
}

/// <summary>Usuário do REP. Identificado por PIS (legado) ou CPF (Portaria 671).</summary>
public sealed class RepUser
{
    /// <summary>PIS (modo legado).</summary>
    [JsonPropertyName("pis")] public long? Pis { get; set; }

    /// <summary>CPF (modo Portaria 671).</summary>
    [JsonPropertyName("cpf")] public long? Cpf { get; set; }

    /// <summary>Nome.</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }

    /// <summary>Matrícula.</summary>
    [JsonPropertyName("registration")] public string? Registration { get; set; }

    /// <summary>Cartão RFID.</summary>
    [JsonPropertyName("rfid")] public long? Rfid { get; set; }

    /// <summary>Código de barras.</summary>
    [JsonPropertyName("bars")] public string? Bars { get; set; }

    /// <summary>Código numérico.</summary>
    [JsonPropertyName("code")] public long? Code { get; set; }

    /// <summary>Senha numérica.</summary>
    [JsonPropertyName("password")] public string? Password { get; set; }

    /// <summary>Administrador.</summary>
    [JsonPropertyName("admin")] public bool? Admin { get; set; }

    /// <summary>Templates biométricos em base64.</summary>
    [JsonPropertyName("templates")] public List<string>? Templates { get; set; }
}

/// <summary>Resultado paginado de <c>load_users.fcgi</c>.</summary>
public sealed class RepUserPage
{
    /// <summary>Total de usuários no equipamento.</summary>
    [JsonPropertyName("count")] public int Count { get; set; }

    /// <summary>Usuários desta página.</summary>
    [JsonPropertyName("users")] public List<RepUser> Users { get; set; } = new();
}

/// <summary>Modo de operação do REP.</summary>
public enum RepMode
{
    /// <summary>Comportamento legado (Portaria 1510, usuários por PIS).</summary>
    Legacy,

    /// <summary>Portaria 671 (usuários por CPF, AFD no novo layout, fuso horário).</summary>
    Portaria671,
}
