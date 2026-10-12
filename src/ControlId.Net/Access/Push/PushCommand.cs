using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace ControlId.Access.Push;

/// <summary>
/// Comando entregue ao equipamento na resposta de <c>GET /push</c>.
/// Qualquer endpoint da API serve (sem o <c>.fcgi</c>); as fábricas cobrem os mais comuns.
/// </summary>
public sealed class PushCommand
{
    /// <summary>Cria um comando para um endpoint da API, ex.: <c>new PushCommand("set_configuration", new { ... })</c>.</summary>
    public PushCommand(string endpoint, object? body = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Informe o endpoint.", nameof(endpoint));
        }

        Endpoint = endpoint.EndsWith(".fcgi", StringComparison.Ordinal) ? endpoint.Substring(0, endpoint.Length - 5) : endpoint;
        Body = body;
    }

    /// <summary>Método HTTP que o equipamento usa para executar o comando. Padrão: POST.</summary>
    [JsonPropertyName("verb")]
    public string Verb { get; set; } = "POST";

    /// <summary>Endpoint da API, sem <c>.fcgi</c>.</summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; }

    /// <summary>Corpo do comando. Para <c>application/octet-stream</c>, uma string Base64.</summary>
    [JsonPropertyName("body")]
    public object? Body { get; set; }

    /// <summary>Tipo do corpo. Padrão: <c>application/json</c>.</summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/json";

    /// <summary>Query string adicional, ex.: <c>user_id=7</c>.</summary>
    [JsonPropertyName("queryString")]
    public string? QueryString { get; set; }

    /// <summary><c>load_objects</c>.</summary>
    public static PushCommand LoadObjects(LoadObjectsRequest request) =>
        new("load_objects", request ?? throw new ArgumentNullException(nameof(request)));

    /// <summary><c>create_objects</c>.</summary>
    public static PushCommand CreateObjects<T>(string @object, IEnumerable<T> values) =>
        new("create_objects", new Dictionary<string, object?> { ["object"] = @object, ["values"] = values.ToList() });

    /// <summary><c>modify_objects</c>.</summary>
    public static PushCommand ModifyObjects(string @object, object values, object where) =>
        new("modify_objects", new Dictionary<string, object?> { ["object"] = @object, ["values"] = values, ["where"] = where });

    /// <summary><c>destroy_objects</c>.</summary>
    public static PushCommand DestroyObjects(string @object, object where) =>
        new("destroy_objects", new Dictionary<string, object?> { ["object"] = @object, ["where"] = where });

    /// <summary><c>execute_actions</c>, ex.: <c>ExecuteActions(new DeviceAction("door", "door=1"))</c>.</summary>
    public static PushCommand ExecuteActions(params DeviceAction[] actions) =>
        new("execute_actions", new Dictionary<string, object> { ["actions"] = actions });

    /// <summary><c>set_configuration</c>.</summary>
    public static PushCommand SetConfiguration(object configuration) =>
        new("set_configuration", configuration ?? throw new ArgumentNullException(nameof(configuration)));

    /// <summary><c>message_to_screen</c>; sem <paramref name="duration"/>, a mensagem fica até ser trocada.</summary>
    public static PushCommand ShowMessage(string message, TimeSpan? duration = null) =>
        new("message_to_screen", AccessClient.MessageBody(message ?? throw new ArgumentNullException(nameof(message)), duration));

    /// <summary><c>buzzer_buzz</c> (até 3 s por comando).</summary>
    public static PushCommand Buzz(TimeSpan duration, int frequency = 4000, int dutyCycle = 50) =>
        new("buzzer_buzz", AccessClient.BuzzBody(duration, frequency, dutyCycle));

    /// <summary><c>reboot</c>.</summary>
    public static PushCommand Reboot() => new("reboot");

    /// <summary>
    /// <c>remote_enroll</c> síncrono: o resultado vem no <c>/result</c>; leia com
    /// <c>result.ResponseAs&lt;RemoteEnrollResult&gt;()</c>.
    /// </summary>
    public static PushCommand RemoteEnroll(RemoteEnrollRequest request) =>
        new("remote_enroll", (request ?? throw new ArgumentNullException(nameof(request))).ToBody(sync: true));

    /// <summary><c>cancel_remote_enroll</c>.</summary>
    public static PushCommand CancelRemoteEnroll() => new("cancel_remote_enroll");

    /// <summary><c>user_set_image</c>: foto (JPEG, menor que 1 MB) enviada como octet-stream em Base64.</summary>
    public static PushCommand SetUserImage(long userId, byte[] jpeg, bool rejectExistingFace = false, DateTimeOffset? timestamp = null) =>
        new("user_set_image", Convert.ToBase64String(jpeg ?? throw new ArgumentNullException(nameof(jpeg))))
        {
            ContentType = "application/octet-stream",
            QueryString = "user_id=" + userId.ToString(CultureInfo.InvariantCulture)
                + "&timestamp=" + (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                + "&match=" + (rejectExistingFace ? "1" : "0"),
        };
}
