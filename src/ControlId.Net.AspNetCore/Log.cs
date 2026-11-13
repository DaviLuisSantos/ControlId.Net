using System;
using Microsoft.Extensions.Logging;

namespace ControlId.AspNetCore;

/// <summary>Mensagens de log das rotas do ASP.NET Core. Nunca recebem o corpo das notificações.</summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Resultado de Push do equipamento {DeviceId} sem comando pendente; ignorado.")]
    public static partial void PushResultIgnored(ILogger logger, long deviceId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Notificação '{Endpoint}' do Monitor inválida.")]
    public static partial void InvalidMonitorNotification(ILogger logger, Exception exception, string endpoint);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Resultado de cadastro remoto inválido em {Endpoint}.")]
    public static partial void InvalidEnrollResult(ILogger logger, Exception exception, string endpoint);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "new_rex_log inválido.")]
    public static partial void InvalidExitButton(ILogger logger, Exception exception);
}
