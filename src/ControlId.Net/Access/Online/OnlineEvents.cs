using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ControlId.Access.Online;

/// <summary>Credencial apresentada numa tentativa de acesso no modo online.</summary>
public enum OnlineIdentificationKind
{
    /// <summary><c>new_user_identified</c> (modo Pro): o equipamento já identificou o usuário.</summary>
    UserIdentified,

    /// <summary><c>new_card</c> (Enterprise): cartão de proximidade.</summary>
    Card,

    /// <summary><c>new_qrcode</c> (Enterprise): QR Code.</summary>
    QrCode,

    /// <summary><c>new_uhf_tag</c> (Enterprise): tag UHF.</summary>
    UhfTag,

    /// <summary><c>new_user_id_and_password</c> (Enterprise): ID e senha digitados.</summary>
    UserIdAndPassword,

    /// <summary><c>new_biometric_image</c> (Enterprise): imagem da digital.</summary>
    BiometricImage,

    /// <summary><c>new_biometric_template</c> (Enterprise): template da digital (formato Innovatrics).</summary>
    BiometricTemplate,
}

/// <summary>
/// Tentativa de acesso enviada pelo equipamento no modo online. O servidor responde com uma
/// <see cref="OnlineAuthorization"/>. Use <see cref="OnlineEvents.Parse"/> para obter o tipo concreto.
/// </summary>
public abstract class OnlineIdentificationEvent
{
    private protected OnlineIdentificationEvent(OnlineIdentificationKind kind, OnlineValues values)
    {
        Kind = kind;
        Values = values.All;
        DeviceId = values.Long("device_id");
        IdentifierId = values.Long("identifier_id");
        Time = values.Long("time");
        PortalId = values.Long("portal_id");
        Uuid = values.Text("uuid");
    }

    /// <summary>Tipo de credencial.</summary>
    public OnlineIdentificationKind Kind { get; }

    /// <summary>Equipamento que enviou.</summary>
    public long? DeviceId { get; }

    /// <summary>Identificador usado (veja <see cref="IdentifierName"/>).</summary>
    public long? IdentifierId { get; }

    /// <summary>
    /// <see cref="IdentifierId"/> decodificado como a documentação descreve: 3 letras ASCII + número,
    /// ex.: <c>2003398144</c> → <c>"win0"</c> (Wiegand 0).
    /// </summary>
    public string? IdentifierName => IdentifierId is { } id ? DecodeIdentifier(id) : null;

    /// <summary>Momento da tentativa (Unix timestamp).</summary>
    public long? Time { get; }

    /// <summary><see cref="Time"/> como data, em UTC.</summary>
    public DateTimeOffset? TimeUtc => Time is { } time ? DateTimeOffset.FromUnixTimeSeconds(time) : null;

    /// <summary>Portal da tentativa.</summary>
    public long? PortalId { get; }

    /// <summary>Identificador único da tentativa.</summary>
    public string? Uuid { get; }

    /// <summary>Todos os campos recebidos, como texto (para os que não têm propriedade).</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    internal static string DecodeIdentifier(long id)
    {
        var value = unchecked((uint)id);
        var text = new StringBuilder(4);
        for (var shift = 24; shift >= 8; shift -= 8)
        {
            var c = (char)((value >> shift) & 0xFF);
            if (c != '\0')
            {
                text.Append(c);
            }
        }

        return text.Append((value & 0xFF).ToString(CultureInfo.InvariantCulture)).ToString();
    }
}

/// <summary><c>new_user_identified</c> (modo Pro): usuário identificado localmente; o servidor decide se libera.</summary>
public sealed class UserIdentifiedEvent : OnlineIdentificationEvent
{
    internal UserIdentifiedEvent(OnlineValues v)
        : base(OnlineIdentificationKind.UserIdentified, v)
    {
        Event = v.Long("event") is { } e ? (AccessEventType)e : null;
        UserId = v.Long("user_id");
        UserName = v.Text("user_name");
        Duress = v.Flag("duress");
        FaceMask = v.Flag("face_mask");
        CardValue = v.Long("card_value");
        QrCodeValue = v.Text("qrcode_value");
        UhfTag = v.Text("uhf_tag");
        PinValue = v.Text("pin_value");
        Password = v.Text("password");
        UserHasImage = v.Long("user_has_image") is { } image ? image == 1 : null;
        Confidence = v.Long("confidence");
        LogTypeId = v.Long("log_type_id");
        BlockReadData = v.Text("block_read_data");
        BlockReadError = v.Long("block_read_error");
        RemoteInterlockState = v.Text("remote_interlock_state");
    }

    /// <summary>Resultado da identificação no equipamento (ex.: <see cref="AccessEventType.NotIdentified"/>).</summary>
    public AccessEventType? Event { get; }

    /// <summary>Usuário identificado.</summary>
    public long? UserId { get; }

    /// <summary>Nome do usuário.</summary>
    public string? UserName { get; }

    /// <summary>Dedo de pânico.</summary>
    public bool? Duress { get; }

    /// <summary>Usuário de máscara (faciais).</summary>
    public bool? FaceMask { get; }

    /// <summary>Cartão usado.</summary>
    public long? CardValue { get; }

    /// <summary>QR Code usado.</summary>
    public string? QrCodeValue { get; }

    /// <summary>Tag UHF lida.</summary>
    public string? UhfTag { get; }

    /// <summary>PIN usado.</summary>
    public string? PinValue { get; }

    /// <summary>Senha informada.</summary>
    public string? Password { get; }

    /// <summary>Usuário tem foto no equipamento (a API envia 1 = sim, 2 = não).</summary>
    public bool? UserHasImage { get; }

    /// <summary>Confiança da identificação facial.</summary>
    public long? Confidence { get; }

    /// <summary>Tipo de batida (modo ponto).</summary>
    public long? LogTypeId { get; }

    /// <summary>Bloco lido do cartão MIFARE, em hexadecimal.</summary>
    public string? BlockReadData { get; }

    /// <summary>Diferente de 0 se houve erro na leitura do bloco MIFARE.</summary>
    public long? BlockReadError { get; }

    /// <summary>Estado das portas no intertravamento remoto.</summary>
    public string? RemoteInterlockState { get; }
}

/// <summary><c>new_card</c> (Enterprise): o servidor identifica o dono do cartão.</summary>
public sealed class CardEvent : OnlineIdentificationEvent
{
    internal CardEvent(OnlineValues v)
        : base(OnlineIdentificationKind.Card, v)
    {
        CardValue = v.Long("card_value");
        Panic = v.Flag("panic");
        BlockReadData = v.Text("block_read_data");
        BlockReadError = v.Long("block_read_error");
    }

    /// <summary>Número do cartão.</summary>
    public long? CardValue { get; }

    /// <summary>Cartão de pânico.</summary>
    public bool? Panic { get; }

    /// <summary>Bloco lido do cartão MIFARE, em hexadecimal.</summary>
    public string? BlockReadData { get; }

    /// <summary>Diferente de 0 se houve erro na leitura do bloco MIFARE.</summary>
    public long? BlockReadError { get; }
}

/// <summary><c>new_qrcode</c> (Enterprise).</summary>
public sealed class QrCodeEvent : OnlineIdentificationEvent
{
    internal QrCodeEvent(OnlineValues v)
        : base(OnlineIdentificationKind.QrCode, v) => QrCodeValue = v.Text("qrcode_value");

    /// <summary>Conteúdo do QR Code.</summary>
    public string? QrCodeValue { get; }
}

/// <summary><c>new_uhf_tag</c> (Enterprise).</summary>
public sealed class UhfTagEvent : OnlineIdentificationEvent
{
    internal UhfTagEvent(OnlineValues v)
        : base(OnlineIdentificationKind.UhfTag, v) => UhfTag = v.Text("uhf_tag");

    /// <summary>Valor lido da tag.</summary>
    public string? UhfTag { get; }
}

/// <summary><c>new_user_id_and_password</c> (Enterprise).</summary>
public sealed class UserIdAndPasswordEvent : OnlineIdentificationEvent
{
    internal UserIdAndPasswordEvent(OnlineValues v)
        : base(OnlineIdentificationKind.UserIdAndPassword, v)
    {
        UserId = v.Long("user_id");
        Password = v.Text("password");
    }

    /// <summary>ID digitado.</summary>
    public long? UserId { get; }

    /// <summary>Senha digitada.</summary>
    public string? Password { get; }
}

/// <summary><c>new_biometric_image</c> e <c>new_biometric_template</c> (Enterprise): o servidor faz a identificação biométrica.</summary>
public sealed class BiometricEvent : OnlineIdentificationEvent
{
    internal BiometricEvent(OnlineIdentificationKind kind, OnlineValues v, byte[] data)
        : base(kind, v)
    {
        Data = data;
        Width = v.Long("width") is { } width ? (int)width : null;
        Height = v.Long("height") is { } height ? (int)height : null;
        Variance = v.Long("variance");
    }

    /// <summary>
    /// Imagem (1 byte por pixel, tons de cinza, <see cref="Width"/> × <see cref="Height"/>) ou template
    /// Innovatrics, conforme <see cref="OnlineIdentificationEvent.Kind"/>.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>Largura da imagem (só imagem).</summary>
    public int? Width { get; }

    /// <summary>Altura da imagem (só imagem).</summary>
    public int? Height { get; }

    /// <summary>Qualidade da captura.</summary>
    public long? Variance { get; }
}

/// <summary>Interpreta os eventos de identificação do modo online.</summary>
public static class OnlineEvents
{
    /// <summary>Rotas de identificação (sem o <c>.fcgi</c>), na ordem da documentação.</summary>
    public static IReadOnlyList<string> IdentificationEndpoints { get; } = new[]
    {
        "new_user_identified", "new_card", "new_qrcode", "new_uhf_tag", "new_user_id_and_password", "new_biometric_image", "new_biometric_template",
    };

    /// <summary>
    /// Converte a requisição do equipamento no evento tipado.
    /// </summary>
    /// <param name="endpoint">Rota chamada, com ou sem <c>.fcgi</c> e caminho (ex.: <c>/api/new_card.fcgi</c>).</param>
    /// <param name="values">Campos do formulário e da query string (a biometria manda tudo na query).</param>
    /// <param name="body">Corpo binário da biometria; ignorado nos demais.</param>
    /// <returns><c>null</c> se a rota não for de identificação.</returns>
    public static OnlineIdentificationEvent? Parse(string endpoint, IEnumerable<KeyValuePair<string, string>> values, byte[]? body = null)
    {
        if (endpoint is null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        var v = new OnlineValues(values ?? throw new ArgumentNullException(nameof(values)));
        return Name(endpoint) switch
        {
            "new_user_identified" => new UserIdentifiedEvent(v),
            "new_card" => new CardEvent(v),
            "new_qrcode" => new QrCodeEvent(v),
            "new_uhf_tag" => new UhfTagEvent(v),
            "new_user_id_and_password" => new UserIdAndPasswordEvent(v),
            "new_biometric_image" => new BiometricEvent(OnlineIdentificationKind.BiometricImage, v, body ?? Array.Empty<byte>()),
            "new_biometric_template" => new BiometricEvent(OnlineIdentificationKind.BiometricTemplate, v, body ?? Array.Empty<byte>()),
            _ => null,
        };
    }

    /// <summary>
    /// Rotas em que o equipamento em modo online entrega o resultado do cadastro remoto (sem o <c>.fcgi</c>).
    /// </summary>
    public static IReadOnlyList<string> EnrollEndpoints { get; } = new[]
    {
        "card_create", "fingerprint_create", "template_create", "face_create", "pin_create", "password_create",
    };

    /// <summary>
    /// Interpreta o resultado de um cadastro remoto feito em modo online (corpo JSON de <c>card_create</c>,
    /// <c>face_create</c>, <c>fingerprint_create</c>, <c>template_create</c>, <c>pin_create</c> ou <c>password_create</c>).
    /// </summary>
    /// <returns><c>null</c> se a rota não for de cadastro.</returns>
    /// <exception cref="ControlIdException">O corpo não é um JSON válido.</exception>
    public static OnlineEnrollEvent? ParseEnroll(string endpoint, string json)
    {
        if (endpoint is null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        RemoteEnrollType? type = Name(endpoint) switch
        {
            "card_create" => RemoteEnrollType.Card,
            "fingerprint_create" or "template_create" => RemoteEnrollType.Biometry,
            "face_create" => RemoteEnrollType.Face,
            "pin_create" => RemoteEnrollType.Pin,
            "password_create" => RemoteEnrollType.Password,
            _ => null,
        };

        if (type is null)
        {
            return null;
        }

        try
        {
            return new OnlineEnrollEvent(Name(endpoint), type.Value, RemoteEnrollResult.Parse(json));
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ControlIdException($"Resultado de cadastro '{Name(endpoint)}' inválido.", responseBody: json, innerException: ex);
        }
    }

    /// <summary>Decodifica um corpo <c>application/x-www-form-urlencoded</c>.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ParseForm(string body)
    {
        var result = new List<KeyValuePair<string, string>>();
        if (string.IsNullOrEmpty(body))
        {
            return result;
        }

        foreach (var pair in body.TrimStart('?').Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var separator = pair.IndexOf('=');
            var key = separator >= 0 ? pair.Substring(0, separator) : pair;
            var value = separator >= 0 ? pair.Substring(separator + 1) : string.Empty;
            result.Add(new KeyValuePair<string, string>(Unescape(key), Unescape(value)));
        }

        return result;
    }

    private static string Unescape(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static string Name(string endpoint)
    {
        var path = endpoint;
        var query = path.IndexOf('?');
        if (query >= 0)
        {
            path = path.Substring(0, query);
        }

        path = path.TrimEnd('/');
        var slash = path.LastIndexOf('/');
        if (slash >= 0)
        {
            path = path.Substring(slash + 1);
        }

        return path.EndsWith(".fcgi", StringComparison.OrdinalIgnoreCase) ? path.Substring(0, path.Length - 5) : path;
    }
}

/// <summary>
/// Resultado de um cadastro remoto entregue pelo equipamento em modo online
/// (<c>card_create</c>, <c>face_create</c>, <c>fingerprint_create</c>, <c>template_create</c>, <c>pin_create</c>, <c>password_create</c>).
/// </summary>
public sealed class OnlineEnrollEvent
{
    internal OnlineEnrollEvent(string endpoint, RemoteEnrollType type, RemoteEnrollResult result)
    {
        Endpoint = endpoint;
        EnrollType = type;
        Result = result;
    }

    /// <summary>Rota chamada, sem <c>.fcgi</c> (distingue <c>fingerprint_create</c> de <c>template_create</c>).</summary>
    public string Endpoint { get; }

    /// <summary>O que foi cadastrado.</summary>
    public RemoteEnrollType EnrollType { get; }

    /// <summary>
    /// Dados do cadastro: <see cref="RemoteEnrollResult.CardValue"/>, <see cref="RemoteEnrollResult.Face"/>,
    /// <see cref="RemoteEnrollResult.Fingerprints"/>, <see cref="RemoteEnrollResult.PinValue"/>... O formato de
    /// <c>template_create</c> não é documentado: use <see cref="RemoteEnrollResult.Raw"/>.
    /// </summary>
    public RemoteEnrollResult Result { get; }
}

/// <summary>Campos recebidos, com leitura tolerante (ausente ou inválido = <c>null</c>).</summary>
internal sealed class OnlineValues
{
    public OnlineValues(IEnumerable<KeyValuePair<string, string>> values)
    {
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            all[pair.Key] = pair.Value; // o último vence (formulário depois da query)
        }

        All = all;
    }

    public IReadOnlyDictionary<string, string> All { get; }

    public string? Text(string key) => All.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    public long? Long(string key) =>
        Text(key) is { } text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    public bool? Flag(string key) => Text(key)?.ToLowerInvariant() switch
    {
        "1" or "true" => true,
        "0" or "false" => false,
        _ => null,
    };
}
