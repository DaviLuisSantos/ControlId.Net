namespace ControlId;

/// <summary>Quanto do corpo da resposta do equipamento fica em <see cref="ControlIdException.ResponseBody"/>.</summary>
public enum ExceptionResponseBody
{
    /// <summary>Só o JSON de erro do equipamento (<c>{"error":..,"code":..}</c>); nos outros casos fica <c>null</c>. Padrão.</summary>
    ErrorOnly = 0,

    /// <summary>O corpo inteiro da resposta, como veio.</summary>
    Full = 1,

    /// <summary>Nenhum corpo é guardado.</summary>
    None = 2,
}
