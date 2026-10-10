using System.Collections.Generic;
using ControlId.Access;
using ControlId.Rep;

namespace ControlId.DependencyInjection;

/// <summary>
/// Entrega os clientes dos equipamentos registrados com <c>AddControlIdAccess</c>/<c>AddControlIdRep</c>.
/// Há um cliente por equipamento, reaproveitado durante a vida da aplicação (a sessão de login fica com ele);
/// não descarte os clientes recebidos — o container cuida disso.
/// </summary>
public interface IControlIdClientFactory
{
    /// <summary>Nomes dos equipamentos de acesso registrados (o padrão é <c>""</c>).</summary>
    IReadOnlyCollection<string> AccessDeviceNames { get; }

    /// <summary>Nomes dos REPs registrados (o padrão é <c>""</c>).</summary>
    IReadOnlyCollection<string> RepDeviceNames { get; }

    /// <summary>Cliente do equipamento de acesso <paramref name="name"/>.</summary>
    /// <exception cref="System.InvalidOperationException">Nome não registrado ou configuração inválida.</exception>
    IAccessClient GetAccessClient(string name = "");

    /// <summary>Cliente do REP <paramref name="name"/>.</summary>
    /// <exception cref="System.InvalidOperationException">Nome não registrado ou configuração inválida.</exception>
    IRepClient GetRepClient(string name = "");
}
