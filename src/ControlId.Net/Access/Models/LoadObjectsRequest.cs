using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Corpo de <c>load_objects.fcgi</c>. Veja a "Lista de Objetos" da documentação Control iD
/// para os nomes de objeto válidos (<c>users</c>, <c>cards</c>, <c>access_logs</c>, <c>groups</c>...).
/// </summary>
public sealed class LoadObjectsRequest
{
    /// <summary>Cria a requisição para o objeto informado.</summary>
    public LoadObjectsRequest(string @object) => Object = @object;

    /// <summary>Nome do objeto (tabela) a carregar.</summary>
    [JsonPropertyName("object")]
    public string Object { get; }

    /// <summary>Campos a retornar. <c>null</c> retorna todos.</summary>
    [JsonPropertyName("fields")]
    public IList<string>? Fields { get; set; }

    /// <summary>
    /// Filtro no formato do equipamento, por exemplo
    /// <c>new { users = new { id = new Dictionary&lt;string, object&gt; { [">="] = 10 } } }</c>.
    /// Use <see cref="Where"/> para montar filtros simples.
    /// </summary>
    [JsonPropertyName("where")]
    public object? Where { get; set; }

    /// <summary>Ordenação: nomes de campo e opcionalmente <c>"ascending"</c>/<c>"descending"</c>.</summary>
    [JsonPropertyName("order")]
    public IList<string>? Order { get; set; }

    /// <summary>Quantidade máxima de registros.</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>Registros a pular (exige <see cref="Limit"/>).</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>Tipo de join: <c>"LEFT"</c> ou <c>"INNER"</c>.</summary>
    [JsonPropertyName("join")]
    public string? Join { get; set; }

    // Cópia com outra página, para a leitura paginada não alterar a requisição do chamador.
    internal LoadObjectsRequest WithPage(int limit, int offset) => new(Object)
    {
        Fields = Fields,
        Where = Where,
        Order = Order,
        Join = Join,
        Limit = limit,
        Offset = offset,
    };
}
