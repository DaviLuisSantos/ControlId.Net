using System;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    private static readonly TimeSpan EnrollDefaultTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EnrollDefaultPollInterval = TimeSpan.FromMilliseconds(500);

    // Consultas sem ver a tela de cadastro antes de aceitar "não está cadastrando" como fim (a tela demora a abrir).
    private const int EnrollOpenGracePolls = 3;

    /// <inheritdoc/>
    public async Task<RemoteEnrollResult> EnrollAndWaitAsync(
        RemoteEnrollRequest request,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var total = timeout ?? EnrollDefaultTimeout;
        if (total <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "O prazo deve ser maior que zero.");
        }

        var interval = pollInterval ?? EnrollDefaultPollInterval;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "O intervalo de consulta deve ser maior que zero.");
        }

        using var deadline = new CancellationTokenSource(total);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await StartRemoteEnrollAsync(request, linked.Token).ConfigureAwait(false);

            var seenEnrolling = false;
            var polls = 0;
            while (true)
            {
                var state = await GetEnrollerStateAsync(linked.Token).ConfigureAwait(false);
                polls++;
                seenEnrolling |= state.IsEnrolling;
                if (!state.IsEnrolling && (seenEnrolling || polls >= EnrollOpenGracePolls))
                {
                    return BuildEnrollResult(request, state);
                }

                await Task.Delay(interval, linked.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsCancellation(ex) && deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await TryCancelRemoteEnrollAsync().ConfigureAwait(false);
            throw new ControlIdTimeoutException(
                $"O cadastro remoto não terminou em {total.TotalSeconds:0.#} s; foi cancelado no equipamento.", ex);
        }
        catch (Exception ex) when (IsCancellation(ex))
        {
            await TryCancelRemoteEnrollAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsCancellation(Exception ex) =>
        ex is OperationCanceledException || (ex is ControlIdException { InnerException: OperationCanceledException });

    private static RemoteEnrollResult BuildEnrollResult(RemoteEnrollRequest request, EnrollerState state)
    {
        var error = state.LastEnrollSucceeded ? null : state.LastEnrollError;
        if (!state.LastEnrollSucceeded && string.IsNullOrEmpty(error))
        {
            error = "UNKNOWN";
        }

        return new RemoteEnrollResult
        {
            SuccessFlag = state.LastEnrollSucceeded,
            Error = error,
            UserId = request.UserId,
        };
    }
}
