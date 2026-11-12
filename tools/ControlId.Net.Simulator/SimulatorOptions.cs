namespace ControlId.Simulator;

/// <summary>Configuração do simulador (seção <c>Simulator</c> do appsettings ou variáveis <c>Simulator__*</c>).</summary>
public sealed class SimulatorOptions
{
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
}
