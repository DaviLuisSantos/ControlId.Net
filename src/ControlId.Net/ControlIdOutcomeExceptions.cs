using System;
using System.Net;

namespace ControlId;

/// <summary>A leitura não teve resposta do equipamento no prazo.</summary>
public sealed class ControlIdTimeoutException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdTimeoutException(string message, Exception? innerException = null)
        : base(message, innerException: innerException)
    {
    }
}

/// <summary>Não foi possível conectar ao equipamento. O comando não saiu, então é seguro repeti-lo.</summary>
public sealed class ControlIdConnectionException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdConnectionException(string message, Exception? innerException = null)
        : base(message, innerException: innerException)
    {
    }
}

/// <summary>
/// Uma escrita foi enviada, mas não houve resposta (timeout ou conexão caída): não dá para saber se o equipamento a executou.
/// Confira o estado antes de repetir.
/// </summary>
public sealed class ControlIdOutcomeUnknownException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdOutcomeUnknownException(string message, string command, bool timedOut, Exception? innerException = null)
        : base(message, innerException: innerException)
    {
        Command = command;
        TimedOut = timedOut;
    }

    /// <summary>Comando enviado (por exemplo <c>create_objects</c>).</summary>
    public string Command { get; }

    /// <summary><c>true</c> se a falta de resposta foi por timeout; <c>false</c> se a conexão caiu.</summary>
    public bool TimedOut { get; }
}

/// <summary>O equipamento respondeu com erro.</summary>
public class ControlIdDeviceErrorException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdDeviceErrorException(string message, HttpStatusCode? statusCode, int? deviceErrorCode, string? deviceMessage, string? responseBody)
        : base(message, statusCode, deviceErrorCode, responseBody)
    {
        DeviceMessage = deviceMessage;
    }

    /// <summary>Texto do campo <c>error</c> do JSON de erro do equipamento, quando presente.</summary>
    public string? DeviceMessage { get; }
}

/// <summary>O equipamento (ou o firmware dele) não suporta o comando ou a tabela: "Invalid command", "Invalid table" ou 404 no <c>.fcgi</c>.</summary>
public sealed class ControlIdNotSupportedException : ControlIdDeviceErrorException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdNotSupportedException(string command, string? firmware, HttpStatusCode? statusCode, string? deviceMessage, string? responseBody)
        : base(
            $"O equipamento{(string.IsNullOrEmpty(firmware) ? string.Empty : $" (firmware {firmware})")} não suporta o comando '{command}'.",
            statusCode,
            null,
            deviceMessage,
            responseBody)
    {
        Command = command;
        Firmware = firmware;
    }

    /// <summary>Comando recusado.</summary>
    public string Command { get; }

    /// <summary>Firmware do equipamento, quando conhecido.</summary>
    public string? Firmware { get; }
}

/// <summary>Motivo da recusa de uma imagem de usuário.</summary>
public enum ImageRejectionReason
{
    /// <summary>Formato inválido (não é JPEG nem PNG, ou está corrompida).</summary>
    Format = 0,

    /// <summary>Dimensões fora do aceito pelo equipamento.</summary>
    Dimensions = 1,

    /// <summary>Tamanho em bytes acima do limite.</summary>
    Size = 2,
}

/// <summary>A biblioteca recusou a imagem antes de enviá-la, porque o equipamento não a aceitaria.</summary>
public sealed class ControlIdImageRejectedException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdImageRejectedException(ImageRejectionReason reason, long? userId, int? width, int? height, long byteCount, string message)
        : base(message)
    {
        Reason = reason;
        UserId = userId;
        Width = width;
        Height = height;
        ByteCount = byteCount;
    }

    /// <summary>Motivo da recusa.</summary>
    public ImageRejectionReason Reason { get; }

    /// <summary>Usuário a que a imagem se destinava, quando informado.</summary>
    public long? UserId { get; }

    /// <summary>Largura em pixels, quando foi possível ler.</summary>
    public int? Width { get; }

    /// <summary>Altura em pixels, quando foi possível ler.</summary>
    public int? Height { get; }

    /// <summary>Tamanho da imagem em bytes.</summary>
    public long ByteCount { get; }
}
