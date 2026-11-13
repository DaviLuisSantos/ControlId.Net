namespace ControlId.Simulator;

/// <summary>Configuração do simulador (seção <c>Simulator</c> do appsettings ou variáveis <c>Simulator__*</c>).</summary>
public sealed class SimulatorOptions
{
    /// <summary>Valor de <see cref="Kind"/> para a linha de acesso.</summary>
    public const string KindAccess = "Access";

    /// <summary>Valor de <see cref="Kind"/> para o REP iDClass.</summary>
    public const string KindRep = "Rep";

    /// <summary>Tipo de equipamento simulado: <c>Access</c> (iDAccess, iDFlex, iDBlock...; padrão) ou <c>Rep</c> (REP iDClass).</summary>
    public string Kind { get; set; } = KindAccess;

    /// <summary>Usuário da interface web do equipamento.</summary>
    public string Login { get; set; } = "admin";

    /// <summary>Senha da interface web do equipamento.</summary>
    public string Password { get; set; } = "admin";

    /// <summary><c>device_id</c> informado nas notificações e no <c>system_information</c>.</summary>
    public long DeviceId { get; set; } = 935107;

    /// <summary>Número de série.</summary>
    public string Serial { get; set; } = "SIM0000001";

    /// <summary>Versão de firmware informada.</summary>
    public string Firmware { get; set; } = "6.13.1-sim";

    /// <summary>Quantidade de portas (1 a 4).</summary>
    public int Doors { get; set; } = 1;

    /// <summary>Quanto tempo a porta fica aberta depois de <c>execute_actions</c>.</summary>
    public TimeSpan DoorOpenTime { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Cria alguns usuários e cartões de exemplo ao iniciar.</summary>
    public bool SeedSampleData { get; set; } = true;

    /// <summary>Se o tipo configurado é o REP. Lança <see cref="InvalidOperationException"/> para um tipo desconhecido.</summary>
    public bool IsRep()
    {
        if (string.Equals(Kind, KindRep, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(Kind, KindAccess, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"Simulator:Kind inválido: '{Kind}'. Use '{KindAccess}' ou '{KindRep}'.");
    }
}
