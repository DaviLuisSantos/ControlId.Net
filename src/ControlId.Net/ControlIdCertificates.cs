using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId;

/// <summary>Ajuda a fixar o certificado TLS de um equipamento (<see cref="ControlIdDeviceOptions.ServerCertificateSha256"/>).</summary>
public static class ControlIdCertificates
{
    /// <summary>
    /// Conecta ao equipamento e devolve o SHA-256 do certificado que ele apresenta, sem validá-lo. Rode uma vez, numa rede
    /// confiável, confira o valor (ex.: no navegador) e grave-o na configuração; a partir daí só esse certificado é aceito.
    /// </summary>
    /// <param name="baseAddress">Endereço HTTPS do equipamento, ex.: <c>https://192.168.0.130</c>.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    /// <exception cref="ControlIdException">O equipamento não respondeu ou não apresentou certificado.</exception>
    public static async Task<string> GetServerCertificateSha256Async(Uri baseAddress, CancellationToken cancellationToken = default)
    {
        if (baseAddress is null)
        {
            throw new ArgumentNullException(nameof(baseAddress));
        }

        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("O equipamento precisa ser acessado por HTTPS para ter certificado.", nameof(baseAddress));
        }

        string? sha256 = null;
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
        {
            if (certificate is not null)
            {
                sha256 = CertificatePinning.Sha256(certificate);
            }

            return true;
        };

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            using var response = await http.GetAsync(baseAddress, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ControlIdException($"Não foi possível conectar a {baseAddress}.", innerException: ex);
        }

        return sha256 ?? throw new ControlIdException($"{baseAddress} não apresentou certificado.");
    }
}
