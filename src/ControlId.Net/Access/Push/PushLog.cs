using System;
using Microsoft.Extensions.Logging;

namespace ControlId.Access.Push;

/// <summary>
/// Mensagens de log do <see cref="PushServer"/> (categoria <see cref="ControlIdTelemetry.LoggerCategory"/>, EventIds 1101 a 1107).
/// Nunca recebem o corpo dos comandos nem dos resultados.
/// </summary>
internal static partial class PushLog
{
    [LoggerMessage(EventId = 1101, Level = LogLevel.Error, Message = "O IPushResultHandler falhou ao tratar o envio {CommandId} do equipamento {DeviceId}; a resposta ao equipamento segue normalmente.")]
    public static partial void ResultHandlerFailed(ILogger logger, Guid commandId, long deviceId, Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "O envio {CommandId} para o equipamento {DeviceId} expirou na fila sem ser entregue (CommandTtl) e foi descartado.")]
    public static partial void CommandExpired(ILogger logger, Guid commandId, long deviceId);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "O envio {CommandId} ao equipamento {DeviceId} ficou sem resultado e foi descartado ({Reason}).")]
    public static partial void CommandOrphaned(ILogger logger, Guid commandId, long deviceId, string reason);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "Limite de {Limit} equipamentos acompanhados atingido (MaxTrackedDevices): a última consulta de equipamentos novos não será registrada.")]
    public static partial void TrackingLimitReached(ILogger logger, int limit);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Warning, Message = "Requisição {Callback} do equipamento {DeviceId} sem uuid recusada (RequireUuid).")]
    public static partial void UuidMissing(ILogger logger, string callback, long deviceId);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Warning, Message = "Não foi possível remover do store o envio {CommandId} do equipamento {DeviceId} depois de cancelado ou vencido.")]
    public static partial void CancelRemoveFailed(ILogger logger, Guid commandId, long deviceId, Exception exception);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Information, Message = "A varredura do Push removeu {Count} envios vencidos.")]
    public static partial void Purged(ILogger logger, int count);
}
