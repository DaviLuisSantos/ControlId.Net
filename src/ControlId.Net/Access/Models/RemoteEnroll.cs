using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>O que o equipamento vai capturar no cadastro remoto.</summary>
public enum RemoteEnrollType
{
    /// <summary>Cartão de proximidade (<c>card</c>).</summary>
    Card,

    /// <summary>Face (<c>face</c>).</summary>
    Face,

    /// <summary>Impressão digital (<c>biometry</c>).</summary>
    Biometry,

    /// <summary>PIN (<c>pin</c>).</summary>
    Pin,

    /// <summary>Senha (<c>password</c>).</summary>
    Password,
}

/// <summary>Parâmetros de <c>remote_enroll.fcgi</c>.</summary>
public sealed class RemoteEnrollRequest
{
    /// <summary>Cria o pedido de cadastro do tipo informado.</summary>
    public RemoteEnrollRequest(RemoteEnrollType type) => Type = type;

    /// <summary>O que capturar.</summary>
    public RemoteEnrollType Type { get; }

    /// <summary>Usuário dono do cadastro. Obrigatório com <see cref="Save"/>; precisa já existir no equipamento.</summary>
    public long? UserId { get; set; }

    /// <summary>
    /// <c>true</c>: o equipamento grava o cartão/face/digital/PIN/senha no usuário <see cref="UserId"/>.
    /// <c>false</c> (padrão do equipamento): só captura e devolve o dado, sem gravar.
    /// </summary>
    public bool Save { get; set; }

    /// <summary>Cadastra a digital como dedo de pânico (só <see cref="RemoteEnrollType.Biometry"/>).</summary>
    public bool? PanicFinger { get; set; }

    /// <summary>Matrícula da pessoa.</summary>
    public string? Registration { get; set; }

    /// <summary>Mensagem exibida durante o cadastro; vazia mostra a padrão.</summary>
    public string? Message { get; set; }

    /// <summary>Face: confirma a foto sozinho após a contagem regressiva, sem tocar no display.</summary>
    public bool? Auto { get; set; }

    /// <summary>Face com <see cref="Auto"/>: duração da contagem regressiva (padrão do equipamento: 5 s).</summary>
    public TimeSpan? Countdown { get; set; }

    internal Dictionary<string, object> ToBody(bool sync)
    {
        if (Save && UserId is null)
        {
            throw new ArgumentException("Informe UserId para gravar o cadastro no equipamento (Save = true).", nameof(UserId));
        }

        var body = new Dictionary<string, object>
        {
            ["type"] = Type switch
            {
                RemoteEnrollType.Card => "card",
                RemoteEnrollType.Face => "face",
                RemoteEnrollType.Biometry => "biometry",
                RemoteEnrollType.Pin => "pin",
                RemoteEnrollType.Password => "password",
                _ => throw new ArgumentOutOfRangeException(nameof(Type)),
            },
            ["save"] = Save,
            ["sync"] = sync,
        };

        if (UserId is { } userId)
        {
            body["user_id"] = userId;
        }

        if (PanicFinger is { } panic)
        {
            body["panic_finger"] = panic ? 1 : 0;
        }

        if (Registration is not null)
        {
            body["registration"] = Registration;
        }

        // A tabela da documentação chama de "msg", mas a descrição e o exemplo usam "message".
        if (Message is not null)
        {
            body["message"] = Message;
        }

        if (Auto is { } auto)
        {
            body["auto"] = auto;
        }

        if (Countdown is { } countdown)
        {
            body["countdown"] = (int)Math.Round(countdown.TotalSeconds);
        }

        return body;
    }
}

/// <summary>
/// Resultado do cadastro remoto: resposta de <c>remote_enroll.fcgi</c> no modo síncrono ou corpo da notificação
/// do Monitor no modo assíncrono. Só os campos do tipo cadastrado vêm preenchidos.
/// </summary>
public sealed class RemoteEnrollResult
{
    /// <summary><c>success</c> como veio do equipamento.</summary>
    [JsonPropertyName("success")]
    public bool? SuccessFlag { get; set; }

    /// <summary>Cadastro concluído. Sem o campo <c>success</c>, considera sucesso a ausência de <see cref="Error"/>.</summary>
    [JsonIgnore]
    public bool Success => SuccessFlag ?? Error is null;

    /// <summary>Erro informado pelo equipamento, ex.: <c>FACE_EXISTS</c>.</summary>
    [JsonPropertyName("error"), JsonConverter(typeof(LenientStringConverter))]
    public string? Error { get; set; }

    /// <summary>Usuário do cadastro.</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Equipamento que fez o cadastro.</summary>
    [JsonPropertyName("device_id")]
    public long? DeviceId { get; set; }

    /// <summary>Detalhes do erro (ex.: <c>match_user_id</c> em <c>FACE_EXISTS</c>).</summary>
    [JsonPropertyName("info")]
    public RemoteEnrollInfo? Info { get; set; }

    /// <summary>Face já cadastrada para este outro usuário (erro <c>FACE_EXISTS</c>).</summary>
    [JsonIgnore]
    public long? MatchUserId => Info?.MatchUserId;

    /// <summary>Cartão lido.</summary>
    [JsonPropertyName("card_value")]
    public long? CardValue { get; set; }

    /// <summary>PIN digitado.</summary>
    [JsonPropertyName("pin_value"), JsonConverter(typeof(LenientStringConverter))]
    public string? PinValue { get; set; }

    /// <summary>Senha digitada.</summary>
    [JsonPropertyName("password_value"), JsonConverter(typeof(LenientStringConverter))]
    public string? PasswordValue { get; set; }

    /// <summary>Foto da face (JPEG).</summary>
    [JsonPropertyName("user_image")]
    public byte[]? UserImage { get; set; }

    /// <summary>Template facial, quando o equipamento está configurado para extrair (<c>extract_face_template</c>).</summary>
    [JsonPropertyName("face_template"), JsonConverter(typeof(LenientStringConverter))]
    public string? FaceTemplate { get; set; }

    /// <summary>Digital: 1 se dedo de pânico, 0 se comum.</summary>
    [JsonPropertyName("finger_type")]
    public int? FingerType { get; set; }

    /// <summary>Digital: imagem capturada em cada etapa do cadastro.</summary>
    [JsonPropertyName("fingerprints")]
    public List<CapturedImage>? Fingerprints { get; set; }

    /// <summary>
    /// Face capturada no modo online (<c>face_create</c>, campo <c>face</c>). No cadastro síncrono e no Monitor a foto
    /// vem em <see cref="UserImage"/>.
    /// </summary>
    [JsonPropertyName("face")]
    public CapturedImage? Face { get; set; }

    /// <summary>JSON original, para campos sem propriedade própria.</summary>
    [JsonIgnore]
    public JsonElement Raw { get; internal set; }

    /// <summary>Lança <see cref="ControlIdException"/> se o cadastro falhou.</summary>
    public RemoteEnrollResult EnsureSuccess() =>
        Success
            ? this
            : throw new ControlIdException(
                MatchUserId is { } match
                    ? $"Cadastro remoto falhou: {Error} (face já cadastrada para o usuário {match})."
                    : $"Cadastro remoto falhou: {Error ?? "sem mensagem do equipamento"}.");

    internal static RemoteEnrollResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            json = "{}";
        }

        var result = ControlIdJson.Deserialize<RemoteEnrollResult>(json);
        using var document = JsonDocument.Parse(json);
        result.Raw = document.RootElement.Clone();
        return result;
    }
}

/// <summary>Campo <c>info</c> do resultado do cadastro remoto.</summary>
public sealed class RemoteEnrollInfo
{
    /// <summary>Usuário que já tem a face informada.</summary>
    [JsonPropertyName("match_user_id")]
    public long? MatchUserId { get; set; }
}

/// <summary>Imagem capturada no cadastro remoto (etapa da digital ou foto da face).</summary>
public sealed class CapturedImage
{
    /// <summary>Largura em pixels.</summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>Altura em pixels.</summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>Imagem (decodificada do Base64).</summary>
    [JsonPropertyName("image")]
    public byte[]? Image { get; set; }
}
