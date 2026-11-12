using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Internal;

/// <summary>
/// Limite de requisições simultâneas a um equipamento (<c>MaxConcurrentRequests</c>), com as métricas de fila e de
/// requisições em andamento. Cada <see cref="WaitAsync"/> bem-sucedido exige um <see cref="Release"/>.
/// </summary>
internal sealed class RequestSlots : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly KeyValuePair<string, object?>[] _deviceTags;

    public RequestSlots(int count, KeyValuePair<string, object?>[] deviceTags)
    {
        _semaphore = new SemaphoreSlim(count, count);
        _deviceTags = deviceTags;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        // Como o SemaphoreSlim.WaitAsync: com o token já cancelado, nada sai para o equipamento.
        cancellationToken.ThrowIfCancellationRequested();

        // Sem espera (o caso comum), a fila nem é contada.
        if (!_semaphore.Wait(0, CancellationToken.None))
        {
            Telemetry.QueuedRequests.Add(1, _deviceTags);
            try
            {
                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Telemetry.QueuedRequests.Add(-1, _deviceTags);
            }
        }

        Telemetry.ActiveRequests.Add(1, _deviceTags);
    }

    public void Release()
    {
        Telemetry.ActiveRequests.Add(-1, _deviceTags);
        _semaphore.Release();
    }

    public void Dispose() => _semaphore.Dispose();
}
