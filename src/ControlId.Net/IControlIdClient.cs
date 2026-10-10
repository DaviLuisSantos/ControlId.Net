using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId;

/// <summary>
/// Chamadas genéricas a qualquer endpoint <c>*.fcgi</c> do equipamento, para o que ainda não tem método próprio.
/// A sessão vai na query string e o login é refeito se ela expirar, como nos métodos tipados.
/// </summary>
/// <remarks>
/// <para><c>endpoint</c> aceita <c>"message_to_screen"</c>, <c>"message_to_screen.fcgi"</c> ou <c>"/message_to_screen.fcgi"</c>.</para>
/// <para><c>queryString</c> é acrescentada após a sessão, sem <c>?</c> inicial e com os valores já escapados, ex.: <c>"user_id=7"</c>.
/// No REP, o <c>mode=671</c> não é incluído automaticamente nessas chamadas.</para>
/// <para>Erros HTTP do equipamento viram <see cref="ControlIdException"/>.</para>
/// </remarks>
public interface IControlIdClient : IDisposable
{
    /// <summary>POST com corpo JSON (<c>null</c> envia <c>{}</c>) e resposta JSON desserializada em <typeparamref name="T"/>.</summary>
    /// <example><code>var r = await client.SendAsync&lt;JsonElement&gt;("get_configuration", new { general = new[] { "language" } });</code></example>
    Task<T> SendAsync<T>(string endpoint, object? body = null, string? queryString = null, CancellationToken cancellationToken = default);

    /// <summary>POST com corpo JSON e resposta em texto (vazio quando o equipamento não devolve corpo).</summary>
    Task<string> SendRawAsync(string endpoint, object? body = null, string? queryString = null, CancellationToken cancellationToken = default);

    /// <summary>POST com corpo binário (ex.: <c>application/octet-stream</c> para logotipo e fotos) e resposta em texto.</summary>
    Task<string> SendBytesAsync(string endpoint, byte[] content, string contentType = "application/octet-stream", string? queryString = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chamada com resposta binária (ex.: imagens). Com <paramref name="method"/> GET o corpo é ignorado;
    /// o padrão é POST com <paramref name="body"/> em JSON.
    /// </summary>
    Task<byte[]> DownloadAsync(string endpoint, object? body = null, string? queryString = null, HttpMethod? method = null, CancellationToken cancellationToken = default);
}
