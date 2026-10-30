using System;

namespace ControlId;

/// <summary>
/// Confirmação exigida pelos métodos que apagam dados do equipamento sem volta (reset de fábrica, apagar administradores,
/// apagar todas as fotos ou todos os templates). Passe <see cref="Confirmed"/> de propósito, no ponto da chamada: o valor
/// padrão (<see cref="None"/>) é recusado, para que um <c>default</c> esquecido não apague nada.
/// </summary>
public enum DataLossConfirmation
{
    /// <summary>Sem confirmação: a chamada é recusada com <see cref="ArgumentException"/>.</summary>
    None = 0,

    /// <summary>Confirmo que os dados serão apagados do equipamento sem possibilidade de recuperação.</summary>
    Confirmed = 1,
}

internal static class DataLoss
{
    public static void Require(DataLossConfirmation confirmation, string operation)
    {
        if (confirmation != DataLossConfirmation.Confirmed)
        {
            throw new ArgumentException(
                $"{operation} apaga dados do equipamento sem volta: passe DataLossConfirmation.Confirmed para confirmar.",
                nameof(confirmation));
        }
    }
}
