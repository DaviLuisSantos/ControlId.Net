using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>access_logs</c> da linha de acesso.</summary>
public sealed class AccessLog
{
    /// <summary>ID sequencial do log.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Data/hora do evento (Unix timestamp).</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>Código do evento (ex.: 7 = acesso concedido). Veja a documentação Control iD.</summary>
    [JsonPropertyName("event")]
    public int Event { get; set; }

    /// <summary><see cref="Event"/> como enum.</summary>
    [JsonIgnore]
    public AccessEventType EventType => (AccessEventType)Event;

    /// <summary>ID do dispositivo que gerou o evento.</summary>
    [JsonPropertyName("device_id")]
    public long? DeviceId { get; set; }

    /// <summary>Identificador do leitor/porta.</summary>
    [JsonPropertyName("identifier_id")]
    public long? IdentifierId { get; set; }

    /// <summary>ID do usuário, quando identificado.</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Portal/porta.</summary>
    [JsonPropertyName("portal_id")]
    public long? PortalId { get; set; }

    /// <summary>Regra de identificação envolvida.</summary>
    [JsonPropertyName("identification_rule_id")]
    public long? IdentificationRuleId { get; set; }

    /// <summary>QR Code usado.</summary>
    [JsonPropertyName("qrcode_value")]
    public string? QrCodeValue { get; set; }

    /// <summary>Tag UHF lida.</summary>
    [JsonPropertyName("uhf_tag")]
    public string? UhfTag { get; set; }

    /// <summary>PIN usado.</summary>
    [JsonPropertyName("pin_value")]
    public string? PinValue { get; set; }

    /// <summary>Confiança do reconhecimento facial (0 a 1800).</summary>
    [JsonPropertyName("confidence")]
    public long? Confidence { get; set; }

    /// <summary>1 se o usuário estava de máscara, 0 se não.</summary>
    [JsonPropertyName("mask")]
    public int? Mask { get; set; }

    /// <summary>Tipo de batida (iDFlex Attendance).</summary>
    [JsonPropertyName("log_type_id")]
    public long? LogTypeId { get; set; }

    /// <summary>Valor do cartão lido, quando houver.</summary>
    [JsonPropertyName("card_value")]
    public long? CardValue { get; set; }
}
