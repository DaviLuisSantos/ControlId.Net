using System.Globalization;
using System.Text;
using ControlId.Rep.Afd;

namespace ControlId.Net.Benchmarks;

/// <summary>
/// Gera um AFD da Portaria 671 com cabeçalho, empresa, um empregado, <c>punches</c> marcações (tipo 3, com CRC) e trailer,
/// no leiaute que um REP iDClass exporta. Cada marcação tem 50 caracteres; 100 mil marcações dão cerca de 5 MB.
/// </summary>
internal static class AfdGenerator
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(-3));

    public static string Generate(int punches)
    {
        var afd = new StringBuilder((punches + 4) * 52);
        afd.Append(Header()).Append("\r\n");
        afd.Append(Company(1)).Append("\r\n");
        afd.Append(Employee(2)).Append("\r\n");

        for (var i = 0; i < punches; i++)
        {
            var time = Start.AddMinutes(i);
            var cpf = 10000000000L + (i % 500);   // 500 empregados batendo ponto
            afd.Append(Punch(3 + i, time, cpf)).Append("\r\n");
        }

        afd.Append(Trailer(punches)).Append("\r\n");
        return afd.ToString();
    }

    private static string Header() => WithCrc(
        N(0, 9) + "1" + "1" + A("12345678000199", 14) + N(0, 14) + A("Empresa de Teste LTDA", 150) + N(12345678901234567, 17)
        + "2026-10-01" + "2026-10-31" + "2026-11-01T09:30:00-0300" + "004" + "1" + A("08238299000129", 14) + A("iDClass", 30));

    private static string Company(long nsr) => WithCrc(
        N(nsr, 9) + "2" + Dh(Start) + N(12345678909, 14) + "1" + A("12345678000199", 14) + N(0, 14)
        + A("Empresa de Teste LTDA", 150) + A("Rua das Flores, 100", 100));

    private static string Employee(long nsr) => WithCrc(
        N(nsr, 9) + "5" + Dh(Start) + "I" + N(10000000000, 12) + A("José da Silva", 52) + A("", 4) + N(12345678909, 11));

    private static string Punch(long nsr, DateTimeOffset time, long cpf) => WithCrc(N(nsr, 9) + "3" + Dh(time) + N(cpf, 12));

    private static string Trailer(int punches) =>
        "999999999" + N(1, 9) + N(punches, 9) + N(0, 9) + N(1, 9) + N(0, 9) + N(0, 9) + "9";

    private static string Dh(DateTimeOffset time) => time.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "-0300";

    private static string A(string value, int length) => value.PadRight(length)[..length];

    private static string N(long value, int length) => value.ToString(CultureInfo.InvariantCulture).PadLeft(length, '0');

    private static string WithCrc(string body) => body + AfdCrc16.ComputeHex(body);
}
