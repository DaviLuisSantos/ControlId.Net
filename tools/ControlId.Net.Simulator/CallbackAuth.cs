using System.Net.Http.Headers;

namespace ControlId.Simulator;

/// <summary>Autenticação dos callbacks do equipamento: com <c>general.curl_bearer_token</c> não vazio, manda <c>Authorization: Bearer</c>.</summary>
internal static class CallbackAuth
{
    /// <summary>Token configurado, ou <c>null</c> se vazio.</summary>
    public static string? Token(DeviceState state)
    {
        lock (state.Lock)
        {
            var token = state.GetConfiguration("general", "curl_bearer_token");
            return string.IsNullOrEmpty(token) ? null : token;
        }
    }

    /// <summary>Aplica o cabeçalho na requisição, se houver token.</summary>
    public static void Apply(HttpRequestMessage request, DeviceState state)
    {
        if (Token(state) is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
