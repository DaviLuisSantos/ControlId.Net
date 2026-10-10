using System.Text;
using ControlId.Rep;
using ControlId.Rep.Afd;

namespace ControlId.Tests;

public class AfdParserTests
{
    // ---- Montagem das linhas conforme as tabelas das portarias ----

    private static string A(string value, int length) => value.PadRight(length).Substring(0, length);

    private static string N(long value, int length) => value.ToString().PadLeft(length, '0');

    private static string WithCrc(string body, int totalLength)
    {
        Assert.Equal(totalLength - 4, body.Length);
        return body + AfdCrc16.ComputeHex(body);
    }

    private const string Dh1 = "2026-10-01T08:00:00-0300";
    private const string Dh2 = "2026-10-01T12:01:00-0300";

    private static string Header671() => WithCrc(
        N(0, 9) + "1" + "1" + A("12345678000199", 14) + N(0, 14) + A("Padaria São João LTDA", 150) + N(12345678901234567, 17)
        + "2026-10-01" + "2026-10-31" + "2026-11-01T09:30:00-0300" + "004" + "1" + A("08238299000129", 14) + A("iDClass", 30),
        302);

    private static string Company671(long nsr) => WithCrc(
        N(nsr, 9) + "2" + Dh1 + N(12345678909, 14) + "1" + A("12345678000199", 14) + N(0, 14)
        + A("Padaria São João LTDA", 150) + A("Rua das Flores, 100", 100),
        331);

    private static string Punch671(long nsr, string dh, long cpf) => WithCrc(N(nsr, 9) + "3" + dh + N(cpf, 12), 50);

    private static string Clock671(long nsr) => WithCrc(N(nsr, 9) + "4" + Dh1 + "2026-10-01T08:05:00-0300" + N(12345678909, 11), 73);

    private static string Employee671(long nsr) => WithCrc(
        N(nsr, 9) + "5" + Dh1 + "I" + N(98765432100, 12) + A("José Ação", 52) + A("", 4) + N(12345678909, 11),
        118);

    private static string Sensitive671(long nsr) => N(nsr, 9) + "6" + Dh1 + "02";

    private static string PunchRepP(long nsr) =>
        N(nsr, 9) + "7" + Dh2 + N(98765432100, 12) + "2026-10-01T12:02:00-0300" + "04" + "1" + new string('a', 64);

    private static string Trailer671(int t2, int t3, int t4, int t5, int t6, int t7) =>
        "999999999" + N(t2, 9) + N(t3, 9) + N(t4, 9) + N(t5, 9) + N(t6, 9) + N(t7, 9) + "9";

    private static string Afd671() => string.Join("\r\n", new[]
    {
        Header671(),
        Company671(1),
        Employee671(2),
        Punch671(3, Dh1, 98765432100),
        Clock671(4),
        Sensitive671(5),
        Punch671(6, Dh2, 98765432100),
        PunchRepP(7),
        Trailer671(1, 2, 1, 1, 1, 1),
        A("ASSINATURA_DIGITAL_EM_ARQUIVO_P7S", 100),
    }) + "\r\n";

    private static string Header1510() =>
        N(0, 9) + "1" + "1" + N(12345678000199, 14) + N(0, 12) + A("Padaria Sao Joao", 150) + N(4567, 17)
        + "01102026" + "31102026" + "01112026" + "0930";

    private static string Punch1510(long nsr, string date, string time, long pis) => N(nsr, 9) + "3" + date + time + N(pis, 12);

    private static string Afd1510() => string.Join("\r\n", new[]
    {
        Header1510(),
        N(1, 9) + "2" + "01102026" + "0800" + "1" + N(12345678000199, 14) + N(0, 12) + A("Padaria Sao Joao", 150) + A("Rua A", 100),
        N(2, 9) + "5" + "01102026" + "0800" + "I" + N(12345678901, 12) + A("Maria", 52),
        Punch1510(3, "01102026", "0800", 12345678901),
        N(4, 9) + "4" + "01102026" + "0800" + "01102026" + "0805",
        Punch1510(5, "01102026", "1200", 12345678901),
        "999999999" + N(1, 9) + N(2, 9) + N(1, 9) + N(1, 9) + "9",
    });

    // ---- Testes ----

    [Fact]
    public void Crc16_matches_example_from_the_layout()
    {
        Assert.Equal("2189", AfdCrc16.ComputeHex("123456789"));
    }

    [Fact]
    public void Fixture_lines_have_the_official_lengths()
    {
        Assert.Equal(302, Header671().Length);
        Assert.Equal(36, Sensitive671(1).Length);
        Assert.Equal(137, PunchRepP(1).Length);
        Assert.Equal(64, Trailer671(0, 0, 0, 0, 0, 0).Length);
        Assert.Equal(232, Header1510().Length);
        Assert.Equal(34, Punch1510(1, "01102026", "0800", 1).Length);
    }

    [Fact]
    public void Parses_portaria_671_file()
    {
        var afd = AfdParser.Parse(Afd671());

        Assert.Empty(afd.Issues);
        Assert.Equal(AfdLayout.Portaria671, afd.Layout);
        Assert.Equal(7, afd.LastNsr);

        var header = afd.Header!;
        Assert.Equal(AfdIdentifierType.Cnpj, header.EmployerIdType);
        Assert.Equal("12345678000199", header.EmployerId);
        Assert.Null(header.CeiOrCno);
        Assert.Equal("Padaria São João LTDA", header.EmployerName);
        Assert.Equal("12345678901234567", header.RepIdentifier);
        Assert.Equal(new DateTime(2026, 10, 1), header.StartDate);
        Assert.Equal(new DateTime(2026, 10, 31), header.EndDate);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 30, 0), header.GeneratedAt);
        Assert.Equal(TimeSpan.FromHours(-3), header.GeneratedAtUtcOffset);
        Assert.Equal("004", header.LayoutVersion);
        Assert.Equal("08238299000129", header.ManufacturerId);
        Assert.Equal("iDClass", header.RepModel);
        Assert.True(header.IsCrcValid);

        Assert.Equal("Rua das Flores, 100", Assert.Single(afd.CompanyRecords).Location);

        var employee = Assert.Single(afd.EmployeeRecords);
        Assert.Equal(AfdEmployeeOperation.Include, employee.Operation);
        Assert.Equal("98765432100", employee.EmployeeId);
        Assert.Equal("José Ação", employee.Name);
        Assert.Equal("12345678909", employee.ResponsibleCpf);
        Assert.Null(employee.OtherData);

        Assert.Equal(3, afd.Punches.Count);
        var first = afd.Punches[0];
        Assert.Equal(3, first.RecordType);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 0, 0), first.Time);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(-3)), first.TimeWithOffset);
        Assert.Equal(AfdEmployeeIdType.Cpf, first.EmployeeIdType);
        Assert.Equal("98765432100", first.EmployeeId);
        Assert.True(first.IsCrcValid);

        var repP = afd.Punches[2];
        Assert.Equal(7, repP.RecordType);
        Assert.Equal(4, repP.CollectorId);
        Assert.True(repP.Offline);
        Assert.Equal(64, repP.Hash!.Length);
        Assert.Null(repP.Crc);

        var clock = Assert.Single(afd.ClockAdjustments);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 5, 0), clock.After);

        Assert.Equal(AfdSensitiveEventType.PowerRestored, Assert.Single(afd.SensitiveEvents).EventType);
        Assert.Equal("ASSINATURA_DIGITAL_EM_ARQUIVO_P7S", afd.Signature!.Value);
    }

    [Fact]
    public void Parses_portaria_1510_file()
    {
        var afd = AfdParser.Parse(Afd1510());

        Assert.Empty(afd.Issues);
        Assert.Equal(AfdLayout.Portaria1510, afd.Layout);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 30, 0), afd.Header!.GeneratedAt);
        Assert.Null(afd.Header.GeneratedAtUtcOffset);
        Assert.Null(afd.Header.IsCrcValid);
        Assert.Equal("00000004567", afd.Header.RepIdentifier.Substring(6));

        Assert.Equal(2, afd.Punches.Count);
        Assert.All(afd.Punches, p => Assert.Equal(AfdEmployeeIdType.Pis, p.EmployeeIdType));
        Assert.Equal("12345678901", afd.Punches[0].EmployeeId);
        Assert.Equal(new DateTime(2026, 10, 1, 12, 0, 0), afd.Punches[1].Time);
        Assert.Null(afd.Punches[1].UtcOffset);
        Assert.Null(afd.Punches[1].TimeWithOffset);

        Assert.Equal("Maria", Assert.Single(afd.EmployeeRecords).Name);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 5, 0), Assert.Single(afd.ClockAdjustments).After);
        Assert.Equal(2, afd.Trailer!.Punches);
        Assert.Equal(5, afd.LastNsr);
    }

    [Fact]
    public void Legacy_line_with_trimmed_trailing_spaces_is_accepted()
    {
        var line = (N(2, 9) + "5" + "01102026" + "0800" + "E" + N(12345678901, 12) + A("Ana", 52)).TrimEnd();

        var record = Assert.IsType<AfdEmployeeRecord>(AfdParser.ParseLine(line));

        Assert.Equal("Ana", record.Name);
        Assert.Equal(AfdEmployeeOperation.Exclude, record.Operation);
    }

    [Fact]
    public void Invalid_line_does_not_stop_parsing()
    {
        var text = string.Join("\n", Punch671(1, Dh1, 111), "000000002" + "3" + "2026-13-45T25:00:00-0300" + N(1, 12) + "FFFF", Punch671(3, Dh2, 222));

        var afd = AfdParser.Parse(text);

        Assert.Equal(2, afd.Punches.Count);
        var invalid = Assert.Single(afd.InvalidRecords);
        Assert.Equal(2, invalid.LineNumber);
        Assert.Equal(2, invalid.Nsr);
        Assert.Contains("Linha 2", Assert.Single(afd.Issues));
    }

    [Fact]
    public void Reports_trailer_mismatch_and_nsr_out_of_order()
    {
        var text = string.Join("\n", Punch671(5, Dh1, 111), Punch671(4, Dh2, 222), Trailer671(0, 3, 0, 0, 0, 0));

        var issues = AfdParser.Parse(text).Issues;

        Assert.Contains(issues, i => i.Contains("tipo 3") && i.Contains("3") && i.Contains("2"));
        Assert.Contains(issues, i => i.Contains("NSR 4"));
    }

    [Fact]
    public void Tampered_record_fails_crc()
    {
        var line = Punch671(1, Dh1, 98765432100);
        var tampered = line.Substring(0, 35) + "1" + line.Substring(36); // troca um dígito do CPF

        Assert.True(AfdParser.ParseLine(line).IsCrcValid);
        Assert.False(AfdParser.ParseLine(tampered).IsCrcValid);
    }

    [Fact]
    public void Unknown_record_type_is_invalid()
    {
        var record = Assert.IsType<AfdInvalidRecord>(AfdParser.ParseLine("000000001" + "8" + "xxxxxxxx"));

        Assert.Contains("'8'", record.Error);
    }

    [Theory]
    [InlineData("iso-8859-1")]
    [InlineData("utf-8")]
    public void Stream_detects_encoding(string encodingName)
    {
        var bytes = Encoding.GetEncoding(encodingName).GetBytes(Afd671());

        var afd = AfdParser.Parse(new MemoryStream(bytes));

        Assert.Equal("José Ação", Assert.Single(afd.EmployeeRecords).Name);
        Assert.Empty(afd.Issues);
    }

    [Fact]
    public void ReadRecords_streams_line_by_line()
    {
        using var reader = new StringReader(Afd1510());

        var types = AfdParser.ReadRecords(reader).Select(r => r.RecordType).ToList();

        Assert.Equal(new[] { 1, 2, 5, 3, 4, 3, 9 }, types);
    }

    [Fact]
    public async Task RepClient_decodes_latin1_afd_and_parses_it()
    {
        // REP sem charset no Content-Type: os bytes ISO-8859-1 não podem virar '�'.
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"rep"}""")
            .OnBytes("get_afd.fcgi", Encoding.Latin1.GetBytes(Afd671()), "text/plain");

        using var client = new RepClient(
            new ControlIdDeviceOptions { BaseAddress = new Uri("https://10.0.0.9"), AcceptAnyServerCertificate = true },
            RepMode.Portaria671,
            device.CreateClient());

        var afd = await client.GetAfdFileFromNsrAsync(1);

        Assert.Equal("José Ação", Assert.Single(afd.EmployeeRecords).Name);
        Assert.Equal(3, afd.Punches.Count);
        Assert.Equal("""{"initial_nsr":1}""", device.RequestsTo("get_afd.fcgi").Single().Body);
    }
}
