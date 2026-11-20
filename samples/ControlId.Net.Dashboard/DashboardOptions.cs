namespace ControlId.Dashboard;

/// <summary>
/// Configuração do dashboard (seção <c>Dashboard</c>; variáveis <c>Dashboard__*</c>). Validada na partida: a senha do
/// equipamento não tem padrão e vem de fora do appsettings.json (variável de ambiente ou user-secrets).
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>Nome da seção de configuração.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>Nome do equipamento no <c>IControlIdClientFactory</c>.</summary>
    public const string DeviceName = "equipamento";

    /// <summary>Tamanho mínimo do token dos callbacks.</summary>
    public const int MinCallbackTokenLength = 16;

    /// <summary>Endereço do equipamento ou do simulador.</summary>
    public string DeviceAddress { get; set; } = "http://localhost:8080";

    /// <summary>Usuário da interface web do equipamento.</summary>
    public string DeviceLogin { get; set; } = "admin";

    /// <summary>
    /// Senha da interface web do equipamento. Sem padrão: informe por <c>Dashboard__DevicePassword</c> ou
    /// <c>dotnet user-secrets set Dashboard:DevicePassword ...</c> (o simulador usa <c>admin</c>).
    /// </summary>
    public string DevicePassword { get; set; } = string.Empty;

    /// <summary>
    /// Token Bearer que o equipamento manda nos callbacks (gravado nele na partida com <c>ConfigureCallbackSecurityAsync</c>
    /// e exigido na rota do Monitor). Vazio: o dashboard gera um aleatório a cada execução. Mínimo de
    /// <see cref="MinCallbackTokenLength"/> caracteres. Atenção: o token vale para todos os callbacks do equipamento
    /// (Push, Online e Monitor); outro servidor que use o mesmo equipamento precisa do mesmo token.
    /// </summary>
    public string CallbackToken { get; set; } = string.Empty;

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
