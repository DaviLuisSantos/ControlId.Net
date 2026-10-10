using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Corpo de <c>report_generate.fcgi</c>: relatório em texto (estilo CSV) de qualquer objeto, com os mesmos
/// filtros de <c>load_objects</c>.
/// </summary>
public sealed class ReportRequest
{
    /// <summary>Cria o relatório do objeto informado (ex.: <c>users</c>, <c>access_logs</c>).</summary>
    public ReportRequest(string @object)
    {
        if (string.IsNullOrWhiteSpace(@object))
        {
            throw new ArgumentException("Informe o objeto do relatório.", nameof(@object));
        }

        Object = @object;
    }

    /// <summary>Objeto principal do relatório.</summary>
    [JsonPropertyName("object")]
    public string Object { get; }

    /// <summary>Colunas, na ordem. Veja <see cref="ReportColumn.ObjectField"/> e <see cref="ReportColumn.Counter"/>.</summary>
    [JsonPropertyName("columns")]
    public IList<ReportColumn> Columns { get; set; } = new List<ReportColumn>();

    /// <summary>Delimitador de campos, ex.: <c>";"</c>.</summary>
    [JsonPropertyName("delimiter")]
    public string? Delimiter { get; set; }

    /// <summary>Linha de cabeçalho, ex.: <c>"Nome;Código"</c>.</summary>
    [JsonPropertyName("header")]
    public string? Header { get; set; }

    /// <summary>Quebra de linha, ex.: <c>"\r\n"</c>.</summary>
    [JsonPropertyName("line_break")]
    public string? LineBreak { get; set; }

    /// <summary>Nome do arquivo gerado.</summary>
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    /// <summary>Filtro no formato de <c>load_objects</c> (veja <see cref="Access.Where"/>).</summary>
    [JsonPropertyName("where")]
    public object? Where { get; set; }

    /// <summary>Ordenação, ex.: <c>["ascending", "name"]</c>.</summary>
    [JsonPropertyName("order")]
    public IList<string>? Order { get; set; }

    /// <summary>Junção com outros objetos: <c>"LEFT"</c> ou <c>"INNER"</c>.</summary>
    [JsonPropertyName("join")]
    public string? Join { get; set; }
}

/// <summary>Coluna de <see cref="ReportRequest"/>.</summary>
public sealed class ReportColumn
{
    /// <summary>
    /// Tipo da coluna: <c>object_field</c>, <c>counter</c> ou <c>fixed</c>. Para <c>fixed</c>, a documentação não diz o
    /// nome do campo do valor fixo: preencha-o em <see cref="AdditionalFields"/>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object_field";

    /// <summary>Objeto (tabela) da coluna <c>object_field</c>.</summary>
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    /// <summary>Campo da coluna.</summary>
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    /// <summary>Contador: valor inicial (padrão do equipamento: 0).</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>Contador: incremento (padrão do equipamento: 1).</summary>
    [JsonPropertyName("step")]
    public int? Step { get; set; }

    /// <summary>Formatação da coluna.</summary>
    [JsonPropertyName("format")]
    public ReportColumnFormat? Format { get; set; }

    /// <summary>Campos extras enviados como estão (ex.: o valor de uma coluna <c>fixed</c>).</summary>
    [JsonExtensionData]
    public IDictionary<string, object>? AdditionalFields { get; set; }

    /// <summary>Coluna com o valor de um campo, ex.: <c>ObjectField("users", "name")</c>.</summary>
    public static ReportColumn ObjectField(string @object, string field, ReportColumnFormat? format = null) =>
        new() { Type = "object_field", Object = @object, Field = field, Format = format };

    /// <summary>Contador de registros.</summary>
    public static ReportColumn Counter(int? offset = null, int? step = null, ReportColumnFormat? format = null) =>
        new() { Type = "counter", Offset = offset, Step = step, Format = format };
}

/// <summary>Formatação de uma <see cref="ReportColumn"/>.</summary>
public sealed class ReportColumnFormat
{
    /// <summary>Alinhamento.</summary>
    [JsonPropertyName("adjustment")]
    public ReportAlignment? Alignment { get; set; }

    /// <summary>Caractere de preenchimento, ex.: <c>"0"</c> ou <c>" "</c>.</summary>
    [JsonPropertyName("fill")]
    public string? Fill { get; set; }

    /// <summary>Formato, ex.: <c>"%s"</c>, ou para datas <c>"%d/%m/%Y %H:%M"</c>.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>Largura da coluna em caracteres.</summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }
}

/// <summary>Alinhamento de coluna do relatório.</summary>
public enum ReportAlignment
{
    /// <summary>Sem ajuste.</summary>
    None = 0,

    /// <summary>À esquerda.</summary>
    Left = 1,

    /// <summary>À direita.</summary>
    Right = 2,
}

/// <summary>Categorias de <c>export_audit_logs.fcgi</c>.</summary>
[Flags]
public enum AuditLogCategories
{
    /// <summary>Nenhuma.</summary>
    None = 0,

    /// <summary>Mudanças de configuração.</summary>
    Config = 1,

    /// <summary>Sessões da API.</summary>
    Api = 2,

    /// <summary>Uso de pen drive.</summary>
    Usb = 4,

    /// <summary>Mudanças de rede.</summary>
    Network = 8,

    /// <summary>Mudanças de horário.</summary>
    Time = 16,

    /// <summary>Conexões e respostas do modo online.</summary>
    Online = 32,

    /// <summary>Acesso ao menu do equipamento.</summary>
    Menu = 64,

    /// <summary>Todas.</summary>
    All = Config | Api | Usb | Network | Time | Online | Menu,
}
