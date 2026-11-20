using ControlId.Access.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlId.Push.EntityFrameworkCore;

/// <summary>Registro da fila de Push durável no <see cref="IServiceCollection"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra um <see cref="IPushCommandStore"/> durável sobre o <typeparamref name="TContext"/>, substituindo o
    /// store em memória. O contexto precisa chamar <see cref="ModelBuilderExtensions.ApplyControlIdPush"/> em
    /// <c>OnModelCreating</c> e estar registrado com <c>AddDbContextFactory</c>. Chame antes de
    /// <c>AddControlIdPush()</c>, para o servidor de Push usar o store. O store implementa
    /// <see cref="IPushCommandStoreMaintenance"/> (varredura de vencidos) e pode ser dividido por várias instâncias da
    /// aplicação sobre o mesmo banco: um envio nunca é entregue duas vezes.
    /// </summary>
    /// <typeparam name="TContext">Contexto do EF Core que contém a tabela dos envios.</typeparam>
    /// <param name="services">Coleção de serviços.</param>
    /// <exception cref="InvalidOperationException">
    /// Na resolução do <see cref="IPushCommandStore"/>, se não houver <see cref="IDbContextFactory{TContext}"/> registrado.
    /// </exception>
    public static IServiceCollection AddControlIdPushEntityFrameworkStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.Replace(ServiceDescriptor.Singleton<IPushCommandStore>(provider =>
        {
            var factory = provider.GetService<IDbContextFactory<TContext>>()
                ?? throw new InvalidOperationException(
                    $"O store de Push do EF Core precisa de um IDbContextFactory<{typeof(TContext).Name}>: registre o contexto com " +
                    $"services.AddDbContextFactory<{typeof(TContext).Name}>(...) (AddDbContext sozinho não basta).");
            return new EntityFrameworkPushCommandStore<TContext>(factory);
        }));
        return services;
    }
}
