using System;
using Microsoft.Extensions.Logging;

namespace ControlId.Internal;

/// <summary>
/// Mensagens de log do núcleo (categoria <see cref="ControlIdTelemetry.LoggerCategory"/>). <c>device</c> é o
/// <c>host:porta</c> do equipamento. Nunca recebem sessão, login, senha nem o corpo das mensagens.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Login realizado em {Device}.")]
    public static partial void LoginSucceeded(ILogger logger, string device);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Login recusado por {Device} (HTTP {Status}).")]
    public static partial void LoginRefused(ILogger logger, string device, int status);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Sessão expirada em {Device} durante '{Command}'; refazendo o login.")]
    public static partial void SessionExpired(ILogger logger, string device, string command);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Nova tentativa {Attempt} de '{Command}' em {Device} ({Reason}).")]
    public static partial void Retrying(ILogger logger, string device, string command, int attempt, string reason);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "{Device} recusou '{Command}' com HTTP {Status} (código {DeviceCode}): {Message}")]
    public static partial void DeviceError(ILogger logger, string device, string command, int status, int? deviceCode, string? message);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "Falha de comunicação com {Device} em '{Command}'.")]
    public static partial void CommunicationFailed(ILogger logger, string device, string command, Exception exception);
}
