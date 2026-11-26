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

/// <summary>Opções de <c>user_set_image.fcgi</c> e <c>user_set_image_list.fcgi</c>.</summary>
public sealed class UserImageUploadOptions
{
    /// <summary>Recusa a foto se o rosto já pertence a outro usuário (<c>match</c>).</summary>
    public bool RejectExistingFace { get; set; } = true;

    /// <summary>
    /// Ignora a verificação de qualidade da imagem (<c>bypass_quality_score</c>). Serve para migrar bases de fotos
    /// existentes; fotos sem a verificação podem causar falhas ou lentidão na identificação. Não use no dia a dia.
    /// </summary>
    public bool BypassQualityScore { get; set; }
}
