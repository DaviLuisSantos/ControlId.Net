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

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "Callback {Route} recusado por token Bearer ausente ou inválido (equipamento {DeviceId}).")]
    public static partial void CallbackUnauthorized(ILogger logger, string route, long? deviceId);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "Callback {Route} recusado com 401: a rota não exige token e os callbacks são fechados por padrão. "
            + "Encadeie .RequireControlIdToken(...) no MapControlId* e grave o mesmo token no equipamento (ConfigureCallbackSecurityAsync), "
            + "ou, só em rede isolada, .AllowAnonymousControlIdCallbacks(). Este aviso sai uma vez por rota.")]
    public static partial void CallbackClosed(ILogger logger, string route);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Warning,
        Message = "Callback {Route} recusado com 403: device_id da query ({QueryDeviceId}) diferente do corpo ({BodyDeviceId}).")]
    public static partial void CallbackDeviceMismatch(ILogger logger, string route, long queryDeviceId, long bodyDeviceId);

    [LoggerMessage(EventId = 2008, Level = LogLevel.Error, Message = "Handler {HandlerType} do Monitor falhou na notificação '{Endpoint}'.")]
    public static partial void MonitorHandlerFailed(ILogger logger, Exception exception, string handlerType, string endpoint);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Warning,
        Message = "Callback {Route} recusado com 403: equipamento {DeviceId} desconhecido (ControlIdCallbackOptions.IsKnownDevice).")]
    public static partial void CallbackUnknownDevice(ILogger logger, string route, long deviceId);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "Callback {Route} recusado com 413: corpo acima de {Limit} bytes (ControlIdCallbackOptions.MaxRequestBodySize).")]
    public static partial void CallbackBodyTooLarge(ILogger logger, string route, long limit);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Error,
        Message = "IPushResultHandler {HandlerType} falhou ao tratar o envio {CommandId} do equipamento {DeviceId}; os demais handlers seguem.")]
    public static partial void PushResultHandlerFailed(ILogger logger, Exception exception, string handlerType, Guid commandId, long deviceId);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Error, Message = "A varredura do Push (PurgeExpiredAsync) falhou; nova tentativa no próximo intervalo.")]
    public static partial void PushPurgeFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Information, Message = "Varredura automática do Push desligada (PushServerOptions.PurgeInterval = null).")]
    public static partial void PushPurgeDisabled(ILogger logger);
}
