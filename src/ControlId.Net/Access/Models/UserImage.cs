using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Foto de um usuário para envio em lote (<c>user_set_image_list.fcgi</c>).</summary>
public sealed class UserImage
{
    /// <summary>Cria a foto do usuário.</summary>
    public UserImage(long userId, byte[] image)
    {
        UserId = userId;
        Image = image;
    }

    /// <summary>ID do usuário.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; }

    /// <summary>Imagem JPEG (vai em Base64 no JSON).</summary>
    [JsonPropertyName("image")]
    public byte[] Image { get; }
}
