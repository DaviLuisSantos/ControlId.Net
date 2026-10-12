using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Evento de identificação com mensagem sonora (iDFace).</summary>
public enum AccessAudioEvent
{
    /// <summary>"Usuário não identificado" (<c>not_identified</c>).</summary>
    NotIdentified,

    /// <summary>"Acesso liberado" (<c>authorized</c>).</summary>
    Authorized,

    /// <summary>"Acesso negado" (<c>not_authorized</c>).</summary>
    NotAuthorized,

    /// <summary>"Por favor, use máscara" (<c>use_mask</c>).</summary>
    UseMask,
}

/// <summary>Resposta de <c>has_audio_access_messages.fcgi</c>: quais eventos têm áudio personalizado.</summary>
public sealed class AccessAudioMessages
{
    /// <summary>"Não identificado" tem áudio personalizado.</summary>
    [JsonPropertyName("not_identified")]
    public bool NotIdentified { get; set; }

    /// <summary>"Autorizado" tem áudio personalizado.</summary>
    [JsonPropertyName("authorized")]
    public bool Authorized { get; set; }

    /// <summary>"Não autorizado" tem áudio personalizado.</summary>
    [JsonPropertyName("not_authorized")]
    public bool NotAuthorized { get; set; }

    /// <summary>"Use máscara" tem áudio personalizado.</summary>
    [JsonPropertyName("use_mask")]
    public bool UseMask { get; set; }
}

/// <summary>Resposta de <c>get_sip_status.fcgi</c>.</summary>
public sealed class SipStatus
{
    /// <summary>
    /// Código SIP: -1 desabilitado, 0/100 conectando, 200 conectado, 401/403 falha de autenticação,
    /// 408 servidor inacessível, 503 falha de rede.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>Há chamada em andamento.</summary>
    [JsonPropertyName("in_call")]
    public bool InCall { get; set; }

    /// <summary><see cref="Status"/> igual a 200.</summary>
    [JsonIgnore]
    public bool Registered => Status == 200;
}
