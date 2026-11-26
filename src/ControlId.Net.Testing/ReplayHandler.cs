using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Testing;

/// <summary>
/// Handler que responde pelo <see cref="Cassette"/>, sem equipamento. As requisições são conferidas na ordem gravada:
/// método, endpoint e corpo (com os mesmos campos mascarados da gravação). Qualquer divergência lança
/// <see cref="InvalidOperationException"/> dizendo o que era esperado e o que chegou.
/// </summary>
public sealed class ReplayHandler : HttpMessageHandler
{
    private readonly Cassette _cassette;
    private readonly HashSet<string> _maskedFields;
    private int _position;

    /// <summary>Cria o reprodutor.</summary>
    /// <param name="cassette">Cassete a reproduzir.</param>
    /// <param name="extraMaskedFields">Mesmos campos adicionais usados na gravação.</param>
    public ReplayHandler(Cassette cassette, IEnumerable<string>? extraMaskedFields = null)
    {
        _cassette = cassette ?? throw new ArgumentNullException(nameof(cassette));
        _maskedFields = Cassette.BuildFields(extraMaskedFields);
    }

    /// <summary>Quantas trocas do cassete ainda não foram consumidas.</summary>
    public int Remaining
    {
        get
        {
            lock (_cassette)
            {
                return _cassette.Entries.Count - _position;
            }
        }
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

        string? actualBody = null;
        if (request.Content is not null)
        {
            var bytes = await ReadBytes(request.Content, cancellationToken).ConfigureAwait(false);
            actualBody = Cassette.IsText(request.Content.Headers.ContentType?.MediaType)
                ? Cassette.MaskJson(Encoding.UTF8.GetString(bytes), _maskedFields)
                : Convert.ToBase64String(bytes);
        }

        var actualPath = Cassette.MaskPath(request.RequestUri!, _maskedFields);

        CassetteEntry entry;
        int index;
        lock (_cassette)
        {
            index = _position;
            if (index >= _cassette.Entries.Count)
            {
                throw new InvalidOperationException(
                    $"Reprodução do cassete: a requisição {index + 1} ({request.Method.Method} {actualPath}) não existe no cassete, que tem só {_cassette.Entries.Count}.");
            }

            entry = _cassette.Entries[index];
            _position++;
        }

        var step = $"Reprodução do cassete, requisição {index + 1}";
        if (!string.Equals(entry.Method, request.Method.Method, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{step}: o método esperado era {entry.Method}, mas chegou {request.Method.Method} ({actualPath}).");
        }

        if (!string.Equals(entry.Path, actualPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{step}: o endpoint esperado era '{entry.Path}', mas chegou '{actualPath}'.");
        }

        if (!Cassette.BodiesEqual(entry.RequestBody, actualBody))
        {
            throw new InvalidOperationException(
                $"{step} ({entry.Method} {entry.Path}): o corpo divergiu. Esperado: {entry.RequestBody ?? "(vazio)"}. Recebido: {actualBody ?? "(vazio)"}.");
        }

        var bodyBytes = entry.ResponseBodyIsBase64 ? Convert.FromBase64String(entry.ResponseBody) : Encoding.UTF8.GetBytes(entry.ResponseBody);
        var content = new ByteArrayContent(bodyBytes);
        if (!string.IsNullOrEmpty(entry.ResponseContentType))
        {
            content.Headers.TryAddWithoutValidation("Content-Type", entry.ResponseContentType);
        }

        return new HttpResponseMessage((HttpStatusCode)entry.StatusCode) { Content = content, RequestMessage = request };
    }
}
