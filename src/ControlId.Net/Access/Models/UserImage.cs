using System;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Foto de um usuário para envio em lote (<c>user_set_image_list.fcgi</c>).</summary>
public sealed class UserImage
{
    /// <summary>Cria a foto do usuário; sem <paramref name="timestamp"/>, usa o momento atual (a API exige a data).</summary>
    public UserImage(long userId, byte[] image, DateTimeOffset? timestamp = null)
    {
        UserId = userId;
        Image = image ?? throw new ArgumentNullException(nameof(image));
        Timestamp = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
    }

    /// <summary>Data do cadastro da foto (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; }

    /// <summary>ID do usuário.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; }

    /// <summary>Imagem JPEG (vai em Base64 no JSON).</summary>
    [JsonPropertyName("image")]
    public byte[] Image { get; }
}
