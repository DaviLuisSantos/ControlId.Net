using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>Objeto <c>users</c> da linha de acesso.</summary>
[ControlIdObject("users")]
public sealed class AccessUser
{
    /// <summary>ID no equipamento. Deixe <c>null</c> ao criar para o equipamento gerar.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Matrícula.</summary>
    [JsonPropertyName("registration")]
    public string? Registration { get; set; }

    /// <summary>Nome.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Senha de acesso (o equipamento guarda o hash).</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>Salt da senha.</summary>
    [JsonPropertyName("salt")]
    public string? Salt { get; set; }

    /// <summary>
    /// Tipo do usuário, uma chave da tabela <c>user_types</c> (no iDFace, 1 = visitante); <c>null</c> = usuário comum.
    /// O equipamento devolve 0 quando o campo está vazio, mas recusa gravar 0 (chave estrangeira); por isso 0 é lido como
    /// <c>null</c>, e um usuário lido pode ser regravado sem erro.
    /// </summary>
    [JsonPropertyName("user_type_id"), JsonConverter(typeof(ZeroAsNullConverter))]
    public int? UserTypeId { get; set; }

    /// <summary>Início da validade (Unix timestamp; 0 = sem limite).</summary>
    [JsonPropertyName("begin_time")]
    public long? BeginTime { get; set; }

    /// <summary>Fim da validade (Unix timestamp; 0 = sem limite).</summary>
    [JsonPropertyName("end_time")]
    public long? EndTime { get; set; }

    /// <summary>Quando a foto do usuário foi gravada (Unix timestamp).</summary>
    [JsonPropertyName("image_timestamp")]
    public long? ImageTimestamp { get; set; }

    /// <summary>Último acesso do usuário (Unix timestamp; 0 = ainda não acessou).</summary>
    [JsonPropertyName("last_access")]
    public long? LastAccess { get; set; }
}
