using ControlId.Rep.Afd;

namespace ControlId.Tests;

/// <summary>
/// Casos de borda do parser de AFD, linha a linha: datas e horas impossíveis, fusos, CRC, PIS/CPF e linhas cortadas.
/// Fixam o comportamento (inclusive as mensagens de erro) para otimizações do parser não o mudarem.
/// </summary>
public class AfdParserEdgeCaseTests
{
    private static string N(long value, int length) => value.ToString().PadLeft(length, '0');

    private static string Punch671(string dateTime, string person = "098765432100", long nsr = 3) =>
        WithCrc(N(nsr, 9) + "3" + dateTime + person);

    private static string WithCrc(string body) => body + AfdCrc16.ComputeHex(body);

    private static string Punch1510(string date, string time, string pis = "012345678901") => N(3, 9) + "3" + date + time + pis;

    [Fact]
    public void Reads_a_671_punch()
    {
        var punch = Assert.IsType<AfdPunch>(AfdParser.ParseLine(Punch671("2026-10-01T08:05:09-0300"), 7));

        Assert.Equal(3, punch.Nsr);
        Assert.Equal(7, punch.LineNumber);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 5, 9), punch.Time);
        Assert.Equal(TimeSpan.FromHours(-3), punch.UtcOffset);
        Assert.Equal("98765432100", punch.EmployeeId);
        Assert.Equal(AfdEmployeeIdType.Cpf, punch.EmployeeIdType);
        Assert.True(punch.IsCrcValid);
        Assert.Equal(AfdLayout.Portaria671, punch.Layout);
    }

    [Theory]
    [InlineData("2026-10-01T08:00:00+0530", 5, 30)]
    [InlineData("2026-10-01T08:00:00+0000", 0, 0)]
    [InlineData("2026-10-01T08:00:00-0230", -2, -30)]
    public void Reads_the_utc_offset(string dateTime, int hours, int minutes)
    {
        var punch = Assert.IsType<AfdPunch>(AfdParser.ParseLine(Punch671(dateTime)));

        Assert.Equal(new TimeSpan(hours, minutes, 0), punch.UtcOffset);
    }

    [Theory]
    [InlineData("2026-02-30T08:00:00-0300", "Data/hora inválida na posição 11: '2026-02-30T08:00:00'.")]
    [InlineData("2025-02-29T08:00:00-0300", "Data/hora inválida na posição 11: '2025-02-29T08:00:00'.")]
    [InlineData("2026-13-01T08:00:00-0300", "Data/hora inválida na posição 11: '2026-13-01T08:00:00'.")]
    [InlineData("2026-00-01T08:00:00-0300", "Data/hora inválida na posição 11: '2026-00-01T08:00:00'.")]
    [InlineData("2026-10-00T08:00:00-0300", "Data/hora inválida na posição 11: '2026-10-00T08:00:00'.")]
    [InlineData("2026-10-01T24:00:00-0300", "Data/hora inválida na posição 11: '2026-10-01T24:00:00'.")]
    [InlineData("2026-10-01T08:60:00-0300", "Data/hora inválida na posição 11: '2026-10-01T08:60:00'.")]
    [InlineData("2026-10-01T08:00:60-0300", "Data/hora inválida na posição 11: '2026-10-01T08:00:60'.")]
    [InlineData("2026-1a-01T08:00:00-0300", "Data/hora inválida na posição 11: '2026-1a-01T08:00:00'.")]
    [InlineData("2026-10-01T08:00:00X0300", "Fuso horário inválido na posição 30: 'X0300'.")]
    [InlineData("2026-10-01T08:00:00-03a0", "Fuso horário inválido na posição 30: '-03a0'.")]
    public void Rejects_impossible_dates_and_offsets(string dateTime, string error)
    {
        var invalid = Assert.IsType<AfdInvalidRecord>(AfdParser.ParseLine(Punch671(dateTime)));

        Assert.Equal(error, invalid.Error);
        Assert.Equal(3, invalid.Nsr);
    }

    [Fact]
    public void Accepts_a_leap_day()
    {
        var punch = Assert.IsType<AfdPunch>(AfdParser.ParseLine(Punch671("2028-02-29T23:59:59-0300")));

        Assert.Equal(new DateTime(2028, 2, 29, 23, 59, 59), punch.Time);
    }

    [Fact]
    public void Flags_a_wrong_crc_and_accepts_lowercase_hex()
    {
        var body = N(3, 9) + "3" + "2026-10-01T08:00:00-0300" + "098765432100";
        var crc = AfdCrc16.ComputeHex(body);

        Assert.True(AfdParser.ParseLine(body + crc.ToLowerInvariant()).IsCrcValid);
        var wrong = AfdParser.ParseLine(body + (crc == "0000" ? "0001" : "0000"));
        Assert.IsType<AfdPunch>(wrong);
        Assert.False(wrong.IsCrcValid);
    }

    [Theory]
    [InlineData("98765432100A", "PIS/CPF inválido na posição 35: '98765432100A'.")]
    [InlineData("            ", "PIS/CPF inválido na posição 35: '            '.")]
    public void Rejects_an_invalid_cpf(string person, string error)
    {
        var invalid = Assert.IsType<AfdInvalidRecord>(AfdParser.ParseLine(Punch671("2026-10-01T08:00:00-0300", person)));

        Assert.Equal(error, invalid.Error);
    }

    [Theory]
    [InlineData("000000000123", "00000000123")]
    [InlineData("123456789012", "123456789012")]
    [InlineData("012345678901", "12345678901")]
    public void Normalizes_the_cpf_to_eleven_digits(string person, string expected)
    {
        var punch = Assert.IsType<AfdPunch>(AfdParser.ParseLine(Punch671("2026-10-01T08:00:00-0300", person)));

        Assert.Equal(expected, punch.EmployeeId);
    }

    [Fact]
    public void Reads_a_1510_punch()
    {
        var punch = Assert.IsType<AfdPunch>(AfdParser.ParseLine(Punch1510("01102026", "0805")));

        Assert.Equal(new DateTime(2026, 10, 1, 8, 5, 0), punch.Time);
        Assert.Equal("12345678901", punch.EmployeeId);
        Assert.Equal(AfdEmployeeIdType.Pis, punch.EmployeeIdType);
        Assert.Equal(AfdLayout.Portaria1510, punch.Layout);
        Assert.Null(punch.UtcOffset);
    }

    [Theory]
    [InlineData("31022026", "0805", "Data/hora inválida na posição 11: '31022026'.")]
    [InlineData("01102026", "2400", "Data/hora inválida na posição 19: '2400'.")]
    [InlineData("01102026", "0860", "Data/hora inválida na posição 19: '0860'.")]
    public void Rejects_impossible_1510_dates(string date, string time, string error)
    {
        var invalid = Assert.IsType<AfdInvalidRecord>(AfdParser.ParseLine(Punch1510(date, time)));

        Assert.Equal(error, invalid.Error);
    }

    [Fact]
    public void Pads_lines_whose_trailing_spaces_were_cut()
    {
        // Registro de empregado 1510 com o nome curto e sem os espaços finais.
        var line = N(4, 9) + "5" + "01102026" + "0805" + "I" + "012345678901" + "Ana";

        var employee = Assert.IsType<AfdEmployeeRecord>(AfdParser.ParseLine(line));

        Assert.Equal("Ana", employee.Name);
        Assert.Equal(AfdEmployeeOperation.Include, employee.Operation);
        Assert.Equal(line, employee.Line);
    }

    [Theory]
    [InlineData("12345678", "Linha curta demais para um registro do AFD.")]
    [InlineData("000000003X2026", "Tipo de registro 'X' desconhecido.")]
    public void Reports_lines_it_cannot_read(string line, string error)
    {
        var invalid = Assert.IsType<AfdInvalidRecord>(AfdParser.ParseLine(line));

        Assert.Equal(error, invalid.Error);
    }

    [Fact]
    public void Matches_DateTime_TryParseExact_on_random_dates()
    {
        // Oráculo: as regras que o parser seguia com DateTime.TryParseExact, comparadas em datas e horas aleatórias, quase
        // todas no limite (dia 28 a 31, hora 23 a 24, minuto 59 a 60...) ou com um caractere trocado.
        var random = new Random(671);
        const string Chars = "0123456789-T:+ x";
        for (var i = 0; i < 20_000; i++)
        {
            var dateTime =
                $"{random.Next(0, 10000):D4}-{random.Next(0, 14):D2}-{random.Next(26, 33):D2}T" +
                $"{random.Next(21, 25):D2}:{random.Next(57, 61):D2}:{random.Next(57, 61):D2}" +
                $"{(random.Next(3) == 0 ? '+' : '-')}{random.Next(0, 15):D2}{random.Next(0, 60):D2}";
            if (random.Next(4) == 0)
            {
                var position = random.Next(dateTime.Length);
                dateTime = dateTime.Substring(0, position) + Chars[random.Next(Chars.Length)] + dateTime.Substring(position + 1);
            }

            var record = AfdParser.ParseLine(Punch671(dateTime));

            // Sem os separadores do 671, a linha é lida no leiaute 1510 (ddMMyyyy), que recusa os 8 primeiros caracteres.
            if (dateTime[4] != '-' || dateTime[7] != '-' || dateTime[10] != 'T')
            {
                Assert.Equal($"Data/hora inválida na posição 11: '{dateTime.Substring(0, 8)}'.", Assert.IsType<AfdInvalidRecord>(record).Error);
                continue;
            }

            var expectedValid = DateTime.TryParseExact(
                dateTime.Substring(0, 19), "yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var expectedTime);
            var offsetValid = dateTime[19] is '+' or '-' && dateTime.Substring(20, 4).All(char.IsDigit);

            if (expectedValid && offsetValid)
            {
                var punch = Assert.IsType<AfdPunch>(record);
                Assert.Equal(expectedTime, punch.Time);
                var offset = new TimeSpan(int.Parse(dateTime.Substring(20, 2)), int.Parse(dateTime.Substring(22, 2)), 0);
                Assert.Equal(dateTime[19] == '-' ? offset.Negate() : offset, punch.UtcOffset);
            }
            else
            {
                var invalid = Assert.IsType<AfdInvalidRecord>(record);
                Assert.Equal(
                    expectedValid
                        ? $"Fuso horário inválido na posição 30: '{dateTime.Substring(19)}'."
                        : $"Data/hora inválida na posição 11: '{dateTime.Substring(0, 19)}'.",
                    invalid.Error);
            }
        }
    }

    [Fact]
    public void Table_crc_matches_the_bitwise_definition()
    {
        // A definição do CRC-16/KERMIT, bit a bit, como referência para a versão com tabela.
        static ushort Reference(string text)
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

            return crc;
        }

        var random = new Random(16);
        for (var i = 0; i < 5_000; i++)
        {
            var chars = new char[random.Next(0, 120)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = random.Next(10) == 0 ? (char)random.Next(0x100, 0x3000) : (char)random.Next(0, 0x100);
            }

            var text = new string(chars);
            Assert.Equal(Reference(text), AfdCrc16.Compute(text));
        }
    }

    [Fact]
    public void Treats_a_line_without_a_numeric_nsr_as_the_signature()
    {
        var signature = Assert.IsType<AfdSignature>(AfdParser.ParseLine("MIIBASSINATURA    "));

        Assert.Equal("MIIBASSINATURA", signature.Value);
    }
}
