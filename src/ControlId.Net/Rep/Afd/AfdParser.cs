using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ControlId.Internal;

namespace ControlId.Rep.Afd;

/// <summary>
/// Lê o Arquivo Fonte de Dados (AFD) nos leiautes da Portaria 1510/2009 e da Portaria 671/2021,
/// detectando o leiaute de cada linha. Linhas que não puderem ser lidas viram <see cref="AfdInvalidRecord"/>
/// (nunca exceção), para um registro corrompido não impedir a leitura das marcações restantes.
/// </summary>
public static class AfdParser
{
    private const string TrailerNsr = "999999999";

    /// <summary>Interpreta o AFD inteiro já em texto (ex.: o retorno de <see cref="IRepClient.GetAfdAsync"/>).</summary>
    public static AfdFile Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        using var reader = new StringReader(text);
        return new AfdFile(ReadRecords(reader));
    }

    /// <summary>
    /// Interpreta o AFD de um arquivo ou stream. Sem <paramref name="encoding"/>, usa UTF-8 se os bytes forem
    /// UTF-8 válido e, senão, ISO-8859-1 (a codificação exigida pelas portarias).
    /// </summary>
    public static AfdFile Parse(Stream stream, Encoding? encoding = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(Decode(buffer.ToArray(), encoding?.WebName));
    }

    /// <summary>
    /// Lê registro a registro, sem carregar o arquivo todo na memória. Útil para AFDs grandes; não valida o trailer
    /// (use <see cref="Parse(string)"/> para isso).
    /// </summary>
    public static IEnumerable<AfdRecord> ReadRecords(TextReader reader)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        return ReadRecordsIterator(reader);
    }

    /// <summary>Interpreta uma única linha do AFD.</summary>
    public static AfdRecord ParseLine(string line, int lineNumber = 1)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        try
        {
            return ParseLineCore(line, lineNumber);
        }
        catch (FormatException ex)
        {
            return Invalid(line, lineNumber, ex.Message);
        }
    }

    /// <summary>Decodifica bytes de AFD: charset informado, senão UTF-8 válido, senão ISO-8859-1.</summary>
    internal static string Decode(byte[] content, string? charSet) => TextDecoding.Decode(content, charSet);

    private static IEnumerable<AfdRecord> ReadRecordsIterator(TextReader reader)
    {
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            yield return ParseLine(line, lineNumber);
        }
    }

    private static AfdRecord ParseLineCore(string line, int lineNumber)
    {
        if (line.Length < 10)
        {
            return Invalid(line, lineNumber, "Linha curta demais para um registro do AFD.");
        }

        var nsrText = line.Substring(0, 9);
        if (!IsDigits(nsrText))
        {
            // A assinatura digital é a única linha sem NSR numérico.
            return new AfdSignature { Line = line, LineNumber = lineNumber, Layout = AfdLayout.Portaria671, Value = line.TrimEnd() };
        }

        if (nsrText == TrailerNsr)
        {
            return ParseTrailer(line, lineNumber);
        }

        var type = line[9];
        return type switch
        {
            '1' => line.Length >= 300 ? ParseHeader671(line, lineNumber) : ParseHeader1510(line, lineNumber),
            '2' => Is671DateTime(line) ? ParseCompany671(line, lineNumber) : ParseCompany1510(line, lineNumber),
            '3' => Is671DateTime(line) ? ParsePunch671(line, lineNumber) : ParsePunch1510(line, lineNumber),
            '4' => Is671DateTime(line) ? ParseClock671(line, lineNumber) : ParseClock1510(line, lineNumber),
            '5' => Is671DateTime(line) ? ParseEmployee671(line, lineNumber) : ParseEmployee1510(line, lineNumber),
            '6' => ParseSensitiveEvent(line, lineNumber),
            '7' => ParsePunchRepP(line, lineNumber),
            _ => Invalid(line, lineNumber, $"Tipo de registro '{type}' desconhecido."),
        };
    }

    // No leiaute 671 todo registro 2 a 5 começa com data e hora "AAAA-MM-ddT..." na posição 11.
    private static bool Is671DateTime(string line) => line.Length > 20 && line[14] == '-' && line[17] == '-' && line[20] == 'T';

    private static AfdHeader ParseHeader1510(string line, int lineNumber)
    {
        var f = new Fields(line, 232);
        var header = Init(new AfdHeader(), f, lineNumber, AfdLayout.Portaria1510, 1);
        header.EmployerIdType = f.IdentifierType(11);
        header.EmployerId = f.Text(12, 14);
        header.CeiOrCno = f.OptionalNumber(26, 12);
        header.EmployerName = f.Text(38, 150);
        header.RepIdentifier = f.Text(188, 17);
        header.StartDate = f.Date1510(205);
        header.EndDate = f.Date1510(213);
        header.GeneratedAt = f.Date1510(221).Add(f.Time1510(229));
        return header;
    }

    private static AfdHeader ParseHeader671(string line, int lineNumber)
    {
        var f = new Fields(line, 302);
        var header = Init(new AfdHeader(), f, lineNumber, AfdLayout.Portaria671, 1);
        header.EmployerIdType = f.IdentifierType(11);
        header.EmployerId = f.Text(12, 14);
        header.CeiOrCno = f.OptionalNumber(26, 14);
        header.EmployerName = f.Text(40, 150);
        header.RepIdentifier = f.Text(190, 17);
        header.StartDate = f.Date671(207);
        header.EndDate = f.Date671(217);
        (header.GeneratedAt, header.GeneratedAtUtcOffset) = f.DateTime671(227);
        header.LayoutVersion = f.Text(251, 3);
        header.ManufacturerIdType = f.IdentifierType(254);
        header.ManufacturerId = f.Text(255, 14);
        header.RepModel = NullIfEmpty(f.Text(269, 30));
        SetCrc(header, f, 299);
        return header;
    }

    private static AfdCompanyRecord ParseCompany1510(string line, int lineNumber)
    {
        var f = new Fields(line, 299);
        var record = Init(new AfdCompanyRecord(), f, lineNumber, AfdLayout.Portaria1510, 2);
        record.RecordedAt = f.Date1510(11).Add(f.Time1510(19));
        record.EmployerIdType = f.IdentifierType(23);
        record.EmployerId = f.Text(24, 14);
        record.CeiOrCno = f.OptionalNumber(38, 12);
        record.EmployerName = f.Text(50, 150);
        record.Location = f.Text(200, 100);
        return record;
    }

    private static AfdCompanyRecord ParseCompany671(string line, int lineNumber)
    {
        var f = new Fields(line, 331);
        var record = Init(new AfdCompanyRecord(), f, lineNumber, AfdLayout.Portaria671, 2);
        (record.RecordedAt, record.RecordedAtUtcOffset) = f.DateTime671(11);
        record.ResponsibleCpf = f.OptionalPerson(35, 14);
        record.EmployerIdType = f.IdentifierType(49);
        record.EmployerId = f.Text(50, 14);
        record.CeiOrCno = f.OptionalNumber(64, 14);
        record.EmployerName = f.Text(78, 150);
        record.Location = f.Text(228, 100);
        SetCrc(record, f, 328);
        return record;
    }

    private static AfdPunch ParsePunch1510(string line, int lineNumber)
    {
        var f = new Fields(line, 34);
        var punch = Init(new AfdPunch(), f, lineNumber, AfdLayout.Portaria1510, 3);
        punch.Time = f.Date1510(11).Add(f.Time1510(19));
        punch.EmployeeId = f.Person(23, 12);
        punch.EmployeeIdType = AfdEmployeeIdType.Pis;
        return punch;
    }

    private static AfdPunch ParsePunch671(string line, int lineNumber)
    {
        var f = new Fields(line, 50);
        var punch = Init(new AfdPunch(), f, lineNumber, AfdLayout.Portaria671, 3);
        (punch.Time, punch.UtcOffset) = f.DateTime671(11);
        punch.EmployeeId = f.Person(35, 12);
        punch.EmployeeIdType = AfdEmployeeIdType.Cpf;
        SetCrc(punch, f, 47);
        return punch;
    }

    private static AfdPunch ParsePunchRepP(string line, int lineNumber)
    {
        var f = new Fields(line, 137);
        var punch = Init(new AfdPunch(), f, lineNumber, AfdLayout.Portaria671, 7);
        (punch.Time, punch.UtcOffset) = f.DateTime671(11);
        punch.EmployeeId = f.Person(35, 12);
        punch.EmployeeIdType = AfdEmployeeIdType.Cpf;
        var (recordedAt, recordedOffset) = f.DateTime671(47);
        punch.RecordedAt = recordedAt;
        punch.RecordedAtUtcOffset = recordedOffset;
        punch.CollectorId = f.Int(71, 2);
        punch.Offline = f.Int(73, 1) == 1;
        punch.Hash = f.Text(74, 64);
        return punch;
    }

    private static AfdClockAdjustment ParseClock1510(string line, int lineNumber)
    {
        var f = new Fields(line, 34);
        var record = Init(new AfdClockAdjustment(), f, lineNumber, AfdLayout.Portaria1510, 4);
        record.Before = f.Date1510(11).Add(f.Time1510(19));
        record.After = f.Date1510(23).Add(f.Time1510(31));
        return record;
    }

    private static AfdClockAdjustment ParseClock671(string line, int lineNumber)
    {
        var f = new Fields(line, 73);
        var record = Init(new AfdClockAdjustment(), f, lineNumber, AfdLayout.Portaria671, 4);
        (record.Before, record.BeforeUtcOffset) = f.DateTime671(11);
        (record.After, record.AfterUtcOffset) = f.DateTime671(35);
        record.ResponsibleCpf = f.OptionalPerson(59, 11);
        SetCrc(record, f, 70);
        return record;
    }

    private static AfdEmployeeRecord ParseEmployee1510(string line, int lineNumber)
    {
        var f = new Fields(line, 87);
        var record = Init(new AfdEmployeeRecord(), f, lineNumber, AfdLayout.Portaria1510, 5);
        record.RecordedAt = f.Date1510(11).Add(f.Time1510(19));
        record.Operation = Operation(f.Text(23, 1));
        record.EmployeeId = f.Person(24, 12);
        record.EmployeeIdType = AfdEmployeeIdType.Pis;
        record.Name = f.Text(36, 52);
        return record;
    }

    private static AfdEmployeeRecord ParseEmployee671(string line, int lineNumber)
    {
        var f = new Fields(line, 118);
        var record = Init(new AfdEmployeeRecord(), f, lineNumber, AfdLayout.Portaria671, 5);
        (record.RecordedAt, record.RecordedAtUtcOffset) = f.DateTime671(11);
        record.Operation = Operation(f.Text(35, 1));
        record.EmployeeId = f.Person(36, 12);
        record.EmployeeIdType = AfdEmployeeIdType.Cpf;
        record.Name = f.Text(48, 52);
        record.OtherData = NullIfEmpty(f.Text(100, 4));
        record.ResponsibleCpf = f.OptionalPerson(104, 11);
        SetCrc(record, f, 115);
        return record;
    }

    private static AfdSensitiveEvent ParseSensitiveEvent(string line, int lineNumber)
    {
        var f = new Fields(line, 36);
        var record = Init(new AfdSensitiveEvent(), f, lineNumber, AfdLayout.Portaria671, 6);
        (record.RecordedAt, record.RecordedAtUtcOffset) = f.DateTime671(11);
        record.Code = f.Int(35, 2);
        return record;
    }

    private static AfdTrailer ParseTrailer(string line, int lineNumber)
    {
        // 1510: 46 posições, tipo "9" na 46. 671: 64 posições, com as contagens dos tipos 6 e 7.
        var is671 = line.TrimEnd().Length >= 64;
        var f = new Fields(line, is671 ? 64 : 46);
        var trailer = Init(new AfdTrailer(), f, lineNumber, is671 ? AfdLayout.Portaria671 : AfdLayout.Portaria1510, 9);
        trailer.CompanyRecords = f.Int(10, 9);
        trailer.Punches = f.Int(19, 9);
        trailer.ClockAdjustments = f.Int(28, 9);
        trailer.EmployeeRecords = f.Int(37, 9);
        if (is671)
        {
            trailer.SensitiveEvents = f.Int(46, 9);
            trailer.RepPPunches = f.Int(55, 9);
        }

        return trailer;
    }

    private static T Init<T>(T record, Fields fields, int lineNumber, AfdLayout layout, int type)
        where T : AfdRecord
    {
        record.Line = fields.Original;
        record.LineNumber = lineNumber;
        record.Layout = layout;
        record.RecordType = type;
        record.Nsr = fields.Long(1, 9);
        return record;
    }

    private static void SetCrc(AfdRecord record, Fields fields, int position)
    {
        record.Crc = fields.Text(position, 4);
        record.IsCrcValid = string.Equals(
            AfdCrc16.ComputeHex(fields.Padded.Substring(0, position - 1)), record.Crc, StringComparison.OrdinalIgnoreCase);
    }

    private static AfdEmployeeOperation Operation(string value) => value switch
    {
        "I" => AfdEmployeeOperation.Include,
        "A" => AfdEmployeeOperation.Change,
        "E" => AfdEmployeeOperation.Exclude,
        _ => AfdEmployeeOperation.Unknown,
    };

    private static AfdInvalidRecord Invalid(string line, int lineNumber, string error)
    {
        long nsr = 0;
        if (line.Length >= 9)
        {
            long.TryParse(line.Substring(0, 9), NumberStyles.None, CultureInfo.InvariantCulture, out nsr);
        }

        return new AfdInvalidRecord { Line = line, LineNumber = lineNumber, Nsr = nsr, Error = error };
    }

    private static bool IsDigits(string value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>
    /// Acesso por posição (1-based, como nas tabelas das portarias). Linhas mais curtas que o leiaute são completadas
    /// com espaços (alguns geradores cortam os espaços finais); campos obrigatórios em branco geram <see cref="FormatException"/>.
    /// </summary>
    private readonly struct Fields
    {
        public Fields(string line, int length)
        {
            Original = line;
            Padded = line.Length >= length ? line : line.PadRight(length);
        }

        public string Original { get; }

        public string Padded { get; }

        public string Raw(int position, int length) => Padded.Substring(position - 1, length);

        public string Text(int position, int length) => Raw(position, length).Trim();

        public long Long(int position, int length)
        {
            var raw = Raw(position, length);
            return long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException($"Campo numérico inválido na posição {position}: '{raw}'.");
        }

        public int Int(int position, int length) => checked((int)Long(position, length));

        public AfdIdentifierType IdentifierType(int position) => Raw(position, 1) switch
        {
            "1" => AfdIdentifierType.Cnpj,
            "2" => AfdIdentifierType.Cpf,
            _ => AfdIdentifierType.Unknown,
        };

        /// <summary>PIS ou CPF obrigatório, normalizado para 11 dígitos.</summary>
        public string Person(int position, int length)
        {
            var value = Text(position, length);
            if (value.Length == 0 || !IsDigits(value))
            {
                throw new FormatException($"PIS/CPF inválido na posição {position}: '{Raw(position, length)}'.");
            }

            return Normalize(value);
        }

        /// <summary>CPF opcional (em branco ou zerado = <c>null</c>), normalizado para 11 dígitos.</summary>
        public string? OptionalPerson(int position, int length)
        {
            var value = Text(position, length);
            return value.Length == 0 || !IsDigits(value) || value.TrimStart('0').Length == 0 ? null : Normalize(value);
        }

        /// <summary>Número opcional (CEI/CNO/CAEPF): em branco ou zerado = <c>null</c>.</summary>
        public string? OptionalNumber(int position, int length)
        {
            var value = Text(position, length);
            return value.Length == 0 || value.TrimStart('0').Length == 0 ? null : value;
        }

        public DateTime Date1510(int position) => ParseExact(Raw(position, 8), "ddMMyyyy", position);

        public TimeSpan Time1510(int position)
        {
            var raw = Raw(position, 4);
            return ParseExact(raw, "HHmm", position).TimeOfDay;
        }

        public DateTime Date671(int position) => ParseExact(Raw(position, 10), "yyyy-MM-dd", position);

        public (DateTime Time, TimeSpan Offset) DateTime671(int position)
        {
            // "AAAA-MM-ddThh:mm:00-zzzz": 19 posições de data/hora + sinal + hhmm do fuso.
            var raw = Raw(position, 24);
            var time = ParseExact(raw.Substring(0, 19), "yyyy-MM-dd'T'HH:mm:ss", position);
            var sign = raw[19];
            if ((sign != '+' && sign != '-') || !IsDigits(raw.Substring(20, 4)))
            {
                throw new FormatException($"Fuso horário inválido na posição {position + 19}: '{raw.Substring(19)}'.");
            }

            var offset = new TimeSpan(int.Parse(raw.Substring(20, 2), CultureInfo.InvariantCulture), int.Parse(raw.Substring(22, 2), CultureInfo.InvariantCulture), 0);
            return (time, sign == '-' ? offset.Negate() : offset);
        }

        private static DateTime ParseExact(string raw, string format, int position) =>
            DateTime.TryParseExact(raw, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
                ? value
                : throw new FormatException($"Data/hora inválida na posição {position}: '{raw}'.");

        // PIS e CPF têm 11 dígitos; o AFD os grava com 12 posições (zero à esquerda).
        private static string Normalize(string digits)
        {
            var trimmed = digits.TrimStart('0');
            return trimmed.Length <= 11 ? trimmed.PadLeft(11, '0') : trimmed;
        }
    }
}
