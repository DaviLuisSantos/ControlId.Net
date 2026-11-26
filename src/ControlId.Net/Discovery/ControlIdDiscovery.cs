using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;

namespace ControlId.Discovery;

/// <summary>
/// Descoberta de equipamentos Control iD numa lista de endereços (por exemplo, uma sub-rede): tenta o login com as
/// credenciais informadas e lê modelo, firmware, número de série e MAC.
/// </summary>
/// <remarks>
/// Cada endereço é sondado em paralelo (<see cref="DiscoveryOptions.MaxParallelism"/>), sem novas tentativas e sem
/// circuit breaker. Em endereços <c>https</c> o certificado do equipamento não é validado: a varredura procura
/// equipamentos que ainda não têm o certificado fixado. A senha nunca vai para log, exceção nem resultado.
/// </remarks>
public static class ControlIdDiscovery
{
    private const string FactoryLogin = "admin";
    private const string FactoryPassword = "admin";

    /// <summary>
    /// Sonda os endereços e devolve um resultado por endereço, na mesma ordem em que foram informados.
    /// </summary>
    /// <param name="addresses">Endereços base (<c>http://192.168.0.129</c>); veja <see cref="ExpandCidr"/>.</param>
    /// <param name="credentials">Credenciais a tentar, na ordem. Podem ser vazias se <see cref="DiscoveryOptions.TryFactoryDefaults"/> estiver ligado.</param>
    /// <param name="options">Ajustes da varredura; <c>null</c> usa os padrões.</param>
    /// <param name="cancellationToken">Cancelamento: interrompe a varredura e lança <see cref="OperationCanceledException"/>.</param>
    public static Task<IReadOnlyList<DiscoveredDevice>> ScanAsync(
        IEnumerable<Uri> addresses,
        IEnumerable<DiscoveryCredential> credentials,
        DiscoveryOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ScanCoreAsync(addresses, credentials, options, null, cancellationToken);

    /// <summary>
    /// Expande uma faixa CIDR IPv4 (<c>192.168.0.0/24</c>) nos endereços dos equipamentos. Nas faixas até /30 os
    /// endereços de rede e de broadcast ficam de fora; /31 devolve os dois, /32 devolve um.
    /// </summary>
    /// <param name="cidr">Faixa IPv4 com prefixo (<c>192.168.0.0/24</c>).</param>
    /// <param name="port">Porta do servidor web do equipamento. Padrão: 80.</param>
    /// <param name="scheme"><c>http</c> (padrão) ou <c>https</c>.</param>
    public static IEnumerable<Uri> ExpandCidr(string cidr, int port = 80, string scheme = "http")
    {
        if (cidr is null)
        {
            throw new ArgumentNullException(nameof(cidr));
        }

        if (port < 1 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "A porta precisa estar entre 1 e 65535.");
        }

        if (!string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) && !string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("O esquema precisa ser 'http' ou 'https'.", nameof(scheme));
        }

        var slash = cidr.IndexOf('/');
        if (slash <= 0 || slash == cidr.Length - 1
            || !int.TryParse(cidr.Substring(slash + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var prefix)
            || prefix < 0 || prefix > 32)
        {
            throw new ArgumentException($"CIDR inválido: '{cidr}'. Use o formato 192.168.0.0/24.", nameof(cidr));
        }

        var text = cidr.Substring(0, slash);
        if (text.Split('.').Length != 4
            || !IPAddress.TryParse(text, out var ip)
            || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException($"CIDR inválido: '{cidr}'. Só há suporte a IPv4 (192.168.0.0/24).", nameof(cidr));
        }

        return Expand(ip, prefix, port, scheme.ToLowerInvariant());
    }

    // Núcleo da varredura; handlerFactory só existe para os testes (sem rede).
    internal static async Task<IReadOnlyList<DiscoveredDevice>> ScanCoreAsync(
        IEnumerable<Uri> addresses,
        IEnumerable<DiscoveryCredential> credentials,
        DiscoveryOptions? options,
        Func<Uri, HttpMessageHandler>? handlerFactory,
        CancellationToken cancellationToken)
    {
        if (addresses is null)
        {
            throw new ArgumentNullException(nameof(addresses));
        }

        if (credentials is null)
        {
            throw new ArgumentNullException(nameof(credentials));
        }

        options ??= new DiscoveryOptions();
        options.Validate();

        var targets = addresses.ToList();
        foreach (var address in targets)
        {
            if (address is null || !address.IsAbsoluteUri
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Os endereços precisam ser URLs http ou https absolutas.", nameof(addresses));
            }
        }

        var attempts = new List<DiscoveryCredential>();
        foreach (var credential in credentials)
        {
            if (credential is null || string.IsNullOrEmpty(credential.Login))
            {
                throw new ArgumentException("Toda credencial precisa de usuário.", nameof(credentials));
            }

            AddUnique(attempts, credential);
        }

        if (options.TryFactoryDefaults)
        {
            AddUnique(attempts, new DiscoveryCredential(FactoryLogin, FactoryPassword));
        }

        if (attempts.Count == 0)
        {
            throw new ArgumentException("Informe ao menos uma credencial ou ligue TryFactoryDefaults.", nameof(credentials));
        }

        var results = new DiscoveredDevice[targets.Count];
        using var gate = new SemaphoreSlim(options.MaxParallelism, options.MaxParallelism);
        var tasks = new Task[targets.Count];
        for (var i = 0; i < targets.Count; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    results[index] = await ProbeAsync(targets[index], attempts, options, handlerFactory, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    private static void AddUnique(List<DiscoveryCredential> list, DiscoveryCredential credential)
    {
        if (!list.Any(c => string.Equals(c.Login, credential.Login, StringComparison.Ordinal) && string.Equals(c.Password, credential.Password, StringComparison.Ordinal)))
        {
            list.Add(credential);
        }
    }

    private static async Task<DiscoveredDevice> ProbeAsync(
        Uri address, List<DiscoveryCredential> credentials, DiscoveryOptions options, Func<Uri, HttpMessageHandler>? handlerFactory, CancellationToken cancellationToken)
    {
        foreach (var credential in credentials)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await TryCredentialAsync(address, credential, options, handlerFactory, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }
        }

        return new DiscoveredDevice(address, DiscoveryStatus.CredentialRejected);
    }

    // null: o login foi recusado, tentar a próxima credencial. Qualquer outro desfecho encerra o endereço.
    private static async Task<DiscoveredDevice?> TryCredentialAsync(
        Uri address, DiscoveryCredential credential, DiscoveryOptions options, Func<Uri, HttpMessageHandler>? handlerFactory, CancellationToken cancellationToken)
    {
        var deviceOptions = new ControlIdDeviceOptions
        {
            BaseAddress = address,
            Login = credential.Login,
            Password = credential.Password,
            Timeout = options.Timeout,
            ConnectTimeout = options.ConnectTimeout,
            MaxConcurrentRequests = 1,
            ReadRetries = 0,
            CircuitBreakerFailureThreshold = 0,
            ExceptionResponseBody = ExceptionResponseBody.None,
            AcceptAnyServerCertificate = address.Scheme == Uri.UriSchemeHttps,
        };

        HttpClient? http = null;
        AccessClient? client = null;
        try
        {
            if (handlerFactory is not null)
            {
                http = new HttpClient(handlerFactory(address), disposeHandler: false) { Timeout = options.Timeout };
            }

            client = new AccessClient(deviceOptions, http);

            try
            {
                await client.LoginAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ControlIdAuthenticationException)
            {
                return null;
            }

            var found = await ReadInformationAsync(client, address, credential.Login, cancellationToken).ConfigureAwait(false);
            await LogoutQuietlyAsync(client, cancellationToken).ConfigureAwait(false);
            return found;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ControlIdConnectionException)
        {
            return new DiscoveredDevice(address, DiscoveryStatus.PortClosed);
        }
        catch (ControlIdException ex) when (!IsFromControlId(ex))
        {
            return new DiscoveredDevice(address, DiscoveryStatus.NotControlId);
        }
        catch (Exception)
        {
            return new DiscoveredDevice(address, DiscoveryStatus.Error);
        }
        finally
        {
            client?.Dispose();
            http?.Dispose();
        }
    }

    private static async Task<DiscoveredDevice> ReadInformationAsync(AccessClient client, Uri address, string login, CancellationToken cancellationToken)
    {
        try
        {
            var info = await client.Transport.PostAsync<InformationProbe>("system_information.fcgi", null, cancellationToken).ConfigureAwait(false);
            return new DiscoveredDevice(address, DiscoveryStatus.Found, info.Model, info.Version, info.Serial, info.Network?.Mac, login);
        }
        catch (ControlIdNotSupportedException)
        {
            // Entrou com sessão, então é Control iD; este firmware só não informa os detalhes.
            return new DiscoveredDevice(address, DiscoveryStatus.Found, credentialLogin: login);
        }
    }

    private static async Task LogoutQuietlyAsync(AccessClient client, CancellationToken cancellationToken)
    {
        try
        {
            // Libera a sessão no equipamento (o número delas é limitado). Falha aqui não muda o resultado.
            await client.LogoutAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Best-effort.
        }
    }

    // Distingue "o equipamento respondeu com erro" (é Control iD) de "a resposta não é a de um Control iD".
    private static bool IsFromControlId(ControlIdException exception) =>
        exception is ControlIdDeviceErrorException { DeviceMessage: not null }
        || exception.DeviceErrorCode is not null
        || exception is ControlIdTimeoutException or ControlIdOutcomeUnknownException or ControlIdCircuitOpenException or ControlIdAuthenticationException;

    private static IEnumerable<Uri> Expand(IPAddress ip, int prefix, int port, string scheme)
    {
        var bytes = ip.GetAddressBytes();
        var value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        var hostBits = 32 - prefix;
        var mask = prefix == 0 ? 0u : uint.MaxValue << hostBits;
        var network = value & mask;
        var count = 1UL << hostBits;

        ulong first = 0;
        var last = count - 1;
        if (hostBits >= 2)
        {
            // Rede e broadcast não são equipamentos.
            first = 1;
            last = count - 2;
        }

        for (var offset = first; offset <= last; offset++)
        {
            var host = network + (uint)offset;
            var builder = new UriBuilder(scheme, $"{host >> 24}.{(host >> 16) & 0xFF}.{(host >> 8) & 0xFF}.{host & 0xFF}", port);
            yield return builder.Uri;
        }
    }

    private sealed class InformationProbe
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("serial")]
        public string? Serial { get; set; }

        [JsonPropertyName("network")]
        public NetworkProbe? Network { get; set; }
    }

    private sealed class NetworkProbe
    {
        [JsonPropertyName("mac")]
        public string? Mac { get; set; }
    }
}
