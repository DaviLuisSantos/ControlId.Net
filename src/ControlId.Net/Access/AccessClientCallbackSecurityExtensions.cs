using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

/// <summary>Configuração da segurança dos callbacks (Push, Monitor e modo online) que o equipamento envia ao servidor.</summary>
public static class AccessClientCallbackSecurityExtensions
{
    /// <summary>
    /// Configura no equipamento o token Bearer e/ou o certificado fixado usados nos callbacks para o servidor
    /// (<c>curl_bearer_token</c> e <c>curl_pinned_certificate</c>). Seção e formato a validar no equipamento (firmware 6.22.3+).
    /// </summary>
    /// <param name="client">Cliente do equipamento.</param>
    /// <param name="bearerToken">Token Bearer; <c>null</c> não altera, <c>""</c> limpa.</param>
    /// <param name="pinnedCertificate">Certificado fixado; <c>null</c> não altera, <c>""</c> limpa.</param>
    /// <param name="section">Seção da configuração (padrão <c>general</c>).</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    /// <exception cref="ArgumentException">Os dois valores são <c>null</c>, ou a seção está vazia.</exception>
    public static Task ConfigureCallbackSecurityAsync(this IAccessClient client, string? bearerToken, string? pinnedCertificate = null,
        string section = "general", CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(section))
        {
            throw new ArgumentException("A seção não pode ser vazia.", nameof(section));
        }

        if (bearerToken is null && pinnedCertificate is null)
        {
            throw new ArgumentException("Informe o token Bearer e/ou o certificado fixado.", nameof(bearerToken));
        }

        var values = new Dictionary<string, string>();
        if (bearerToken is not null)
        {
            values["curl_bearer_token"] = bearerToken;
        }

        if (pinnedCertificate is not null)
        {
            values["curl_pinned_certificate"] = pinnedCertificate;
        }

        return client.SetConfigurationAsync(new Dictionary<string, object> { [section] = values }, cancellationToken);
    }
}
