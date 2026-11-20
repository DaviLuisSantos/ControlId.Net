using System;
using Microsoft.Extensions.Options;

namespace ControlId.DependencyInjection;

/// <summary>Validação das opções dos equipamentos registrados, com as mesmas regras do construtor do cliente.</summary>
internal static class ControlIdOptionsValidation
{
    public const string AccessKind = "de acesso";

    public const string RepKind = "REP";

    /// <summary>Roda o <see cref="ControlIdDeviceOptions.Validate"/> e devolve a falha citando o equipamento.</summary>
    public static ValidateOptionsResult Validate(ControlIdDeviceOptions options, string kind, string name)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail(Message(kind, name, "opções ausentes."));
        }

        // O pacote grava o Timeout no HttpClient, que recusa zero e negativos (exceto o infinito).
        if (options.Timeout <= TimeSpan.Zero && options.Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            return ValidateOptionsResult.Fail(Message(kind, name, $"{nameof(ControlIdDeviceOptions.Timeout)} precisa ser positivo."));
        }

        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(Message(kind, name, ex.Message));
        }
    }

    public static string Message(string kind, string name, string detail) =>
        $"Configuração inválida do equipamento {kind} {Describe(name)}: {detail}";

    public static string Describe(string name) => name.Length == 0 ? "padrão" : $"'{name}'";
}

/// <summary>Valida as opções dos equipamentos de acesso registrados; outros nomes ficam de fora.</summary>
internal sealed class ControlIdDeviceOptionsValidator : IValidateOptions<ControlIdDeviceOptions>
{
    private readonly ControlIdRegistry _registry;

    public ControlIdDeviceOptionsValidator(ControlIdRegistry registry) => _registry = registry;

    public ValidateOptionsResult Validate(string? name, ControlIdDeviceOptions options)
    {
        var device = name ?? Options.DefaultName;
        return _registry.AccessDevices.Contains(device)
            ? ControlIdOptionsValidation.Validate(options, ControlIdOptionsValidation.AccessKind, device)
            : ValidateOptionsResult.Skip;
    }
}

/// <summary>Valida as opções dos REPs registrados; outros nomes ficam de fora.</summary>
internal sealed class ControlIdRepOptionsValidator : IValidateOptions<ControlIdRepOptions>
{
    private readonly ControlIdRegistry _registry;

    public ControlIdRepOptionsValidator(ControlIdRegistry registry) => _registry = registry;

    public ValidateOptionsResult Validate(string? name, ControlIdRepOptions options)
    {
        var device = name ?? Options.DefaultName;
        if (!_registry.RepDevices.Contains(device))
        {
            return ValidateOptionsResult.Skip;
        }

        return options is null
            ? ValidateOptionsResult.Fail(ControlIdOptionsValidation.Message(ControlIdOptionsValidation.RepKind, device, "opções ausentes."))
            : ControlIdOptionsValidation.Validate(options.ToDeviceOptions(), ControlIdOptionsValidation.RepKind, device);
    }
}
