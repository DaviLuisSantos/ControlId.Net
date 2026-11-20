using System;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Push;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlId.AspNetCore;

/// <summary>
/// Varredura periódica do Push, registrada por <c>AddControlIdPush</c>: a cada <see cref="PushServerOptions.PurgeInterval"/>
/// chama <see cref="PushServer.PurgeExpiredAsync"/>, que remove os envios vencidos (<see cref="PushServerOptions.CommandTtl"/>
/// e <see cref="PushServerOptions.InFlightTimeout"/>) e avisa quem esperava ou o <see cref="IPushResultHandler"/>.
/// Uma falha da varredura é logada (2012) e o laço continua: nunca derruba o host. Com <c>PurgeInterval = null</c> o
/// serviço continua registrado (as opções só são conhecidas depois de montar o container), mas termina na partida sem
/// varrer (log 2013).
/// </summary>
internal sealed class ControlIdPushMaintenanceService : BackgroundService
{
    /// <summary>Categoria dos logs do Push no ASP.NET Core.</summary>
    internal const string LoggerCategory = "ControlId.AspNetCore.Push";

    private readonly PushServer _server;
    private readonly TimeSpan? _interval;
    private readonly ILogger _logger;
    private readonly Func<TimeSpan, IPurgeTimer> _timerFactory;

    /// <summary>Cria o serviço.</summary>
    /// <param name="server">Servidor a varrer. Resolvido na partida do host: opções inválidas falham ali, não no primeiro callback.</param>
    /// <param name="interval">Intervalo entre varreduras, ou <c>null</c> para não varrer.</param>
    /// <param name="logger">Destino dos logs, ou <c>null</c>.</param>
    /// <param name="timerFactory">Cria o temporizador (testes); <c>null</c> usa <see cref="PeriodicTimer"/>.</param>
    internal ControlIdPushMaintenanceService(PushServer server, TimeSpan? interval, ILogger? logger, Func<TimeSpan, IPurgeTimer>? timerFactory = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _interval = interval;
        _logger = logger ?? NullLogger.Instance;
        _timerFactory = timerFactory ?? (static period => new PeriodicPurgeTimer(period));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval is not { } interval)
        {
            Log.PushPurgeDisabled(_logger);
            return;
        }

        using var timer = _timerFactory(interval);
        while (true)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _server.PurgeExpiredAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.PushPurgeFailed(_logger, ex);
            }
        }
    }

    /// <summary>Temporizador da varredura (substituível nos testes).</summary>
    internal interface IPurgeTimer : IDisposable
    {
        /// <summary>Espera o próximo disparo; <c>false</c> se o temporizador foi encerrado.</summary>
        ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
    }

    private sealed class PeriodicPurgeTimer : IPurgeTimer
    {
        private readonly PeriodicTimer _timer;

        public PeriodicPurgeTimer(TimeSpan period) => _timer = new PeriodicTimer(period);

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) => _timer.WaitForNextTickAsync(cancellationToken);

        public void Dispose() => _timer.Dispose();
    }
}
