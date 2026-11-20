using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Tipo de dado de uma coluna de tabela customizada.</summary>
public enum CustomFieldType
{
    /// <summary><c>INTEGER</c>.</summary>
    Integer,

    /// <summary><c>BIGINT</c>.</summary>
    BigInt,

    /// <summary><c>TEXT</c>.</summary>
    Text,

    /// <summary><c>BLOB</c>.</summary>
    Blob,
}

/// <summary>Quais tabelas <c>object_metadata.fcgi</c> devolve.</summary>
public enum ObjectMetadataFilter
{
    /// <summary>Todas as tabelas (padrão e customizadas).</summary>
    All,

    /// <summary>Só as tabelas customizadas (<c>"custom"</c>).</summary>
    Custom,

    /// <summary>Só as tabelas padrão (<c>"default"</c>).</summary>
    Default,
}

/// <summary>Referência a um campo de outra tabela (chave estrangeira ou item de <c>joins</c>).</summary>
public sealed class ForeignKey
{
    /// <summary>Nome da tabela referenciada.</summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    /// <summary>Nome do campo referenciado.</summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;
}

/// <summary>
/// Definição de uma coluna de tabela customizada (<c>object_add</c>, <c>object_add_field</c>, <c>object_update_field</c>).
/// </summary>
public sealed class CustomFieldDefinition
{
    /// <summary>Nome interno da coluna (<c>column_name</c>).</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>Rótulo de exibição (<c>name</c>). Obrigatório ao criar; ao atualizar, vazio mantém o atual.</summary>
    public string? Name { get; set; }

    /// <summary>Tipo de dado.</summary>
    public CustomFieldType Type { get; set; } = CustomFieldType.Text;

    /// <summary>Restrição <c>NOT_NULL</c> (<c>constraint</c>). Ao adicionar um campo, exige <see cref="DefaultValue"/>.</summary>
    public bool NotNull { get; set; }

    /// <summary>Restrição <c>unique</c>. Ao atualizar um campo para unique, exige <see cref="DefaultValue"/>.</summary>
    public bool Unique { get; set; }

    /// <summary>Valor padrão (<c>default_value</c>); o tipo deve corresponder a <see cref="Type"/>. Não vale em <c>object_add</c>.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Chave estrangeira, se houver.</summary>
    public ForeignKey? ForeignKey { get; set; }
}

/// <summary>Descritor de um campo em <see cref="ObjectMetadata.Fields"/>.</summary>
public sealed class ObjectFieldMetadata
{
    /// <summary>Tipo de dado (<c>INTEGER</c>, <c>BIGINT</c>, <c>TEXT</c>, <c>BLOB</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Se o campo é chave primária.</summary>
    [JsonPropertyName("primary key")]
    public bool PrimaryKey { get; set; }

    /// <summary>Se o campo tem restrição NOT NULL.</summary>
    [JsonPropertyName("not null")]
    public bool NotNull { get; set; }

    /// <summary>Valor padrão, se houver (a documentação diz texto, mas o equipamento pode mandar número).</summary>
    [JsonPropertyName("default value")]
    public JsonElement? DefaultValue { get; set; }

    /// <summary>Se o campo tem restrição UNIQUE.</summary>
    [JsonPropertyName("unique")]
    public bool Unique { get; set; }

    /// <summary>Chave estrangeira; presente só quando o campo é uma.</summary>
    [JsonPropertyName("foreign key")]
    public ForeignKey? ForeignKey { get; set; }
}

/// <summary>Metadados de uma tabela de objetos (<c>object_metadata.fcgi</c>).</summary>
public sealed class ObjectMetadata
{
    /// <summary>Campos da tabela, por nome.</summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, ObjectFieldMetadata> Fields { get; set; } = new();

    /// <summary>Relacionamentos de join: nome do join para as referências <c>{ object, field }</c>.</summary>
    [JsonPropertyName("joins")]
    public Dictionary<string, List<ForeignKey>> Joins { get; set; } = new();
}
