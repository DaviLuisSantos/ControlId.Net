using System.Text.Json.Serialization;

namespace ControlId.Rep;

/// <summary>Configuração da câmera e do reconhecimento facial (REP iDClass Facial).</summary>
public sealed class RepFacialModuleConfig
{
    /// <summary>Distância, em centímetros, a partir da qual o rosto é identificado (30 a 200; padrão 100).</summary>
    [JsonPropertyName("identification_distance")] public int IdentificationDistance { get; set; } = 100;

    /// <summary>Luminosidade dos LEDs de apoio, em porcentagem (0 a 100; padrão 100).</summary>
    [JsonPropertyName("leds_brightness")] public int LedsBrightness { get; set; } = 100;

    /// <summary>Com que luz ambiente os LEDs acendem (padrão <see cref="RepLedActivation.LowLight"/>).</summary>
    [JsonPropertyName("led_activation_ambient_brightness")] public RepLedActivation LedActivation { get; set; }

    /// <summary>Calibração das câmeras: <c>"-1"</c> se não calibradas (só leitura).</summary>
    [JsonPropertyName("calibration_state")] public string? CalibrationState { get; set; }
}

/// <summary>Quando os LEDs de identificação acendem (<c>led_activation_ambient_brightness</c>).</summary>
public enum RepLedActivation
{
    /// <summary>Com pouca luz.</summary>
    LowLight = 0,

    /// <summary>Com luz média.</summary>
    MediumLight = 1,

    /// <summary>Com muita luz.</summary>
    HighLight = 2,

    /// <summary>Sempre.</summary>
    Always = 3,
}

/// <summary>Módulo facial do REP iDClass Facial (<c>get_about_facial.fcgi</c>); diferente do REP em si.</summary>
public sealed class RepFacialAbout
{
    /// <summary>Número de série do módulo facial.</summary>
    [JsonPropertyName("serial_number")] public string? SerialNumber { get; set; }

    /// <summary>Firmware do módulo facial.</summary>
    [JsonPropertyName("fw_version")] public string? FirmwareVersion { get; set; }

    /// <summary>Faces cadastradas.</summary>
    [JsonPropertyName("number_of_faces")] public string? NumberOfFaces { get; set; }

    /// <summary>Máximo de faces (10 mil com a licença de <c>set_facial_limit</c>).</summary>
    [JsonPropertyName("max_number_of_faces")] public string? MaxNumberOfFaces { get; set; }
}

/// <summary>Resposta de <c>facial_enroller_state.fcgi</c>.</summary>
public sealed class RepFacialEnrollerState
{
    /// <summary>Há um cadastro facial pela tela do REP.</summary>
    [JsonPropertyName("enrolling")] public bool Enrolling { get; set; }

    /// <summary>Há um cadastro facial remoto (<c>remote_enroll</c>) em andamento.</summary>
    [JsonPropertyName("remote_enrolling")] public bool RemoteEnrolling { get; set; }
}

/// <summary>Resultado de <c>template_extract.fcgi</c>.</summary>
public sealed class RepTemplateExtraction
{
    /// <summary>Qualidade da digital, quando informada.</summary>
    [JsonPropertyName("quality")] public int? Quality { get; set; }

    /// <summary>Template biométrico em base 64.</summary>
    [JsonPropertyName("template")] public string? Template { get; set; }
}

/// <summary>Resultado de <c>template_merge.fcgi</c>.</summary>
public sealed class RepTemplateMergeResult
{
    /// <summary>Template final em base 64.</summary>
    [JsonPropertyName("template")] public string? Template { get; set; }
}
