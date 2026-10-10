using System;

namespace ControlId.Rep.Afd;

/// <summary>Leiaute do AFD.</summary>
public enum AfdLayout
{
    /// <summary>Portaria 1510/2009 (legado): datas <c>ddmmaaaa</c>, horas <c>hhmm</c>, empregado por PIS, sem CRC.</summary>
    Portaria1510,

    /// <summary>Portaria 671/2021: data e hora com fuso, empregado por CPF, CRC-16 nos registros 1 a 5.</summary>
    Portaria671,
}

/// <summary>Tipo de identificador do empregador ou do fabricante.</summary>
public enum AfdIdentifierType
{
    /// <summary>Valor não reconhecido.</summary>
    Unknown = 0,

    /// <summary>CNPJ.</summary>
    Cnpj = 1,

    /// <summary>CPF.</summary>
    Cpf = 2,
}

/// <summary>Documento que identifica o empregado no registro.</summary>
public enum AfdEmployeeIdType
{
    /// <summary>PIS (Portaria 1510).</summary>
    Pis,

    /// <summary>CPF (Portaria 671).</summary>
    Cpf,
}

/// <summary>Operação de um registro de empregado (tipo 5).</summary>
public enum AfdEmployeeOperation
{
    /// <summary>Valor não reconhecido.</summary>
    Unknown,

    /// <summary>"I": inclusão.</summary>
    Include,

    /// <summary>"A": alteração.</summary>
    Change,

    /// <summary>"E": exclusão.</summary>
    Exclude,
}

/// <summary>Tipo de evento sensível (registro tipo 6, Portaria 671).</summary>
public enum AfdSensitiveEventType
{
    /// <summary>Código não reconhecido (veja <see cref="AfdSensitiveEvent.Code"/>).</summary>
    Unknown = 0,

    /// <summary>01: abertura do REP por manutenção ou violação (REP-C).</summary>
    Opened = 1,

    /// <summary>02: retorno de energia.</summary>
    PowerRestored = 2,

    /// <summary>03: dispositivo externo de memória inserido na Porta Fiscal (REP-C).</summary>
    FiscalPortDeviceInserted = 3,

    /// <summary>04: dispositivo externo de memória retirado da Porta Fiscal (REP-C).</summary>
    FiscalPortDeviceRemoved = 4,

    /// <summary>05: emissão da Relação Instantânea de Marcações (REP-C).</summary>
    InstantPunchReportPrinted = 5,

    /// <summary>06: erro de impressão (REP-C).</summary>
    PrintError = 6,

    /// <summary>07: disponibilidade de serviço (REP-P).</summary>
    ServiceAvailable = 7,

    /// <summary>08: indisponibilidade de serviço (REP-P).</summary>
    ServiceUnavailable = 8,
}

/// <summary>
/// Um registro (linha) do AFD. Datas e horas são o horário de parede gravado pelo REP
/// (<see cref="DateTimeKind.Unspecified"/>); no leiaute 671 o fuso vem nas propriedades <c>...UtcOffset</c>.
/// </summary>
public abstract class AfdRecord
{
    /// <summary>Número da linha no arquivo (começa em 1).</summary>
    public int LineNumber { get; internal set; }

    /// <summary>Linha original, sem a quebra de linha.</summary>
    public string Line { get; internal set; } = string.Empty;

    /// <summary>Leiaute deste registro.</summary>
    public AfdLayout Layout { get; internal set; }

    /// <summary>Número Sequencial de Registro (0 no cabeçalho, 999999999 no trailer).</summary>
    public long Nsr { get; internal set; }

    /// <summary>Tipo do registro (1 a 7, 9 no trailer; 0 em assinatura e linhas inválidas).</summary>
    public int RecordType { get; internal set; }

    /// <summary>CRC-16 gravado no registro (tipos 1 a 5 do leiaute 671), em hexadecimal.</summary>
    public string? Crc { get; internal set; }

    /// <summary>
    /// <c>true</c> se <see cref="Crc"/> confere com o CRC-16/KERMIT da linha — o algoritmo exigido para REP-A e REP-P.
    /// Para REP-C a portaria não fixa o algoritmo, então <c>false</c> não indica, sozinho, arquivo adulterado.
    /// <c>null</c> quando o registro não tem CRC.
    /// </summary>
    public bool? IsCrcValid { get; internal set; }
}

/// <summary>Registro tipo 1: cabeçalho.</summary>
public sealed class AfdHeader : AfdRecord
{
    /// <summary>CNPJ ou CPF do empregador.</summary>
    public AfdIdentifierType EmployerIdType { get; internal set; }

    /// <summary>CNPJ ou CPF do empregador (só dígitos e letras, sem espaços).</summary>
    public string EmployerId { get; internal set; } = string.Empty;

    /// <summary>CEI (1510) ou CNO/CAEPF (671), quando existir.</summary>
    public string? CeiOrCno { get; internal set; }

    /// <summary>Razão social ou nome do empregador.</summary>
    public string EmployerName { get; internal set; } = string.Empty;

    /// <summary>Número de fabricação (REP-C), número do processo do acordo (REP-A) ou registro no INPI (REP-P).</summary>
    public string RepIdentifier { get; internal set; } = string.Empty;

    /// <summary>Data inicial dos registros no arquivo.</summary>
    public DateTime StartDate { get; internal set; }

    /// <summary>Data final dos registros no arquivo.</summary>
    public DateTime EndDate { get; internal set; }

    /// <summary>Data e hora da geração do arquivo.</summary>
    public DateTime GeneratedAt { get; internal set; }

    /// <summary>Fuso de <see cref="GeneratedAt"/> (só no leiaute 671).</summary>
    public TimeSpan? GeneratedAtUtcOffset { get; internal set; }

    /// <summary>Versão do leiaute (671), ex.: <c>003</c> ou <c>004</c>.</summary>
    public string? LayoutVersion { get; internal set; }

    /// <summary>Tipo de identificador do fabricante/desenvolvedor do REP (671).</summary>
    public AfdIdentifierType? ManufacturerIdType { get; internal set; }

    /// <summary>CNPJ ou CPF do fabricante/desenvolvedor do REP (671).</summary>
    public string? ManufacturerId { get; internal set; }

    /// <summary>Modelo do REP-C (671).</summary>
    public string? RepModel { get; internal set; }
}

/// <summary>Registro tipo 2: inclusão ou alteração da identificação da empresa no REP.</summary>
public sealed class AfdCompanyRecord : AfdRecord
{
    /// <summary>Data e hora da gravação.</summary>
    public DateTime RecordedAt { get; internal set; }

    /// <summary>Fuso de <see cref="RecordedAt"/> (671).</summary>
    public TimeSpan? RecordedAtUtcOffset { get; internal set; }

    /// <summary>CPF do responsável pela alteração (671).</summary>
    public string? ResponsibleCpf { get; internal set; }

    /// <summary>CNPJ ou CPF do empregador.</summary>
    public AfdIdentifierType EmployerIdType { get; internal set; }

    /// <summary>CNPJ ou CPF do empregador.</summary>
    public string EmployerId { get; internal set; } = string.Empty;

    /// <summary>CEI (1510) ou CNO/CAEPF (671), quando existir.</summary>
    public string? CeiOrCno { get; internal set; }

    /// <summary>Razão social ou nome do empregador.</summary>
    public string EmployerName { get; internal set; } = string.Empty;

    /// <summary>Local de prestação de serviços.</summary>
    public string Location { get; internal set; } = string.Empty;
}

/// <summary>Marcação de ponto: registro tipo 3 (REP-C/REP-A) ou tipo 7 (REP-P).</summary>
public sealed class AfdPunch : AfdRecord
{
    /// <summary>Data e hora da marcação.</summary>
    public DateTime Time { get; internal set; }

    /// <summary>Fuso de <see cref="Time"/> (671).</summary>
    public TimeSpan? UtcOffset { get; internal set; }

    /// <summary>PIS (1510) ou CPF (671) do empregado, com 11 dígitos.</summary>
    public string EmployeeId { get; internal set; } = string.Empty;

    /// <summary>Se <see cref="EmployeeId"/> é PIS ou CPF.</summary>
    public AfdEmployeeIdType EmployeeIdType { get; internal set; }

    /// <summary>Data e hora de gravação do registro (só tipo 7).</summary>
    public DateTime? RecordedAt { get; internal set; }

    /// <summary>Fuso de <see cref="RecordedAt"/> (só tipo 7).</summary>
    public TimeSpan? RecordedAtUtcOffset { get; internal set; }

    /// <summary>Coletor da marcação (só tipo 7): 1 mobile, 2 browser, 3 desktop, 4 dispositivo eletrônico, 5 outro.</summary>
    public int? CollectorId { get; internal set; }

    /// <summary>Marcação feita off-line (só tipo 7).</summary>
    public bool? Offline { get; internal set; }

    /// <summary>Hash SHA-256 encadeado (só tipo 7).</summary>
    public string? Hash { get; internal set; }

    /// <summary><see cref="Time"/> com fuso, quando o leiaute informa.</summary>
    public DateTimeOffset? TimeWithOffset => UtcOffset is { } offset ? new DateTimeOffset(Time, offset) : null;
}

/// <summary>Registro tipo 4: ajuste do relógio.</summary>
public sealed class AfdClockAdjustment : AfdRecord
{
    /// <summary>Data e hora antes do ajuste.</summary>
    public DateTime Before { get; internal set; }

    /// <summary>Fuso de <see cref="Before"/> (671).</summary>
    public TimeSpan? BeforeUtcOffset { get; internal set; }

    /// <summary>Data e hora ajustada.</summary>
    public DateTime After { get; internal set; }

    /// <summary>Fuso de <see cref="After"/> (671).</summary>
    public TimeSpan? AfterUtcOffset { get; internal set; }

    /// <summary>CPF do responsável pela alteração (671).</summary>
    public string? ResponsibleCpf { get; internal set; }
}

/// <summary>Registro tipo 5: inclusão, alteração ou exclusão de empregado no REP.</summary>
public sealed class AfdEmployeeRecord : AfdRecord
{
    /// <summary>Data e hora da gravação.</summary>
    public DateTime RecordedAt { get; internal set; }

    /// <summary>Fuso de <see cref="RecordedAt"/> (671).</summary>
    public TimeSpan? RecordedAtUtcOffset { get; internal set; }

    /// <summary>Operação realizada.</summary>
    public AfdEmployeeOperation Operation { get; internal set; }

    /// <summary>PIS (1510) ou CPF (671) do empregado, com 11 dígitos.</summary>
    public string EmployeeId { get; internal set; } = string.Empty;

    /// <summary>Se <see cref="EmployeeId"/> é PIS ou CPF.</summary>
    public AfdEmployeeIdType EmployeeIdType { get; internal set; }

    /// <summary>Nome do empregado.</summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>Demais dados de identificação do empregado (671).</summary>
    public string? OtherData { get; internal set; }

    /// <summary>CPF do responsável pela alteração (671).</summary>
    public string? ResponsibleCpf { get; internal set; }
}

/// <summary>Registro tipo 6: evento sensível do REP (671).</summary>
public sealed class AfdSensitiveEvent : AfdRecord
{
    /// <summary>Data e hora da gravação.</summary>
    public DateTime RecordedAt { get; internal set; }

    /// <summary>Fuso de <see cref="RecordedAt"/>.</summary>
    public TimeSpan? RecordedAtUtcOffset { get; internal set; }

    /// <summary>Código do evento como gravado (01 a 08).</summary>
    public int Code { get; internal set; }

    /// <summary><see cref="Code"/> como enum.</summary>
    public AfdSensitiveEventType EventType => Code is >= 1 and <= 8 ? (AfdSensitiveEventType)Code : AfdSensitiveEventType.Unknown;
}

/// <summary>Registro tipo 9: trailer, com a quantidade de registros de cada tipo.</summary>
public sealed class AfdTrailer : AfdRecord
{
    /// <summary>Quantidade de registros tipo 2.</summary>
    public int CompanyRecords { get; internal set; }

    /// <summary>Quantidade de registros tipo 3.</summary>
    public int Punches { get; internal set; }

    /// <summary>Quantidade de registros tipo 4.</summary>
    public int ClockAdjustments { get; internal set; }

    /// <summary>Quantidade de registros tipo 5.</summary>
    public int EmployeeRecords { get; internal set; }

    /// <summary>Quantidade de registros tipo 6 (671).</summary>
    public int SensitiveEvents { get; internal set; }

    /// <summary>Quantidade de registros tipo 7 (671).</summary>
    public int RepPPunches { get; internal set; }
}

/// <summary>Linha de assinatura digital ao fim do arquivo (671).</summary>
public sealed class AfdSignature : AfdRecord
{
    /// <summary>Conteúdo da assinatura, sem espaços à direita (REP-A/REP-P: <c>ASSINATURA_DIGITAL_EM_ARQUIVO_P7S</c>).</summary>
    public string Value { get; internal set; } = string.Empty;
}

/// <summary>Linha que não pôde ser interpretada. O motivo está em <see cref="Error"/>.</summary>
public sealed class AfdInvalidRecord : AfdRecord
{
    /// <summary>Motivo da falha.</summary>
    public string Error { get; internal set; } = string.Empty;
}
