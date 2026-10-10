using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>users</c> da linha de acesso.</summary>
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

    /// <summary>0 = usuário comum, 1 = visitante.</summary>
    [JsonPropertyName("user_type_id")]
    public int? UserTypeId { get; set; }

    /// <summary>Início da validade (Unix timestamp; 0 = sem limite).</summary>
    [JsonPropertyName("begin_time")]
    public long? BeginTime { get; set; }

    /// <summary>Fim da validade (Unix timestamp; 0 = sem limite).</summary>
    [JsonPropertyName("end_time")]
    public long? EndTime { get; set; }
}
