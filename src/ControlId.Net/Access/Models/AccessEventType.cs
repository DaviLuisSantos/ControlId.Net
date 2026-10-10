namespace ControlId.Access;

/// <summary>
/// Códigos de evento de acesso (campo <c>event</c> dos logs, da resposta do modo online e da autorização remota).
/// Numeração na ordem da documentação Control iD, que confirma 3 (não identificado), 7 (concedido) e 11 (botoeira).
/// </summary>
public enum AccessEventType
{
    /// <summary>1: equipamento inválido.</summary>
    InvalidDevice = 1,

    /// <summary>2: parâmetros de identificação inválidos.</summary>
    InvalidIdentificationParameters = 2,

    /// <summary>3: não identificado.</summary>
    NotIdentified = 3,

    /// <summary>4: identificação pendente.</summary>
    IdentificationPending = 4,

    /// <summary>5: tempo de identificação esgotado.</summary>
    IdentificationTimeout = 5,

    /// <summary>6: acesso negado.</summary>
    AccessDenied = 6,

    /// <summary>7: acesso concedido.</summary>
    AccessGranted = 7,

    /// <summary>8: acesso pendente (depende de mais de uma pessoa).</summary>
    AccessPending = 8,

    /// <summary>9: usuário não é administrador (tentou acessar o menu).</summary>
    NotAdministrator = 9,

    /// <summary>10: acesso não identificado (portal aberto pela API sem motivo).</summary>
    UnidentifiedAccess = 10,

    /// <summary>11: acesso por botoeira.</summary>
    ExitButton = 11,

    /// <summary>12: acesso pela interface web.</summary>
    WebInterface = 12,

    /// <summary>13: desistência de entrada (iDBlock).</summary>
    GaveUp = 13,

    /// <summary>14: sem resposta (nenhuma ação é tomada).</summary>
    NoResponse = 14,

    /// <summary>15: acesso pela interfonia (iDFace).</summary>
    Intercom = 15,
}
