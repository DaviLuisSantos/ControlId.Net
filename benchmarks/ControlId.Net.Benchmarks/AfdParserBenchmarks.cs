using System.Text;
using BenchmarkDotNet.Attributes;
using ControlId.Rep.Afd;

namespace ControlId.Net.Benchmarks;

/// <summary>
/// Ler um AFD inteiro (<see cref="AfdParser.Parse(Stream, Encoding?)"/>) contra ler registro a registro
/// (<see cref="AfdParser.ReadRecords"/>), como o <c>EnumerateAfdRecordsAsync</c> faz direto do REP. O AFD de um mês de
/// uma empresa média passa de 100 mil marcações.
/// </summary>
[MemoryDiagnoser]
public class AfdParserBenchmarks
{
    private byte[] _bytes = [];
    private string _text = string.Empty;

    [Params(10_000, 100_000)]
    public int Punches { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = AfdGenerator.Generate(Punches);
        _bytes = Encoding.UTF8.GetBytes(_text);

        // O arquivo gerado precisa ser válido, senão o benchmark mede o caminho de erro.
        var afd = AfdParser.Parse(_text);
        if (afd.Punches.Count != Punches || afd.Issues.Count != 0)
        {
            throw new InvalidOperationException($"AFD gerado inválido: {afd.Punches.Count} marcações, {afd.Issues.Count} problemas.");
        }
    }

    /// <summary>Arquivo inteiro na memória, com todos os registros tipados e as conferências do trailer.</summary>
    [Benchmark(Baseline = true)]
    public int ParseWholeFile()
    {
        using var stream = new MemoryStream(_bytes, writable: false);
        return AfdParser.Parse(stream).Punches.Count;
    }

    /// <summary>Registro a registro, sem guardar o arquivo: memória constante, qualquer tamanho.</summary>
    [Benchmark]
    public int StreamRecords()
    {
        using var reader = new StreamReader(new MemoryStream(_bytes, writable: false), Encoding.UTF8);
        var punches = 0;
        foreach (var record in AfdParser.ReadRecords(reader))
        {
            if (record is AfdPunch)
            {
                punches++;
            }
        }

        return punches;
    }
}
