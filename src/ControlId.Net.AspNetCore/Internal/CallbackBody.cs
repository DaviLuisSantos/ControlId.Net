using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace ControlId.AspNetCore.Internal;

/// <summary>Leitura do corpo dos callbacks com limite de tamanho.</summary>
internal static class CallbackBody
{
    private const int ChunkSize = 16 * 1024;

    /// <summary>
    /// Lê o corpo inteiro, desde que caiba em <paramref name="limit"/> bytes. Devolve <see langword="null"/> quando passa do
    /// limite, sem ler o resto: confere o <c>Content-Length</c>, ajusta o limite do servidor quando ele permite e, em último
    /// caso, para a leitura no primeiro byte excedente.
    /// </summary>
    public static async Task<byte[]?> ReadAsync(HttpContext context, long limit)
    {
        var request = context.Request;
        if (request.ContentLength is { } declared && declared > limit)
        {
            return null;
        }

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = limit;
        }

        var capacity = request.ContentLength is { } length && length <= int.MaxValue ? (int)length : 0;
        using var buffer = new MemoryStream(capacity);
        var chunk = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            while (true)
            {
                // Pede no máximo um byte além do limite: o suficiente para saber que passou.
                var wanted = (int)Math.Min(chunk.Length, limit - buffer.Length + 1);
                var read = await request.Body.ReadAsync(chunk.AsMemory(0, wanted), context.RequestAborted).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
                if (buffer.Length > limit)
                {
                    return null;
                }
            }
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // O servidor aplicou o limite ajustado acima.
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return buffer.ToArray();
    }

    /// <summary>Texto UTF-8 do corpo, sem a marca de ordem de bytes (BOM), como o <c>StreamReader</c> faria.</summary>
    public static string ToText(byte[] body)
    {
        var preamble = Encoding.UTF8.Preamble;
        var start = body.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return Encoding.UTF8.GetString(body, start, body.Length - start);
    }
}
