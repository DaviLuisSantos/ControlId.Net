using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// Lê o firmware em <c>system_information.fcgi</c> e devolve o que o equipamento não tem. Também registra o firmware
    /// no cliente e marca os comandos ausentes da matriz conhecida: depois disso, chamar um deles lança
    /// <see cref="ControlIdNotSupportedException"/> sem enviar requisição. Firmware fora da matriz não marca nada.
    /// </summary>
    Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
}
