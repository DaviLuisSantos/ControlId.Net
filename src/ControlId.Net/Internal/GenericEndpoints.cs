using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Internal;

/// <summary>Implementação de <see cref="IControlIdClient"/> compartilhada pelos clientes de acesso e REP.</summary>
internal static class GenericEndpoints
{
    public static Task<T> SendAsync<T>(ControlIdTransport transport, string endpoint, object? body, string? queryString, CancellationToken cancellationToken) =>
        transport.PostAsync<T>(EndpointPath.Normalize(endpoint), body, cancellationToken, EndpointPath.NormalizeQuery(queryString));

    public static Task<string> SendRawAsync(ControlIdTransport transport, string endpoint, object? body, string? queryString, CancellationToken cancellationToken) =>
        transport.PostRawAsync(EndpointPath.Normalize(endpoint), body, cancellationToken, EndpointPath.NormalizeQuery(queryString));

    public static Task<string> SendBytesAsync(
        ControlIdTransport transport, string endpoint, byte[] content, string contentType, string? queryString, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Informe o Content-Type.", nameof(contentType));
        }

        return transport.PostBytesAsync(EndpointPath.Normalize(endpoint), content, contentType, cancellationToken, EndpointPath.NormalizeQuery(queryString));
    }

    public static async Task<byte[]> DownloadAsync(
        ControlIdTransport transport, string endpoint, object? body, string? queryString, HttpMethod? method, CancellationToken cancellationToken)
    {
        var path = EndpointPath.Normalize(endpoint);
        var query = EndpointPath.NormalizeQuery(queryString);

        if (method is null || method == HttpMethod.Post)
        {
            var (content, _) = await transport.PostForBytesAsync(path, body, cancellationToken, query).ConfigureAwait(false);
            return content;
        }

        if (method == HttpMethod.Get)
        {
            return await transport.GetBytesAsync(path, cancellationToken, query).ConfigureAwait(false);
        }

        throw new ArgumentException("Use GET ou POST.", nameof(method));
    }
}
