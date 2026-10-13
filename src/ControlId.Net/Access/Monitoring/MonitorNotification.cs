using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access.Monitoring;

/// <summary>
/// Notificação enviada pelo equipamento ao servidor do Monitor (<c>POST {path}/{endpoint}</c>).
/// Use <see cref="MonitorNotifications.Parse"/> para obter o tipo concreto.
/// </summary>
public abstract class MonitorNotification
{
    /// <summary>Último segmento da rota chamada: <c>dao</c>, <c>door</c>, <c>catra_event</c>...</summary>
    [JsonIgnore]
    public string Endpoint { get; internal set; } = string.Empty;

    /// <summary>JSON original da notificação.</summary>
    [JsonIgnore]
    public JsonElement Raw { get; internal set; }

    /// <summary>ID do equipamento que enviou (ausente em <c>secbox</c>).</summary>
    [JsonPropertyName("device_id")]
    public long? DeviceId { get; set; }

    /// <summary>Data/hora do envio (Unix timestamp), quando informada.</summary>
    [JsonPropertyName("time")]
    public long? Time { get; set; }
}

/// <summary><c>dao</c>: inserção, alteração ou remoção em <c>access_logs</c>, <c>templates</c>, <c>cards</c> ou <c>alarm_logs</c>.</summary>
public sealed class DaoNotification : MonitorNotification
{
    /// <summary>Registros alterados.</summary>
    [JsonPropertyName("object_changes")]
    public List<ObjectChange> ObjectChanges { get; set; } = new();

    /// <summary>Atalho: logs de acesso inseridos nesta notificação.</summary>
    public IReadOnlyList<AccessLog> GetInsertedAccessLogs() =>
        ObjectChanges
            .Where(c => c.Object == "access_logs" && c.ChangeType == ObjectChangeType.Inserted)
            .Select(c => c.ValuesAs<AccessLog>())
            .ToList();
}

/// <summary>Um item de <see cref="DaoNotification.ObjectChanges"/>.</summary>
public sealed class ObjectChange
{
    /// <summary>Objeto alterado (<c>access_logs</c>, <c>cards</c>...).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    /// <summary>Tipo da alteração como veio do equipamento: <c>inserted</c>, <c>updated</c> ou <c>deleted</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Campos do registro. O equipamento envia os números como texto.</summary>
    [JsonPropertyName("values")]
    public JsonElement Values { get; set; }

    /// <summary><see cref="Type"/> como enum.</summary>
    [JsonIgnore]
    public ObjectChangeType ChangeType => Type switch
    {
        "inserted" => ObjectChangeType.Inserted,
        "updated" => ObjectChangeType.Updated,
        "deleted" => ObjectChangeType.Deleted,
        _ => ObjectChangeType.Unknown,
    };

    /// <summary>
    /// Converte <see cref="Values"/> para um modelo, ex.: <c>ValuesAs&lt;AccessLog&gt;()</c>. Campos numéricos vazios
    /// (<c>""</c>, como o <c>user_id</c> de um rosto não identificado) viram <c>null</c>.
    /// </summary>
    public T ValuesAs<T>() => Values.ValueKind == JsonValueKind.Undefined
        ? ControlIdJson.Deserialize<T>("{}", ControlIdJson.MonitorOptions)
        : ControlIdJson.Deserialize<T>(Values.GetRawText(), ControlIdJson.MonitorOptions);
}

/// <summary>Valor de <see cref="ObjectChange.ChangeType"/>.</summary>
public enum ObjectChangeType
{
    /// <summary>Tipo não reconhecido.</summary>
    Unknown,

    /// <summary>Registro inserido.</summary>
    Inserted,

    /// <summary>Registro alterado.</summary>
    Updated,

    /// <summary>Registro removido.</summary>
    Deleted,
}

/// <summary><c>door</c>: porta mudou de estado (relé ou sensor).</summary>
public sealed class DoorNotification : MonitorNotification
{
    /// <summary>Porta e estado atual.</summary>
    [JsonPropertyName("door")]
    public DoorState? Door { get; set; }

    /// <summary>Evento em <c>access_events</c> (exige <see cref="MonitorConfiguration.InformAccessEventId"/>).</summary>
    [JsonPropertyName("access_event_id")]
    public long? AccessEventId { get; set; }
}

/// <summary><c>secbox</c>: relé da SecBox mudou de estado.</summary>
public sealed class SecBoxNotification : MonitorNotification
{
    /// <summary>SecBox e estado atual.</summary>
    [JsonPropertyName("secbox")]
    public DoorState? SecBox { get; set; }

    /// <summary>Evento em <c>access_events</c> (exige <see cref="MonitorConfiguration.InformAccessEventId"/>).</summary>
    [JsonPropertyName("access_event_id")]
    public long? AccessEventId { get; set; }
}

/// <summary>Estado de porta ou SecBox.</summary>
public sealed class DoorState
{
    /// <summary>ID da porta (1 a 4) ou da SecBox.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary><c>true</c> se aberta.</summary>
    [JsonPropertyName("open")]
    public bool Open { get; set; }
}

/// <summary><c>catra_event</c> (iDBlock): giro confirmado ou desistência.</summary>
public sealed class TurnstileNotification : MonitorNotification
{
    /// <summary>Dados do evento.</summary>
    [JsonPropertyName("event")]
    public TurnstileEvent? Event { get; set; }

    /// <summary>Evento em <c>access_events</c> (exige <see cref="MonitorConfiguration.InformAccessEventId"/>).</summary>
    [JsonPropertyName("access_event_id")]
    public long? AccessEventId { get; set; }
}

/// <summary>Evento de catraca.</summary>
public sealed class TurnstileEvent
{
    /// <summary>Código do evento.</summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>Nome do evento, ex.: <c>TURN LEFT</c>, <c>TURN RIGHT</c>, <c>GIVE UP</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Data/hora (Unix timestamp).</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>Identificador do evento.</summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }
}

/// <summary><c>operation_mode</c>: equipamento ligou ou entrou/saiu do modo de contingência.</summary>
public sealed class OperationModeNotification : MonitorNotification
{
    /// <summary>Modo atual.</summary>
    [JsonPropertyName("operation_mode")]
    public OperationModeInfo? OperationMode { get; set; }
}

/// <summary>Detalhes de <see cref="OperationModeNotification"/>.</summary>
public sealed class OperationModeInfo
{
    /// <summary>Código do modo.</summary>
    [JsonPropertyName("mode")]
    public int Mode { get; set; }

    /// <summary>Nome do modo, ex.: <c>DEFAULT</c>.</summary>
    [JsonPropertyName("mode_name")]
    public string? ModeName { get; set; }

    /// <summary>Data/hora da mudança (Unix timestamp).</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>Quando ficou offline pela última vez (Unix timestamp); 0 quando acabou de ligar.</summary>
    [JsonPropertyName("last_offline")]
    public long LastOffline { get; set; }

    /// <summary>Modo de exceção, se houver (pode vir vazio ou <c>none</c>).</summary>
    [JsonPropertyName("exception_mode")]
    public string? ExceptionMode { get; set; }
}

/// <summary><c>device_is_alive</c>: sinal periódico no modo standalone.</summary>
public sealed class DeviceIsAliveNotification : MonitorNotification
{
    /// <summary>Quantidade de logs de acesso disponíveis no equipamento.</summary>
    [JsonPropertyName("access_logs")]
    public long AccessLogs { get; set; }
}

/// <summary><c>access_photo</c>: foto tirada na identificação (exige <see cref="MonitorConfiguration.EnablePhotoUpload"/>).</summary>
public sealed class AccessPhotoNotification : MonitorNotification
{
    /// <summary>Portal acionado.</summary>
    [JsonPropertyName("portal_id")]
    public long? PortalId { get; set; }

    /// <summary>Identificador que fez a leitura.</summary>
    [JsonPropertyName("identifier_id")]
    public long? IdentifierId { get; set; }

    /// <summary>Código do evento (ex.: 7 = acesso concedido).</summary>
    [JsonPropertyName("event")]
    public int? Event { get; set; }

    /// <summary>Usuário identificado (0 se nenhum).</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Foto em JPEG.</summary>
    [JsonPropertyName("access_photo")]
    public byte[]? AccessPhoto { get; set; }
}

/// <summary><c>usb_drive</c>: log de auditoria de exportação por pen drive.</summary>
public sealed class UsbDriveNotification : MonitorNotification
{
    /// <summary>Dados do evento.</summary>
    [JsonPropertyName("usb_drive")]
    public UsbDriveEvent? UsbDrive { get; set; }
}

/// <summary>Evento de <see cref="UsbDriveNotification"/>.</summary>
public sealed class UsbDriveEvent
{
    /// <summary>Descrição do evento.</summary>
    [JsonPropertyName("event")]
    public string? Event { get; set; }
}

/// <summary>
/// Resultado de um cadastro remoto assíncrono (<see cref="IAccessClient.StartRemoteEnrollAsync"/>): rotas
/// <c>card</c>, <c>user_image</c>, <c>face_template</c>, <c>template</c>, <c>pin</c> e <c>password</c>.
/// O iDFace 6.9.12 não envia esta notificação (veja <see cref="IAccessClient.StartRemoteEnrollAsync"/>).
/// </summary>
public sealed class RemoteEnrollNotification : MonitorNotification
{
    /// <summary>O que foi cadastrado, deduzido da rota.</summary>
    [JsonIgnore]
    public RemoteEnrollType EnrollType { get; internal set; }

    /// <summary>Dados do cadastro (mesma estrutura da resposta síncrona).</summary>
    [JsonIgnore]
    public RemoteEnrollResult Result { get; internal set; } = new();
}

/// <summary>
/// Notificação sem modelo próprio (rotas novas ou de outros firmwares).
/// O conteúdo fica em <see cref="MonitorNotification.Raw"/>.
/// </summary>
public sealed class UnknownMonitorNotification : MonitorNotification
{
}
