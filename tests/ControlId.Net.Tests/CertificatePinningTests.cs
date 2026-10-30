using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlId.DependencyInjection;
using ControlId.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

public class CertificatePinningTests
{
    [Fact]
    public void Pinned_certificate_is_accepted_only_when_the_sha256_matches()
    {
        using var certificate = SelfSigned("CN=iDClass");
        using var other = SelfSigned("CN=outro");
        var sha256 = CertificatePinning.Sha256(certificate);

        using var handler = new HttpClientHandler();
        CertificatePinning.Configure(handler, sha256, acceptAny: false);
        var callback = handler.ServerCertificateCustomValidationCallback!;

        // Autoassinado (erro de cadeia), mas é o certificado fixado: aceito.
        Assert.True(callback(new HttpRequestMessage(), certificate, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(new HttpRequestMessage(), other, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(new HttpRequestMessage(), null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable));
    }

    [Fact]
    public void Pinning_wins_over_accept_any()
    {
        using var certificate = SelfSigned("CN=iDClass");
        using var other = SelfSigned("CN=outro");

        using var handler = new HttpClientHandler();
        CertificatePinning.Configure(handler, CertificatePinning.Sha256(certificate), acceptAny: true);

        Assert.False(handler.ServerCertificateCustomValidationCallback!(new HttpRequestMessage(), other, null, System.Net.Security.SslPolicyErrors.None));
    }

    [Fact]
    public void Without_pinning_the_previous_behaviour_is_kept()
    {
        using var acceptAny = new HttpClientHandler();
        CertificatePinning.Configure(acceptAny, null, acceptAny: true);
        using var standard = new HttpClientHandler();
        CertificatePinning.Configure(standard, " ", acceptAny: false);

        Assert.NotNull(acceptAny.ServerCertificateCustomValidationCallback);
        Assert.Null(standard.ServerCertificateCustomValidationCallback); // validação padrão do .NET
    }

    [Theory]
    [InlineData("ab:cd:ef:01:23:45:67:89:ab:cd:ef:01:23:45:67:89:ab:cd:ef:01:23:45:67:89:ab:cd:ef:01:23:45:67:89")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("abcdef01 23456789 abcdef01 23456789 abcdef01 23456789 abcdef01 23456789")]
    public void Sha256_accepts_the_formats_browsers_show(string value)
    {
        Assert.Equal("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789", CertificatePinning.Normalize(value));
    }

    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("ZZCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    public void Invalid_sha256_is_rejected_when_the_client_is_created(string value)
    {
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("https://10.0.0.3"), ServerCertificateSha256 = value };

        Assert.Throws<ArgumentException>(() => new ControlId.Rep.RepClient(options));
    }

    [Fact]
    public async Task Rep_registered_by_di_uses_the_pinned_certificate()
    {
        using var certificate = SelfSigned("CN=iDClass");
        using var other = SelfSigned("CN=outro");
        var services = new ServiceCollection();
        services.AddControlIdRep(o =>
        {
            o.BaseAddress = new Uri("https://10.0.0.3");
            o.ServerCertificateSha256 = CertificatePinning.Sha256(certificate);
        });

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("ControlId.Rep:");
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        var callback = Assert.IsType<HttpClientHandler>(handler).ServerCertificateCustomValidationCallback!;
        Assert.True(callback(new HttpRequestMessage(), certificate, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(new HttpRequestMessage(), other, null, System.Net.Security.SslPolicyErrors.None));
    }

    [Fact]
    public async Task Reading_the_certificate_requires_https()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ControlIdCertificates.GetServerCertificateSha256Async(new Uri("http://10.0.0.3")));
    }

    private static X509Certificate2 SelfSigned(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
