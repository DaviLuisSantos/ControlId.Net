using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Testing;

/// <summary>
/// Handler que repassa as requisições ao handler interno (o equipamento real) e grava cada troca num <see cref="Cassette"/>.
/// Os campos sensíveis (<see cref="Cassette.DefaultMaskedFields"/> e os informados no construtor) saem mascarados.
/// </summary>
/// <remarks>
/// Use com o <c>HttpClient</c> entregue ao cliente: <c>new HttpClient(new RecordingHandler(new HttpClientHandler(), cassette))</c>.
/// Depois do teste, chame <see cref="Cassette.Save(string)"/>.
/// </remarks>
public sealed class RecordingHandler : DelegatingHandler
{
    private readonly Cassette _cassette;
    private readonly HashSet<string> _maskedFields;

    /// <summary>Cria o gravador.</summary>
    /// <param name="innerHandler">Handler que fala com o equipamento.</param>
    /// <param name="cassette">Cassete onde as trocas são gravadas.</param>
    /// <param name="extraMaskedFields">Campos JSON adicionais a mascarar, além dos padrão.</param>
    public RecordingHandler(HttpMessageHandler innerHandler, Cassette cassette, IEnumerable<string>? extraMaskedFields = null)
        : base(innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)))
    {
        _cassette = cassette ?? throw new ArgumentNullException(nameof(cassette));
        _maskedFields = Cassette.BuildFields(extraMaskedFields);
    }

    private static Task<byte[]> ReadBytes(HttpContent content, CancellationToken cancellationToken)
#if NET5_0_OR_GREATER
        => content.ReadAsByteArrayAsync(cancellationToken);
#else
        => content.ReadAsByteArrayAsync();
#endif

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        string? requestBody = null;
        var requestBase64 = false;
        if (request.Content is not null)
        {
            var bytes = await ReadBytes(request.Content, cancellationToken).ConfigureAwait(false);
            (requestBody, requestBase64) = Encode(bytes, request.Content.Headers.ContentType?.MediaType);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
        var responseBytes = await ReadBytes(response.Content, cancellationToken).ConfigureAwait(false);
        var (responseBody, responseBase64) = Encode(responseBytes, response.Content.Headers.ContentType?.MediaType);

        var entry = new CassetteEntry
        {
            Method = request.Method.Method,
            Path = Cassette.MaskPath(request.RequestUri!, _maskedFields),
            RequestBody = requestBody,
            RequestBodyIsBase64 = requestBase64,
            StatusCode = (int)response.StatusCode,
            ResponseContentType = response.Content.Headers.ContentType?.ToString(),
            ResponseBody = responseBody ?? string.Empty,
            ResponseBodyIsBase64 = responseBase64,
        };

        lock (_cassette.Entries)
        {
            _cassette.Entries.Add(entry);
        }

        return response;
    }

    private (string? Text, bool IsBase64) Encode(byte[] bytes, string? mediaType)
    {
        if (!Cassette.IsText(mediaType))
        {
            return (Convert.ToBase64String(bytes), true);
        }

        return (Cassette.MaskJson(Encoding.UTF8.GetString(bytes), _maskedFields), false);
    }
}
