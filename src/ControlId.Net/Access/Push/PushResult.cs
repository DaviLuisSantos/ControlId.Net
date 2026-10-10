using System.Text.Json;
using ControlId.Internal;

namespace ControlId.Access.Push;

/// <summary>Resultado que o equipamento devolveu em <c>POST /result</c>.</summary>
public sealed class PushResult
{
    internal PushResult(long deviceId, string endpoint, JsonElement? response, string? error)
    {
        DeviceId = deviceId;
        Endpoint = endpoint;
        Response = response;
        Error = error;
    }

    /// <summary>Equipamento que executou.</summary>
    public long DeviceId { get; }

    /// <summary>Endpoint executado.</summary>
    public string Endpoint { get; }

    /// <summary>Resposta do comando (mesmo formato da API HTTP). Ausente em erro ou em comandos sem retorno.</summary>
    public JsonElement? Response { get; }

    /// <summary>Mensagem de erro do equipamento, ou <c>null</c> se deu certo.</summary>
    public string? Error { get; }

    /// <summary><c>true</c> se o equipamento não informou erro.</summary>
    public bool Success => Error is null;

    /// <summary>Lança <see cref="ControlIdException"/> se o equipamento informou erro.</summary>
    public PushResult EnsureSuccess() =>
        Success ? this : throw new ControlIdException($"'{Endpoint}' falhou via Push no equipamento {DeviceId}: {Error}");

    /// <summary>Converte <see cref="Response"/> para <typeparamref name="T"/> (lança se houve erro).</summary>
    public T ResponseAs<T>()
    {
        EnsureSuccess();
        return ControlIdJson.Deserialize<T>(Response is { ValueKind: not JsonValueKind.Null } response ? response.GetRawText() : "{}");
    }
}
