using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlId.AspNetCore;

/// <summary>Rota que recebeu o callback: o modo online, o Monitor ou o resultado do Push.</summary>
public enum ControlIdCallbackKind
{
    /// <summary>Callback do modo online (<c>MapControlIdOnline</c>).</summary>
    Online = 0,

    /// <summary>Notificação do Monitor (<c>MapControlIdMonitor</c>).</summary>
    Monitor = 1,

    /// <summary>Resultado de um envio do Push (<c>MapControlIdPush</c>).</summary>
    PushResult = 2,
}

/// <summary>
/// Cópia do callback recebida por um <see cref="IControlIdCallbackObserver"/>: rota, cabeçalhos relevantes, equipamento
/// e corpo bruto.
/// </summary>
/// <remarks>
/// Os cabeçalhos nunca incluem <c>Authorization</c>, <c>Cookie</c> nem <c>Proxy-Authorization</c>, e os campos sensíveis
/// do corpo são mascarados com <c>***</c> como no <c>Cassette</c>.
/// </remarks>
public sealed class ControlIdRawCallback
{
    /// <summary>Cria a cópia do callback entregue ao observador.</summary>
    /// <param name="kind">Rota que recebeu o callback.</param>
    /// <param name="route">Caminho do callback, sem a consulta.</param>
    /// <param name="headers">Cabeçalhos relevantes, sem os de autenticação e de sessão.</param>
    /// <param name="deviceId">Identificador do equipamento, quando conhecido.</param>
    /// <param name="body">Corpo bruto (com os campos sensíveis mascarados).</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="route"/> ou <paramref name="headers"/> é <see langword="null"/>.
    /// </exception>
    public ControlIdRawCallback(
        ControlIdCallbackKind kind,
        string route,
        IReadOnlyDictionary<string, string> headers,
        long? deviceId,
        ReadOnlyMemory<byte> body)
    {
        Kind = kind;
        Route = route ?? throw new ArgumentNullException(nameof(route));
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        DeviceId = deviceId;
        Body = body;
    }

    /// <summary>Rota que recebeu o callback.</summary>
    public ControlIdCallbackKind Kind { get; }

    /// <summary>Caminho do callback, sem a consulta.</summary>
    public string Route { get; }

    /// <summary>Cabeçalhos relevantes da requisição, sem <c>Authorization</c>, <c>Cookie</c> nem <c>Proxy-Authorization</c>.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Identificador do equipamento, quando conhecido.</summary>
    public long? DeviceId { get; }

    /// <summary>Corpo bruto do callback, com os campos sensíveis mascarados.</summary>
    public ReadOnlyMemory<byte> Body { get; }
}

/// <summary>
/// Observa os callbacks antes dos handlers e, quando o corpo não é necessário para aceitar a requisição, antes do
/// <c>Parse</c>. Registre com
/// <c>services.AddControlIdCallbackObserver&lt;TObserver&gt;()</c>; pode haver vários, chamados na ordem de registro.
/// </summary>
/// <remarks>
/// <para>
/// Antes do <c>Parse</c>: rota com token único ou anônima e sem <see cref="ControlIdCallbackOptions.IsKnownDevice"/>.
/// Nesse caso, uma requisição recusada depois por <c>device_id</c> divergente (403) já terá sido observada. Com token por
/// equipamento ou com <c>IsKnownDevice</c>, a observação acontece depois de a requisição ser aceita. O <c>POST /result</c>
/// do Push é sempre observado antes de qualquer interpretação.
/// </para>
/// <para>
/// A resposta espera cada observador por no máximo <see cref="ControlIdCallbackOptions.ObserverTimeout"/>. Uma exceção ou
/// o estouro do prazo é registrado em log e não altera a resposta enviada ao equipamento.
/// </para>
/// </remarks>
public interface IControlIdCallbackObserver
{
    /// <summary>Observa o callback.</summary>
    /// <param name="callback">Cópia do callback com o corpo bruto.</param>
    /// <param name="cancellationToken">Cancelado quando a requisição é abortada ou o prazo vence.</param>
    Task OnCallbackAsync(ControlIdRawCallback callback, CancellationToken cancellationToken);
}

/// <summary>Registro dos observadores de callbacks no container.</summary>
public static class ControlIdCallbackObserverExtensions
{
    /// <summary>
    /// Registra um observador de callbacks. O <typeparamref name="TObserver"/> é scoped, então pode receber DbContext
    /// etc.; observadores diferentes rodam todos, na ordem de registro. Registrar o mesmo tipo de novo não tem efeito.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <typeparam name="TObserver">Tipo do observador.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> é <see langword="null"/>.</exception>
    public static IServiceCollection AddControlIdCallbackObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IControlIdCallbackObserver
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IControlIdCallbackObserver, TObserver>());
        return services;
    }
}
