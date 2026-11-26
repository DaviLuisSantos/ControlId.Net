using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ControlId.Access.Push;

/// <summary>
/// Prazos e limites do <see cref="PushServer"/>. Os valores são lidos uma vez, quando o servidor é criado: alterar a
/// instância depois não muda o servidor.
/// </summary>
public sealed class PushServerOptions
{
    /// <summary>Maior prazo aceito por <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> (cerca de 24,8 dias).</summary>
    private static readonly TimeSpan MaxTimerDuration = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Prazo de <see cref="PushServer.SendAsync"/> e <see cref="PushServer.SendBatchAsync"/> (padrão: 5 minutos), contado a
    /// partir do enfileiramento. Ao vencer, o envio sai da fila (pendente ou em andamento) e a tarefa falha com
    /// <see cref="TimeoutException"/>. Vale junto com o <see cref="CancellationToken"/> do chamador: o que vencer primeiro.
    /// <c>null</c> desliga o prazo (a tarefa só termina com o resultado, um órfão, a varredura ou o cancelamento).
    /// Não se aplica a <see cref="PushServer.EnqueueAsync"/>.
    /// </summary>
    public TimeSpan? CommandTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validade de um envio pendente (padrão: 24 horas), contada de <see cref="PushEnvelope.CreatedAt"/>. Um envio mais
    /// velho que isso nunca é entregue: sai da fila na próxima consulta do equipamento (ou na varredura de
    /// <see cref="PushServer.PurgeExpiredAsync"/>) e falha como expirado. Evita que um equipamento que ficou dias desligado
    /// execute ordens obsoletas (abrir porta, reiniciar). <c>null</c> desliga a validade.
    /// </summary>
    public TimeSpan? CommandTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Tempo máximo de um envio entregue sem resultado (padrão: 10 minutos), contado de <see cref="PushEnvelope.DeliveredAt"/>.
    /// Usado só pela varredura (<see cref="PushServer.PurgeExpiredAsync"/>): o envio sai do store e falha. <c>null</c> desliga.
    /// </summary>
    public TimeSpan? InFlightTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Máximo de envios (pendentes mais o em andamento) por equipamento (padrão: 1.000). Acima disso, <c>Enqueue*</c> e
    /// <c>Send*</c> lançam <see cref="PushQueueFullException"/>. <c>0</c> desliga o limite.
    /// </summary>
    public int MaxPendingPerDevice { get; set; } = 1000;

    /// <summary>
    /// Máximo de equipamentos com a última consulta registrada em <see cref="PushServer.GetLastSeen"/> (padrão: 10.000).
    /// Acima disso, equipamentos novos não são registrados (os já conhecidos continuam atualizados): um <c>deviceId</c>
    /// arbitrário na query não faz a memória crescer sem limite.
    /// </summary>
    public int MaxTrackedDevices { get; set; } = 10_000;

    /// <summary>
    /// Exige o <c>uuid</c> do equipamento (padrão: <c>false</c>). Ligado, <c>/push</c> sem <c>uuid</c> devolve corpo vazio
    /// (nada sai da fila) e <c>/result</c> sem <c>uuid</c> (na query ou no corpo) devolve <c>false</c>. Impede que um
    /// <c>/result</c> atrasado ou repetido complete o envio em andamento.
    /// </summary>
    public bool RequireUuid { get; set; }

    /// <summary>
    /// Usa o formato <c>transactions</c> nos lotes (<see cref="PushServer.SendBatchAsync"/> e
    /// <see cref="PushServer.EnqueueBatchAsync"/>). <c>null</c> (padrão) é automático: o lote sai como <c>transactions</c> e,
    /// se o equipamento recusar o formato (o iDFace 6.9.12 responde "Invalid member 'endpoint'"), o servidor lembra isso para
    /// aquele equipamento, reenvia os comandos do lote como envios individuais e entrega os mesmos resultados; os lotes
    /// seguintes dele já saem individuais. <c>true</c> sempre usa <c>transactions</c> e nunca recua. <c>false</c> nunca usa
    /// <c>transactions</c>: todo lote sai como envios individuais.
    /// </summary>
    public bool? UseTransactions { get; set; }

    /// <summary>
    /// Prazo de cada chamada ao <see cref="IPushResultHandler"/> (padrão: 30 segundos). O handler recebe um token que
    /// vence nesse prazo, sem ligação com a requisição do equipamento; se não terminar a tempo ou lançar, a falha é logada
    /// e a resposta ao equipamento segue normalmente.
    /// </summary>
    public TimeSpan ResultHandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Intervalo da varredura automática (<see cref="PushServer.PurgeExpiredAsync"/>) feita pelo serviço de fundo do pacote
    /// <c>ControlId.Net.AspNetCore</c> (padrão: 1 minuto). O <see cref="PushServer"/> não lê este valor: sem o pacote,
    /// chame a varredura você mesmo. <c>null</c> desliga a varredura automática.
    /// </summary>
    public TimeSpan? PurgeInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Destino dos logs do servidor (categoria <see cref="ControlIdTelemetry.LoggerCategory"/>), ou <c>null</c> para não
    /// logar. Os logs nunca incluem o corpo dos comandos nem dos resultados.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>Relógio do servidor (testes). <c>null</c> usa <see cref="DateTimeOffset.UtcNow"/>.</summary>
    internal Func<DateTimeOffset>? Clock { get; set; }

    /// <summary>
    /// Cria o prazo de cada envio a partir de <see cref="CommandTimeout"/> (testes). <c>null</c> usa
    /// <c>new CancellationTokenSource(timeout)</c>; um teste pode devolver uma fonte que ele mesmo cancela.
    /// </summary>
    internal Func<TimeSpan, CancellationTokenSource>? CommandDeadlineFactory { get; set; }

    /// <summary>Confere os valores; lança <see cref="ArgumentOutOfRangeException"/> no primeiro inválido.</summary>
    internal void Validate()
    {
        if (CommandTimeout is { } commandTimeout && (commandTimeout <= TimeSpan.Zero || commandTimeout > MaxTimerDuration))
        {
            throw new ArgumentOutOfRangeException(nameof(CommandTimeout), commandTimeout, $"{nameof(CommandTimeout)} precisa ser positivo e no máximo {MaxTimerDuration.TotalDays:0.#} dias (ou null).");
        }

        if (CommandTtl is { } ttl && ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CommandTtl), ttl, $"{nameof(CommandTtl)} precisa ser positivo (ou null).");
        }

        if (InFlightTimeout is { } inFlight && inFlight <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(InFlightTimeout), inFlight, $"{nameof(InFlightTimeout)} precisa ser positivo (ou null).");
        }

        if (MaxPendingPerDevice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPendingPerDevice), MaxPendingPerDevice, $"{nameof(MaxPendingPerDevice)} não pode ser negativo (0 desliga o limite).");
        }

        if (MaxTrackedDevices < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTrackedDevices), MaxTrackedDevices, $"{nameof(MaxTrackedDevices)} precisa ser pelo menos 1.");
        }

        if (ResultHandlerTimeout <= TimeSpan.Zero || ResultHandlerTimeout > MaxTimerDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(ResultHandlerTimeout), ResultHandlerTimeout, $"{nameof(ResultHandlerTimeout)} precisa ser positivo e no máximo {MaxTimerDuration.TotalDays:0.#} dias.");
        }

        if (PurgeInterval is { } purge && (purge <= TimeSpan.Zero || purge > MaxTimerDuration))
        {
            throw new ArgumentOutOfRangeException(nameof(PurgeInterval), purge, $"{nameof(PurgeInterval)} precisa ser positivo e no máximo {MaxTimerDuration.TotalDays:0.#} dias (ou null).");
        }
    }
}
