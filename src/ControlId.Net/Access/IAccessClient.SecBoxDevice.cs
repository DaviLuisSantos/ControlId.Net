using System;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>device_has_secbox.fcgi</c> — informa se o equipamento possui um módulo SecBox. Apenas equipamentos da linha iDAccess.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<bool> DeviceHasSecBoxAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>secbox_is_active.fcgi</c> — informa se um módulo SecBox está detectado e ativo no equipamento no momento.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<bool> IsSecBoxActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>secbox_serial_number.fcgi</c> — número de série do módulo SecBox conectado; <c>"0"</c> se nenhum estiver conectado.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<string> GetSecBoxSerialNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>update_secbox_firmware_version.fcgi</c> — versão atual do firmware do módulo SecBox.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<int> GetSecBoxFirmwareVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>update_secbox_firmware.fcgi</c> — aciona a atualização de firmware do módulo SecBox. A chamada é síncrona no
    /// equipamento: só retorna ao fim do processo, que pode demorar. Para acompanhar o andamento a partir de outra
    /// tarefa, use <see cref="GetSecBoxFirmwareUpdateStatusAsync"/> e <see cref="WaitSecBoxFirmwareUpdateAsync"/>.
    /// Apenas equipamentos da linha iDAccess.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> se a atualização foi aplicada com sucesso (<c>isUpdated</c>).</returns>
    Task<bool> UpdateSecBoxFirmwareAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>update_secbox_firmware_status.fcgi</c> — progresso da atualização de firmware do SecBox em andamento.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<SecBoxFirmwareUpdateStatus> GetSecBoxFirmwareUpdateStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sonda <c>update_secbox_firmware_status.fcgi</c> até o equipamento informar <c>isFinished</c> e devolve o último
    /// estado. Não tem prazo próprio: use o <paramref name="cancellationToken"/> para limitar a espera.
    /// </summary>
    /// <param name="pollInterval">Intervalo entre as consultas; <c>null</c> usa 1 segundo.</param>
    /// <param name="cancellationToken">Cancela a espera.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> é zero ou negativo.</exception>
    /// <exception cref="OperationCanceledException">A espera foi cancelada antes de o processo terminar.</exception>
    Task<SecBoxFirmwareUpdateStatus> WaitSecBoxFirmwareUpdateAsync(TimeSpan? pollInterval = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>change_crypto_key_default.fcgi</c> — reverte a chave de segurança do SecBox para o valor padrão de fábrica.
    /// Operação destrutiva: a chave anterior é perdida.
    /// </summary>
    /// <param name="confirmation">Passe <see cref="DataLossConfirmation.Confirmed"/> para confirmar.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="ArgumentException"><paramref name="confirmation"/> não é <see cref="DataLossConfirmation.Confirmed"/>.</exception>
    Task<SecBoxKeyChangeResult> ChangeSecBoxCryptoKeyToDefaultAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>change_crypto_key_random.fcgi</c> — troca a chave de segurança do SecBox por um valor gerado aleatoriamente.
    /// Operação destrutiva: a chave anterior é perdida.
    /// </summary>
    /// <param name="confirmation">Passe <see cref="DataLossConfirmation.Confirmed"/> para confirmar.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="ArgumentException"><paramref name="confirmation"/> não é <see cref="DataLossConfirmation.Confirmed"/>.</exception>
    Task<SecBoxKeyChangeResult> ChangeSecBoxCryptoKeyToRandomAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>reset_crypto_key.fcgi</c> — reset completo, em nível de dispositivo, do módulo SecBox: reinicializa todo o estado
    /// criptográfico, não apenas a chave. Operação destrutiva.
    /// </summary>
    /// <param name="confirmation">Passe <see cref="DataLossConfirmation.Confirmed"/> para confirmar.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="ArgumentException"><paramref name="confirmation"/> não é <see cref="DataLossConfirmation.Confirmed"/>.</exception>
    Task<SecBoxKeyChangeResult> ResetSecBoxCryptoKeyAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_dual_sided_secret.fcgi</c> — grava o segredo compartilhado usado para autenticar (HMAC-SHA256) a comunicação
    /// entre os dois equipamentos do dual sided; o mesmo segredo deve ser gravado nos dois. Requer sessão de administrador
    /// (outro nível retorna 401). Apenas equipamentos da linha iDAccess. O segredo é só de escrita: o equipamento nunca o
    /// devolve e ele não aparece em exceção nem em log.
    /// </summary>
    /// <param name="secret">Exatamente 64 caracteres hexadecimais (32 bytes); maiúsculas e minúsculas são aceitas.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="ArgumentException"><paramref name="secret"/> não tem 64 caracteres hexadecimais.</exception>
    Task SetDualSidedSecretAsync(string secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_dual_sided_status.fcgi</c> — informa se o segredo compartilhado do dual sided está gravado e se o endereço
    /// do par (<c>door1_peer_address</c>) é válido. O segredo nunca é devolvido. Apenas equipamentos da linha iDAccess.
    /// </summary>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<DualSidedStatus> GetDualSidedStatusAsync(CancellationToken cancellationToken = default);
}
