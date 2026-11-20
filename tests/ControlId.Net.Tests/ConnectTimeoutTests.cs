using System.Diagnostics;
using ControlId.Access;
using ControlId.Internal;

namespace ControlId.Tests;

/// <summary>Prazo de conexão do handler primário (#53).</summary>
public class ConnectTimeoutTests
{
#if NET8_0_OR_GREATER
    [Fact]
    public async Task Unreachable_address_fails_near_the_connect_timeout()
    {
        using var handler = HttpHandlers.CreatePrimaryHandler(1, TimeSpan.FromSeconds(1), null, false);
        using var http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };

        var watch = Stopwatch.StartNew();
        // 10.255.255.1 não responde; sem rota, a falha vem mais cedo ainda. Em ambos os casos tem de ficar longe dos 30 s.
        await Assert.ThrowsAnyAsync<Exception>(() => http.GetAsync("http://10.255.255.1/"));
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Levou {watch.Elapsed}.");
    }

    [Fact]
    public void Connect_timeout_is_applied_to_the_sockets_handler()
    {
        using var handler = HttpHandlers.CreatePrimaryHandler(2, TimeSpan.FromSeconds(3), null, false);

        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.NotNull(sockets.ConnectCallback); // o prazo é aplicado na conexão própria, que falha como SocketException
        Assert.Equal(2, sockets.MaxConnectionsPerServer);
    }

    [Fact]
    public async Task Connect_timeout_surfaces_as_a_connection_failure()
    {
        using var client = new AccessClient(new ControlIdDeviceOptions
        {
            BaseAddress = new Uri("http://10.255.255.1"),
            ConnectTimeout = TimeSpan.FromSeconds(1),
            Timeout = TimeSpan.FromSeconds(30),
        });

        var watch = Stopwatch.StartNew();
        // Prazo de conexão vencido (ou rede sem rota): o comando não saiu, então é falha de conexão, não timeout de resposta.
        await Assert.ThrowsAsync<ControlIdConnectionException>(() => client.GetUsersAsync());
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Levou {watch.Elapsed}.");
    }

    [Fact]
    public void Null_or_infinite_connect_timeout_keeps_the_runtime_default()
    {
        using var none = HttpHandlers.CreatePrimaryHandler(1, null, null, false);
        using var infinite = HttpHandlers.CreatePrimaryHandler(1, Timeout.InfiniteTimeSpan, null, false);

        // Sem prazo (null, como o REP), fica o HttpClientHandler de sempre; infinito usa o SocketsHttpHandler sem prazo.
        Assert.IsType<HttpClientHandler>(none);
        Assert.Equal(Timeout.InfiniteTimeSpan, Assert.IsType<SocketsHttpHandler>(infinite).ConnectTimeout);
    }

    [Fact]
    public void Pinned_certificate_is_checked_by_the_sockets_handler()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=iDClass", key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using var other = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddYears(1));
        var sha256 = CertificatePinning.Sha256(certificate);

        using var handler = HttpHandlers.CreatePrimaryHandler(1, Timeout.InfiniteTimeSpan, sha256.ToLowerInvariant(), false);
        var callback = Assert.IsType<SocketsHttpHandler>(handler).SslOptions.RemoteCertificateValidationCallback!;

        const System.Net.Security.SslPolicyErrors errors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
        Assert.True(callback(this, certificate, null, errors));
        Assert.False(callback(this, other, null, errors));
    }

    [Fact]
    public void Accept_any_certificate_installs_a_permissive_callback()
    {
        using var handler = HttpHandlers.CreatePrimaryHandler(1, Timeout.InfiniteTimeSpan, null, true);

        var callback = Assert.IsType<SocketsHttpHandler>(handler).SslOptions.RemoteCertificateValidationCallback!;
        Assert.True(callback(this, null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable));
    }
#else
    [Fact]
    public void Connect_timeout_is_ignored_on_the_framework_handler()
    {
        using var handler = HttpHandlers.CreatePrimaryHandler(1, TimeSpan.FromSeconds(1), null, false);

        Assert.IsType<HttpClientHandler>(handler);
    }
#endif
}
