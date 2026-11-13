using System.Globalization;
using System.Text;

namespace ControlId.Simulator;

/// <summary>
/// Monta as linhas do AFD no leiaute da Portaria 671 (posições fixas, ISO-8859-1, CRC-16/KERMIT nos registros 1 a 5).
/// Não usa o <c>ControlId.Net</c> de propósito: um erro de leiaute da biblioteca tem de aparecer nos testes de integração.
/// </summary>
internal static class AfdWriter
{
    /// <summary>Leiaute que o simulador informa no cabeçalho.</summary>
    private const string LayoutVersion = "003";

    public static string Header(RepIdentity identity, DateTime start, DateTime end, DateTimeOffset generatedAt)
    {
        var line = new Line();
        line.Number(0, 9).Raw("1").Raw("1").Text(identity.EmployerId, 14).Number(0, 14).Text(identity.EmployerName, 150)
            .Text(identity.RepIdentifier, 17).Raw(Date(start)).Raw(Date(end)).Raw(DateTimeText(generatedAt)).Raw(LayoutVersion)
            .Raw("1").Text(identity.ManufacturerId, 14).Text(identity.Model, 30);
        return line.Seal();
    }

    public static string Company(long nsr, DateTimeOffset at, RepIdentity identity)
    {
        var line = new Line();
        line.Number(nsr, 9).Raw("2").Raw(DateTimeText(at)).Number(0, 14).Raw("1").Text(identity.EmployerId, 14).Number(0, 14)
            .Text(identity.EmployerName, 150).Text(identity.Location, 100);
        return line.Seal();
    }

    public static string Punch(long nsr, DateTimeOffset at, long cpf)
    {
        var line = new Line();
        line.Number(nsr, 9).Raw("3").Raw(DateTimeText(at)).Number(cpf, 12);
        return line.Seal();
    }

    public static string ClockAdjustment(long nsr, DateTimeOffset before, DateTimeOffset after)
    {
        var line = new Line();
        line.Number(nsr, 9).Raw("4").Raw(DateTimeText(before)).Raw(DateTimeText(after)).Text(string.Empty, 11);
        return line.Seal();
    }

    /// <param name="operation"><c>I</c> inclusão, <c>A</c> alteração, <c>E</c> exclusão.</param>
    public static string Employee(long nsr, DateTimeOffset at, char operation, long cpf, string name)
    {
        var line = new Line();
        line.Number(nsr, 9).Raw("5").Raw(DateTimeText(at)).Raw(operation.ToString()).Number(cpf, 12).Text(name, 52).Text(string.Empty, 4)
            .Text(string.Empty, 11);
        return line.Seal();
    }

    /// <summary>Trailer de 64 posições (sem CRC): contagens dos tipos 2 a 7 e o tipo 9 na última posição.</summary>
    public static string Trailer(int companies, int punches, int clocks, int employees)
    {
        var line = new Line();
        line.Number(999999999, 9).Number(companies, 9).Number(punches, 9).Number(clocks, 9).Number(employees, 9).Number(0, 9).Number(0, 9).Raw("9");
        return line.ToString();
    }

    /// <summary><c>yyyy-MM-ddTHH:mm:ss±hhmm</c> (24 posições).</summary>
    public static string DateTimeText(DateTimeOffset value)
    {
        var offset = value.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        offset = offset.Duration();
        return value.DateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
            + sign + offset.Hours.ToString("00", CultureInfo.InvariantCulture) + offset.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }

    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>CRC-16/KERMIT (polinômio 0x8408 refletido, valor inicial 0), sobre os caracteres como bytes ISO-8859-1.</summary>
    public static string Crc(string text)
    {
        ushort crc = 0;
        foreach (var c in text)
        {
            crc ^= c <= 0xFF ? c : '?';
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0x8408) : (ushort)(crc >> 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>Linha em construção, campo a campo.</summary>
    private sealed class Line
    {
        private readonly StringBuilder _text = new();

        public Line Raw(string value)
        {
            _text.Append(value);
            return this;
        }

        public Line Number(long value, int width)
        {
            _text.Append(value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0'));
            return this;
        }

        public Line Text(string value, int width)
        {
            // ISO-8859-1: o que não cabe vira '?', como o encoder faria. Excesso é cortado.
            var clean = new string(value.Select(c => c <= 0xFF && !char.IsControl(c) ? c : '?').ToArray());
            _text.Append(clean.Length > width ? clean[..width] : clean.PadRight(width));
            return this;
        }

        /// <summary>Acrescenta o CRC dos caracteres já escritos (4 hexadecimais maiúsculos).</summary>
        public string Seal()
        {
            var body = _text.ToString();
            return body + Crc(body);
        }

        public override string ToString() => _text.ToString();
    }
}

/// <summary>Identificação do empregador e do REP que vai no cabeçalho e no registro de empresa do AFD.</summary>
internal sealed record RepIdentity(
    string EmployerId, string EmployerName, string Location, string RepIdentifier, string ManufacturerId, string Model);
