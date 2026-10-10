using System.Collections.Generic;
using System.Linq;

namespace ControlId.Rep.Afd;

/// <summary>AFD interpretado. Criado por <see cref="AfdParser.Parse(string)"/>.</summary>
public sealed class AfdFile
{
    internal AfdFile(IEnumerable<AfdRecord> records)
    {
        Records = records.ToList();
        Header = Records.OfType<AfdHeader>().FirstOrDefault();
        Trailer = Records.OfType<AfdTrailer>().LastOrDefault();
        Signature = Records.OfType<AfdSignature>().LastOrDefault();
        Punches = Records.OfType<AfdPunch>().ToList();
        EmployeeRecords = Records.OfType<AfdEmployeeRecord>().ToList();
        ClockAdjustments = Records.OfType<AfdClockAdjustment>().ToList();
        CompanyRecords = Records.OfType<AfdCompanyRecord>().ToList();
        SensitiveEvents = Records.OfType<AfdSensitiveEvent>().ToList();
        InvalidRecords = Records.OfType<AfdInvalidRecord>().ToList();

        var numbered = Records.Where(r => r.RecordType is >= 2 and <= 7).ToList();
        LastNsr = numbered.Count > 0 ? numbered.Max(r => r.Nsr) : null;
        Layout = Header?.Layout ?? numbered.Select(r => (AfdLayout?)r.Layout).FirstOrDefault() ?? Trailer?.Layout;
        Issues = Validate();
    }

    /// <summary>Leiaute do arquivo (pelo cabeçalho ou, sem ele, pelo primeiro registro); <c>null</c> se vazio.</summary>
    public AfdLayout? Layout { get; }

    /// <summary>Todos os registros, na ordem do arquivo.</summary>
    public IReadOnlyList<AfdRecord> Records { get; }

    /// <summary>Cabeçalho (tipo 1), se presente.</summary>
    public AfdHeader? Header { get; }

    /// <summary>Trailer (tipo 9), se presente.</summary>
    public AfdTrailer? Trailer { get; }

    /// <summary>Linha de assinatura digital (671), se presente.</summary>
    public AfdSignature? Signature { get; }

    /// <summary>Marcações de ponto (tipos 3 e 7).</summary>
    public IReadOnlyList<AfdPunch> Punches { get; }

    /// <summary>Inclusões, alterações e exclusões de empregados (tipo 5).</summary>
    public IReadOnlyList<AfdEmployeeRecord> EmployeeRecords { get; }

    /// <summary>Ajustes do relógio (tipo 4).</summary>
    public IReadOnlyList<AfdClockAdjustment> ClockAdjustments { get; }

    /// <summary>Alterações da identificação da empresa (tipo 2).</summary>
    public IReadOnlyList<AfdCompanyRecord> CompanyRecords { get; }

    /// <summary>Eventos sensíveis (tipo 6).</summary>
    public IReadOnlyList<AfdSensitiveEvent> SensitiveEvents { get; }

    /// <summary>Linhas que não puderam ser interpretadas.</summary>
    public IReadOnlyList<AfdInvalidRecord> InvalidRecords { get; }

    /// <summary>
    /// Maior NSR entre os registros 2 a 7. Para leitura incremental, guarde-o e peça o próximo AFD a partir de
    /// <c>LastNsr + 1</c> (<see cref="IRepClient.GetAfdFileFromNsrAsync"/>).
    /// </summary>
    public long? LastNsr { get; }

    /// <summary>
    /// Problemas encontrados: linhas inválidas, contagens do trailer que não batem e NSR fora de ordem.
    /// Vazio quando o arquivo está consistente. CRC divergente não entra aqui; veja <see cref="AfdRecord.IsCrcValid"/>.
    /// </summary>
    public IReadOnlyList<string> Issues { get; }

    private IReadOnlyList<string> Validate()
    {
        var issues = InvalidRecords.Select(r => $"Linha {r.LineNumber}: {r.Error}").ToList();

        if (Trailer is { } trailer)
        {
            Check(issues, "2", trailer.CompanyRecords, CompanyRecords.Count);
            Check(issues, "3", trailer.Punches, Punches.Count(p => p.RecordType == 3));
            Check(issues, "4", trailer.ClockAdjustments, ClockAdjustments.Count);
            Check(issues, "5", trailer.EmployeeRecords, EmployeeRecords.Count);
            Check(issues, "6", trailer.SensitiveEvents, SensitiveEvents.Count);
            Check(issues, "7", trailer.RepPPunches, Punches.Count(p => p.RecordType == 7));
        }

        AfdRecord? previous = null;
        foreach (var record in Records.Where(r => r.RecordType is >= 2 and <= 7))
        {
            if (previous is not null && record.Nsr <= previous.Nsr)
            {
                issues.Add($"Linha {record.LineNumber}: NSR {record.Nsr} não é maior que o anterior ({previous.Nsr}).");
            }

            previous = record;
        }

        return issues;
    }

    private static void Check(List<string> issues, string type, int expected, int actual)
    {
        if (expected != actual)
        {
            issues.Add($"Trailer informa {expected} registro(s) tipo {type}, mas o arquivo tem {actual}.");
        }
    }
}
