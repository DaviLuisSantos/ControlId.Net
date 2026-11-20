using System.Diagnostics;
using System.Globalization;

namespace ControlId.Net.IntegrationTests;

/// <summary>
/// Espera por condição com prazo: os testes de ponta a ponta esperam o efeito (uma requisição, um estado do simulador,
/// uma chamada ao handler) em vez de dormir um tempo fixo e torcer para ele ter acontecido.
/// </summary>
internal static class EndToEndWait
{
    /// <summary>Prazo padrão de cada espera. Só é atingido quando o teste vai falhar.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(6);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>Espera <paramref name="condition"/> ficar verdadeira; lança <see cref="TimeoutException"/> com <paramref name="description"/> no prazo.</summary>
    public static Task UntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null) =>
        UntilAsync(() => Task.FromResult(condition()), description, timeout);

    /// <summary>Espera <paramref name="condition"/> ficar verdadeira; lança <see cref="TimeoutException"/> com <paramref name="description"/> no prazo.</summary>
    public static async Task UntilAsync(Func<Task<bool>> condition, string description, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var watch = Stopwatch.StartNew();
        while (true)
        {
            if (await condition())
            {
                return;
            }

            if (watch.Elapsed >= limit)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture, $"Condição não atingida em {limit.TotalSeconds:0.#} s: {description}."));
            }

            await Task.Delay(PollInterval);
        }
    }
}
