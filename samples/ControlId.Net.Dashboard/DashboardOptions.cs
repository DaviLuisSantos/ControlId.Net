namespace ControlId.Dashboard;

/// <summary>Configuração do dashboard (seção <c>Dashboard</c>; variáveis <c>Dashboard__*</c>).</summary>
public sealed class DashboardOptions
{
    /// <summary>Nome do equipamento no <c>IControlIdClientFactory</c>.</summary>
    public const string DeviceName = "equipamento";

    /// <summary>Endereço do equipamento ou do simulador.</summary>
    public string DeviceAddress { get; set; } = "http://localhost:8080";

    /// <summary>Usuário da interface web do equipamento.</summary>
    public string DeviceLogin { get; set; } = "admin";

    /// <summary>Senha da interface web do equipamento.</summary>
    public string DevicePassword { get; set; } = "admin";

    /// <summary>
    /// Nome ou IP pelo qual o equipamento alcança este dashboard (vai para a configuração do Monitor).
    /// No Docker Compose é o nome do serviço.
    /// </summary>
    public string MonitorHostname { get; set; } = "localhost";

    /// <summary>Porta pela qual o equipamento alcança este dashboard.</summary>
    public int MonitorPort { get; set; } = 5080;

    /// <summary>Quantos acessos ficam na memória (os mais recentes).</summary>
    public int MaxEvents { get; set; } = 50;

    /// <summary>Intervalo entre as consultas ao equipamento (online, usuários, configuração do Monitor).</summary>
    public int PollSeconds { get; set; } = 5;

    /// <summary>
    /// Depois de quanto tempo uma porta aberta volta a aparecer fechada se o equipamento não avisar o fechamento
    /// (o simulador só notifica a abertura).
    /// </summary>
    public int DoorFallbackSeconds { get; set; } = 5;
}
