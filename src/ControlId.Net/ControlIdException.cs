using System;
using System.Net;

namespace ControlId;

/// <summary>
/// Erro retornado por um equipamento Control iD ou falha de comunicação com ele.
/// </summary>
public class ControlIdException : Exception
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdException(string message, HttpStatusCode? statusCode = null, int? deviceErrorCode = null, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        DeviceErrorCode = deviceErrorCode;
        ResponseBody = responseBody;
    }

    /// <summary>Status HTTP da resposta, quando houve resposta.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Campo <c>code</c> do JSON de erro do equipamento, quando presente.</summary>
    public int? DeviceErrorCode { get; }

    /// <summary>Corpo bruto da resposta, útil para diagnóstico.</summary>
    public string? ResponseBody { get; }
}

/// <summary>
/// Login recusado pelo equipamento (usuário ou senha incorretos).
/// </summary>
public sealed class ControlIdAuthenticationException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdAuthenticationException(string message, HttpStatusCode? statusCode = null, string? responseBody = null)
        : base(message, statusCode, null, responseBody)
    {
    }
}
