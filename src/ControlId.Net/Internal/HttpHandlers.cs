using System;
using System.Net.Http;
using System.Threading;

namespace ControlId.Internal;

/// <summary>Ajustes comuns aos <see cref="HttpClientHandler"/> criados para os equipamentos.</summary>
internal static class HttpHandlers
{
    /// <summary>
    /// Cria o handler primário de um equipamento: limite de conexões, validação do certificado TLS e, onde o runtime
    /// permite, o prazo de conexão (<paramref name="connectTimeout"/>; <c>null</c> não aplica prazo).
    /// </summary>
    internal static HttpMessageHandler CreatePrimaryHandler(int maxConnections, TimeSpan? connectTimeout, string? certificateSha256, bool acceptAnyCertificate)
    {
#if NET8_0_OR_GREATER
        // No .NET moderno só o SocketsHttpHandler tem prazo de conexão próprio. O prazo geral da requisição
        // (HttpClient.Timeout) continua valendo por cima: se for menor que o de conexão, é ele que vence.
        // Sem prazo de conexão (o REP passa null), fica o HttpClientHandler de sempre.
        if (connectTimeout is { } connect)
        {
            var sockets = new SocketsHttpHandler { MaxConnectionsPerServer = maxConnections };
            if (connect != Timeout.InfiniteTimeSpan && connect > TimeSpan.Zero)
            {
                // Conexão própria em vez de SocketsHttpHandler.ConnectTimeout: o prazo vencido dele chega como
                // TaskCanceledException, indistinguível do timeout da requisição. Aqui ele vira SocketException
                // (TimedOut) dentro da HttpRequestException, e o transporte o classifica como falha de conexão
                // (o comando não saiu, seguro repetir).
                sockets.ConnectCallback = (context, cancellationToken) => ConnectAsync(context.DnsEndPoint, connect, cancellationToken);
            }

            var callback = CertificatePinning.CreateValidationCallback(certificateSha256, acceptAnyCertificate);
            if (callback is not null)
            {
                sockets.SslOptions.RemoteCertificateValidationCallback = callback;
            }

            return sockets;
        }
#endif
        // netstandard2.0 (.NET Framework): o HttpClientHandler não tem prazo de conexão, então connectTimeout é ignorado.
        var handler = new HttpClientHandler();
        LimitConnections(handler, maxConnections);
        CertificatePinning.Configure(handler, certificateSha256, acceptAnyCertificate);
        return handler;
    }

#if NET8_0_OR_GREATER
    private static async System.Threading.Tasks.ValueTask<System.IO.Stream> ConnectAsync(System.Net.DnsEndPoint endPoint, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await socket.ConnectAsync(endPoint, deadline.Token).ConfigureAwait(false);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.TimedOut);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
#endif

    /// <summary>
    /// Alinha o limite de conexões do handler ao de requisições simultâneas. Sem isso, no .NET Framework o padrão de
    /// 2 conexões por servidor seguraria as requisições abaixo de <c>MaxConcurrentRequests</c>.
    /// </summary>
    public static void LimitConnections(HttpClientHandler handler, int maxConnections)
    {
        try
        {
            handler.MaxConnectionsPerServer = maxConnections;
        }
        catch (PlatformNotSupportedException)
        {
            // Plataforma sem suporte: fica o padrão do runtime, e o limite do transporte continua valendo.
        }
    }
}
