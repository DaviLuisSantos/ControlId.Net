namespace ControlId.Tests;

/// <summary>
/// Auxiliares para IAsyncEnumerable nos testes. Os nomes evitam colidir com System.Linq.AsyncEnumerable, que existe no
/// .NET 10 e não no .NET 8 nem no .NET Framework.
/// </summary>
internal static class AsyncEnumerableTestExtensions
{
    public static async Task<List<T>> CollectAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>Lê só os <paramref name="count"/> primeiros e para de enumerar.</summary>
    public static async Task<List<T>> CollectFirstAsync<T>(this IAsyncEnumerable<T> source, int count)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
            if (list.Count == count)
            {
                break;
            }
        }

        return list;
    }
}
