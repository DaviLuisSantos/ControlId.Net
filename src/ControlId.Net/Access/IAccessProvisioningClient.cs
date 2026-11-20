using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

/// <summary>
/// Operações sensíveis, fora do <see cref="IAccessClient"/>: chaves e configuração interna do leitor HID (OMNIKEY Reader
/// Core) e código OEM. Um erro aqui pode deixar leitores ou equipamentos inutilizáveis em campo, por isso o cliente só
/// existe quando o integrador o cria com
/// <see cref="AccessProvisioningClientExtensions.CreateProvisioningClient(IAccessClient)"/> (ADR 0024).
/// </summary>
/// <remarks>
/// Implementado conforme a documentação oficial, sem teste em leitor HID real. Chaves, KEKs e licenças nunca aparecem
/// em exceção nem em log.
/// </remarks>
public interface IAccessProvisioningClient
{
    /// <summary>
    /// <c>hid_inject_default_kek.fcgi</c> — substitui a KEK de fábrica do leitor HID. É a primeira operação da hierarquia:
    /// nenhuma outra chave pode ser injetada antes dela. Apenas firmware V7 (iDFace Max com OMNIKEY Reader Core).
    /// </summary>
    /// <param name="key">A nova KEK, em hexadecimal. No modo plain é a própria chave; no modo wrapped, a chave já envolvida por KWP (NIST SP 800-38F).</param>
    /// <param name="currentKek">A KEK atual em texto plano (modo plain), ou <c>null</c> para o modo wrapped.</param>
    /// <param name="keyId">Identificador opcional de 8 bytes (16 dígitos hexadecimais), devolvido depois por <see cref="GetHidKeyInfoAsync"/>.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> quando a chave foi injetada.</returns>
    /// <exception cref="ArgumentException">Algum valor não é hexadecimal, ou <paramref name="keyId"/> não tem 8 bytes.</exception>
    Task<bool> InjectHidDefaultKekAsync(string key, string? currentKek = null, string? keyId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_inject_custom_kek.fcgi</c> — injeta uma KEK intermediária em <paramref name="targetRef"/>, envolvida pela KEK
    /// de <paramref name="kekRef"/>. Apenas firmware V7 (iDFace Max com OMNIKEY Reader Core).
    /// </summary>
    /// <param name="targetRef">Referência de chave de destino, em hexadecimal (ex.: <c>"03F001"</c>).</param>
    /// <param name="kekRef">Referência que contém a KEK que envolve a nova chave (ex.: <c>"03F000"</c>).</param>
    /// <param name="key">A chave, em hexadecimal (já envolvida por KWP no modo wrapped).</param>
    /// <param name="kek">A KEK em texto plano (modo plain), ou <c>null</c> para o modo wrapped.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> quando a chave foi injetada.</returns>
    Task<bool> InjectHidCustomKekAsync(string targetRef, string kekRef, string key, string? kek = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_inject_media_key.fcgi</c> — injeta a chave do cartão (ex.: chave de setor MIFARE Classic, 6 bytes) em
    /// <paramref name="targetRef"/>, envolvida pela KEK de <paramref name="kekRef"/>. Apenas firmware V7 (iDFace Max com
    /// OMNIKEY Reader Core).
    /// </summary>
    /// <param name="targetRef">Referência de chave de destino, em hexadecimal (ex.: <c>"030101"</c>).</param>
    /// <param name="kekRef">Referência que contém a KEK que envolve a chave.</param>
    /// <param name="key">A chave do cartão, em hexadecimal (já envolvida por KWP no modo wrapped).</param>
    /// <param name="kek">A KEK em texto plano (modo plain), ou <c>null</c> para o modo wrapped.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> quando a chave foi injetada.</returns>
    Task<bool> InjectHidMediaKeyAsync(string targetRef, string kekRef, string key, string? kek = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_get_key_info.fcgi</c> — informa o tipo e o identificador da chave guardada em <paramref name="targetRef"/>.
    /// Referência vazia volta como erro do equipamento ("Key not found"). Apenas firmware V7.
    /// </summary>
    /// <param name="targetRef">Referência de chave, em hexadecimal.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<HidKeyInfo> GetHidKeyInfoAsync(string targetRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_set_oid.fcgi</c> — escreve um booleano no parâmetro <paramref name="oid"/> do módulo OMNIKEY (não confundir
    /// com a seção <c>hid_reader</c> das configurações). Apenas firmware V7.
    /// </summary>
    /// <param name="oid">OID em notação com pontos, sempre seis partes (ex.: <c>"1.1.1.2.3.5"</c>).</param>
    /// <param name="value">Valor a escrever.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> quando o parâmetro foi escrito.</returns>
    Task<bool> SetHidOidAsync(string oid, bool value, CancellationToken cancellationToken = default);

    /// <summary><c>hid_set_oid.fcgi</c> — escreve um inteiro no parâmetro <paramref name="oid"/> do módulo OMNIKEY. Apenas firmware V7.</summary>
    /// <param name="oid">OID em notação com pontos, sempre seis partes.</param>
    /// <param name="value">Valor a escrever.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> quando o parâmetro foi escrito.</returns>
    Task<bool> SetHidOidAsync(string oid, int value, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_set_oid.fcgi</c> — escreve uma sequência de bytes no parâmetro <paramref name="oid"/> do módulo OMNIKEY
    /// (enviada em hexadecimal). Apenas firmware V7.
    /// </summary>
    /// <param name="oid">OID em notação com pontos, sempre seis partes.</param>
    /// <param name="value">Bytes a escrever; não pode ser vazio.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <returns><c>true</c> quando o parâmetro foi escrito.</returns>
    Task<bool> SetHidOidAsync(string oid, byte[] value, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_get_oid.fcgi</c> — lê o parâmetro <paramref name="oid"/> do módulo OMNIKEY. O tipo do valor é inferido pelo
    /// equipamento a partir do tamanho (ver <see cref="HidOidValue"/>). Apenas firmware V7.
    /// </summary>
    /// <param name="oid">OID em notação com pontos, sempre seis partes.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<HidOidValue> GetHidOidAsync(string oid, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_reset_config.fcgi</c> com <c>start: true</c> — inicia o reset das configurações do módulo OMNIKEY para os
    /// valores de fábrica. Acompanhe com <see cref="WaitHidConfigResetAsync"/>. Um reset já em andamento volta como erro do
    /// equipamento. Apenas firmware V7.
    /// </summary>
    /// <param name="confirmation">Confirmação explícita de que as configurações do leitor serão perdidas.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<HidConfigResetStatus> ResetHidConfigAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>hid_reset_config.fcgi</c> com o arquivo <c>config.orco</c> no corpo (<c>application/octet-stream</c>) — restaura
    /// uma configuração customizada do módulo OMNIKEY em vez da de fábrica. Acompanhe com
    /// <see cref="WaitHidConfigResetAsync"/>. Apenas firmware V7.
    /// </summary>
    /// <param name="confirmation">Confirmação explícita de que as configurações atuais do leitor serão substituídas.</param>
    /// <param name="orcoFile">Conteúdo binário do arquivo <c>.orco</c>; não pode ser vazio.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task<HidConfigResetStatus> RestoreHidConfigAsync(DataLossConfirmation confirmation, byte[] orcoFile, CancellationToken cancellationToken = default);

    /// <summary><c>hid_reset_config.fcgi</c> sem <c>start</c> — consulta o andamento do reset em curso. Apenas firmware V7.</summary>
    Task<HidConfigResetStatus> GetHidConfigResetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta <c>hid_reset_config.fcgi</c> a cada <paramref name="pollInterval"/> (padrão: 1 s) até o reset terminar.
    /// </summary>
    /// <param name="pollInterval">Intervalo entre consultas; deve ser maior que zero.</param>
    /// <param name="cancellationToken">Interrompe a espera (o reset continua no equipamento).</param>
    Task<HidConfigResetStatus> WaitHidConfigResetAsync(TimeSpan? pollInterval = null, CancellationToken cancellationToken = default);

    /// <summary><c>get_oem_code.fcgi</c> — devolve o código OEM do equipamento (ex.: <c>"CID"</c>). Firmware V6, V7 e V8.</summary>
    Task<string> GetOemCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_oem_code.fcgi</c> — define o código OEM do equipamento. Exige a chave de licença fornecida pela Control iD.
    /// Firmware V6, V7 e V8.
    /// </summary>
    /// <param name="oemCode">Novo código OEM.</param>
    /// <param name="license">Chave de licença que autoriza a alteração; nunca aparece em exceção nem em log.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task SetOemCodeAsync(string oemCode, string license, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_oem_downgrade_approved_versions.fcgi</c> — versões (<c>major.minor.patch</c>) aceitas como destino de downgrade
    /// OEM. É uma lista, não um piso. Apenas firmware V7 (iDFace Max V7.x.x).
    /// </summary>
    Task<IReadOnlyList<string>> GetOemDowngradeApprovedVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_oem_approved_fw.fcgi</c> — fixa a versão de firmware que o modo de update instala. Deve ter o mesmo
    /// <i>major</i> do firmware atual; se for anterior, precisa estar em <see cref="GetOemDowngradeApprovedVersionsAsync"/>.
    /// Apenas firmware V7 (iDFace Max V7.x.x).
    /// </summary>
    /// <param name="oemCode">O código OEM já vinculado ao equipamento.</param>
    /// <param name="license">Chave de licença do código OEM para este equipamento; nunca aparece em exceção nem em log.</param>
    /// <param name="approvedFirmware">Versão no formato <c>major.minor.patch</c> (ex.: <c>"7.14.0"</c>).</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="ArgumentException"><paramref name="approvedFirmware"/> não está no formato <c>major.minor.patch</c>.</exception>
    Task SetOemApprovedFirmwareAsync(string oemCode, string license, string approvedFirmware, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_oem_approved_fw.fcgi</c> com <c>approved_fw: ""</c> — remove a restrição: o modo de update volta a instalar a
    /// versão mais recente publicada. Apenas firmware V7 (iDFace Max V7.x.x).
    /// </summary>
    /// <param name="oemCode">O código OEM já vinculado ao equipamento.</param>
    /// <param name="license">Chave de licença do código OEM para este equipamento; nunca aparece em exceção nem em log.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    Task ClearOemApprovedFirmwareAsync(string oemCode, string license, CancellationToken cancellationToken = default);
}
