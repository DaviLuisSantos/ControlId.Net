using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Rep;

/// <summary>
/// Alteração de um usuário em <c>update_users.fcgi</c>. Identifique o usuário por <see cref="Pis"/> (modo legado) ou
/// <see cref="Cpf"/> (Portaria 671); só os campos preenchidos são alterados.
/// </summary>
public sealed class RepUserUpdate
{
    /// <summary>PIS do usuário a alterar (modo legado).</summary>
    [JsonPropertyName("pis")] public long? Pis { get; set; }

    /// <summary>Novo PIS (modo legado).</summary>
    [JsonPropertyName("new_pis")] public long? NewPis { get; set; }

    /// <summary>CPF do usuário a alterar (Portaria 671).</summary>
    [JsonPropertyName("cpf")] public long? Cpf { get; set; }

    /// <summary>Novo CPF (Portaria 671).</summary>
    [JsonPropertyName("new_cpf")] public long? NewCpf { get; set; }

    /// <summary>Nome.</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }

    /// <summary>Cartão de proximidade.</summary>
    [JsonPropertyName("rfid")] public long? Rfid { get; set; }

    /// <summary>Código de barras.</summary>
    [JsonPropertyName("bars")] public string? Bars { get; set; }

    /// <summary>Código usado com a senha para emitir comprovante.</summary>
    [JsonPropertyName("code")] public long? Code { get; set; }

    /// <summary>Senha numérica.</summary>
    [JsonPropertyName("password")] public string? Password { get; set; }

    /// <summary>Administrador.</summary>
    [JsonPropertyName("admin")] public bool? Admin { get; set; }

    /// <summary>Templates biométricos a acrescentar, em base 64.</summary>
    [JsonPropertyName("templates")] public List<string>? Templates { get; set; }

    /// <summary>Remove todos os templates biométricos do usuário.</summary>
    [JsonPropertyName("remove_templates")] public bool? RemoveTemplates { get; set; }

    /// <summary>Remove as faces do usuário (REP iDClass Facial).</summary>
    [JsonPropertyName("remove_faces")] public bool? RemoveFaces { get; set; }

    /// <summary>Data da foto (Unix timestamp), junto com <see cref="Image"/> (REP iDClass Facial).</summary>
    [JsonPropertyName("image_timestamp")] public long? ImageTimestamp { get; set; }

    /// <summary>Nova foto do usuário (JPEG ou PNG); vai em base 64 (REP iDClass Facial).</summary>
    [JsonPropertyName("image")] public byte[]? Image { get; set; }
}

/// <summary>Resultado de <c>import_users_csv.fcgi</c>.</summary>
public sealed class RepCsvImportResult
{
    /// <summary>Usuários importados (os que já existiam ficam como estão).</summary>
    [JsonPropertyName("imported_users_num")] public int ImportedUsers { get; set; }

    /// <summary>Templates biométricos importados.</summary>
    [JsonPropertyName("imported_templates_num")] public int ImportedTemplates { get; set; }

    /// <summary>Linhas do CSV com erro.</summary>
    [JsonPropertyName("error_in_rows")] public List<int> ErrorRows { get; set; } = new();
}
