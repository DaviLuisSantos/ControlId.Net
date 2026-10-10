using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access.Online;

/// <summary>
/// Decisão do servidor sobre uma tentativa de acesso: a resposta (<c>result</c>) dos eventos do modo online
/// e o corpo de <c>remote_user_authorization.fcgi</c>.
/// </summary>
public sealed class OnlineAuthorization
{
    /// <summary>Resultado: concedido, negado, não identificado...</summary>
    [JsonPropertyName("event")]
    public AccessEventType Event { get; set; }

    /// <summary>Usuário identificado.</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Nome exibido no display.</summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    /// <summary>
    /// <c>true</c> se o usuário tem foto: o equipamento então pede a imagem em <c>GET user_get_image.fcgi</c> ao servidor.
    /// </summary>
    [JsonPropertyName("user_image")]
    public bool? UserImage { get; set; }

    /// <summary>SHA-1 da foto, para o equipamento reaproveitar a que já tem em cache.</summary>
    [JsonPropertyName("user_image_hash")]
    public string? UserImageHash { get; set; }

    /// <summary>Portal do acesso.</summary>
    [JsonPropertyName("portal_id")]
    public long? PortalId { get; set; }

    /// <summary>Ações que o equipamento executa (abrir porta, SecBox, catraca). Veja <see cref="DeviceAction.OpenDoor"/>.</summary>
    [JsonPropertyName("actions")]
    public List<DeviceAction>? Actions { get; set; }

    /// <summary>1 para dedo de pânico, 0 para identificação comum.</summary>
    [JsonPropertyName("duress")]
    public int? Duress { get; set; }

    /// <summary>Mensagem exibida no display.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Concede o acesso executando <paramref name="actions"/> (ex.: <c>DeviceAction.OpenDoor(1)</c>).</summary>
    public static OnlineAuthorization Grant(long? userId, string? userName, params DeviceAction[] actions) => new()
    {
        Event = AccessEventType.AccessGranted,
        UserId = userId,
        UserName = userName,
        Actions = actions?.ToList() ?? new List<DeviceAction>(),
    };

    /// <summary>Nega o acesso.</summary>
    public static OnlineAuthorization Deny(long? userId = null, string? userName = null, string? message = null) => new()
    {
        Event = AccessEventType.AccessDenied,
        UserId = userId,
        UserName = userName,
        Message = message,
    };

    /// <summary>Credencial não reconhecida.</summary>
    public static OnlineAuthorization NotIdentified(string? message = null) => new()
    {
        Event = AccessEventType.NotIdentified,
        Message = message,
    };

    /// <summary>Corpo da resposta aos eventos de identificação: <c>{"result": {...}}</c>.</summary>
    public string ToResponseJson() => ControlIdJson.Serialize(new Dictionary<string, object> { ["result"] = this });
}
