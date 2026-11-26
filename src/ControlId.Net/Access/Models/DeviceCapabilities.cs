using System;
using System.Collections.Generic;
using System.Linq;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>
/// O que o equipamento não tem, conforme o firmware informado por <c>system_information.fcgi</c> e a matriz observada em
/// equipamento real (<c>docs/COBERTURA.md</c>). Um firmware fora da matriz não tem restrições conhecidas: tudo é
/// considerado suportado até o equipamento recusar o comando.
/// </summary>
public sealed class DeviceCapabilities
{
    private readonly HashSet<string> _unsupported;

    /// <summary>Cria o resultado a partir do modelo, do firmware e dos comandos sabidamente ausentes.</summary>
    public DeviceCapabilities(string? model, string? firmware, IEnumerable<string> unsupportedCommands)
    {
        if (unsupportedCommands is null)
        {
            throw new ArgumentNullException(nameof(unsupportedCommands));
        }

        Model = model;
        Firmware = firmware;
        _unsupported = new HashSet<string>(unsupportedCommands.Select(Telemetry.CommandName), StringComparer.Ordinal);
        UnsupportedCommands = _unsupported.OrderBy(c => c, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Modelo do equipamento, quando o <c>system_information.fcgi</c> o informa; <c>null</c> se não informa.</summary>
    public string? Model { get; }

    /// <summary>Versão do firmware.</summary>
    public string? Firmware { get; }

    /// <summary>Comandos que este firmware sabidamente não tem (sem o <c>.fcgi</c>).</summary>
    public IReadOnlyCollection<string> UnsupportedCommands { get; }

    /// <summary>
    /// O comando existe neste firmware? Aceita o nome com ou sem <c>.fcgi</c>. <c>false</c> só para os comandos da
    /// matriz; fora dela, <c>true</c>.
    /// </summary>
    public bool Supports(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Informe o comando.", nameof(command));
        }

        return !_unsupported.Contains(Telemetry.CommandName(command));
    }
}
