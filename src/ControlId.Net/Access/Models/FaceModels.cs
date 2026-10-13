using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Notas de enquadramento e qualidade de uma foto de rosto.</summary>
public sealed class FaceImageScores
{
    /// <summary>Largura do rosto.</summary>
    [JsonPropertyName("bounds_width")]
    public int BoundsWidth { get; set; }

    /// <summary>Distância horizontal do centro do rosto ao centro da imagem.</summary>
    [JsonPropertyName("horizontal_center_offset")]
    public int HorizontalCenterOffset { get; set; }

    /// <summary>Distância vertical do centro do rosto ao centro da imagem.</summary>
    [JsonPropertyName("vertical_center_offset")]
    public int VerticalCenterOffset { get; set; }

    /// <summary>Qualidade da pose (rosto de frente para a câmera).</summary>
    [JsonPropertyName("center_pose_quality")]
    public int CenterPoseQuality { get; set; }

    /// <summary>Nitidez.</summary>
    [JsonPropertyName("sharpness_quality")]
    public int SharpnessQuality { get; set; }
}

/// <summary>Erro de cadastro, teste ou extração de rosto. Veja <see cref="FaceImageErrorCode"/>.</summary>
public sealed class FaceImageError
{
    /// <summary>Código do erro.</summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>Mensagem do equipamento, ex.: <c>Low sharpness</c>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Com <see cref="FaceImageErrorCode.FaceExists"/>: de quem é o rosto parecido.</summary>
    [JsonPropertyName("info")]
    public FaceMatchInfo? Info { get; set; }

    /// <summary><see cref="Code"/> como enum.</summary>
    [JsonIgnore]
    public FaceImageErrorCode CodeType => (FaceImageErrorCode)Code;
}

/// <summary>Rosto já cadastrado que coincidiu com o enviado.</summary>
public sealed class FaceMatchInfo
{
    /// <summary>Usuário dono do rosto parecido.</summary>
    [JsonPropertyName("match_user_id")]
    public long? MatchUserId { get; set; }

    /// <summary>Pontuação da coincidência.</summary>
    [JsonPropertyName("match_score")]
    public int? MatchScore { get; set; }

    /// <summary>Similaridade.</summary>
    [JsonPropertyName("match_similarity")]
    public int? MatchSimilarity { get; set; }

    /// <summary>Confiança da coincidência (relatada junto com limiares personalizados).</summary>
    [JsonPropertyName("match_confidence")]
    public int? MatchConfidence { get; set; }
}

/// <summary>Códigos de erro documentados para fotos e templates faciais.</summary>
public enum FaceImageErrorCode
{
    /// <summary>1: erro nos parâmetros (arquivo inválido, usuário inexistente...).</summary>
    InvalidRequest = 1,

    /// <summary>2: nenhum rosto detectado.</summary>
    FaceNotDetected = 2,

    /// <summary>3: rosto já cadastrado para outro usuário (veja <see cref="FaceImageError.Info"/>).</summary>
    FaceExists = 3,

    /// <summary>4: rosto fora do centro da imagem.</summary>
    FaceNotCentered = 4,

    /// <summary>5: rosto muito distante.</summary>
    FaceTooDistant = 5,

    /// <summary>6: rosto muito próximo.</summary>
    FaceTooClose = 6,

    /// <summary>7: rosto torto em relação à câmera.</summary>
    FacePoseNotCentered = 7,

    /// <summary>8: pouca nitidez.</summary>
    LowSharpness = 8,

    /// <summary>9: rosto muito próximo das bordas.</summary>
    FaceTooCloseToBorders = 9,

    /// <summary>11: imagem em tons de cinza (com <c>face_enroll_mode</c> 1).</summary>
    GrayscaleImage = 11,

    /// <summary>12: mais de um rosto na imagem (com <c>face_enroll_mode</c> 1).</summary>
    MultipleFaces = 12,
}

/// <summary>
/// Resultado de cadastro, teste ou extração de uma foto de rosto (<c>user_set_image</c>, <c>user_test_image</c>,
/// <c>face_template_extract</c>, <c>face_template_enroll</c> e suas versões em lote).
/// </summary>
public sealed class FaceImageResult
{
    /// <summary>Usuário (cadastros).</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Identificador do item no lote de extração (<c>face_template_extract_list</c>).</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    private bool? _success;

    /// <summary>
    /// A operação deu certo. Quando o equipamento não manda <c>success</c> (o iDFace não manda em
    /// <c>face_template_extract</c>), vale a ausência de erros.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success
    {
        get => _success ?? (Errors is null || Errors.Count == 0);
        set => _success = value;
    }

    /// <summary>Notas de enquadramento e qualidade.</summary>
    [JsonPropertyName("scores")]
    public FaceImageScores? Scores { get; set; }

    /// <summary>Motivos da recusa.</summary>
    [JsonPropertyName("errors")]
    public List<FaceImageError> Errors { get; set; } = new();

    /// <summary>Template facial em Base64 (extrações).</summary>
    [JsonPropertyName("face_template")]
    public string? FaceTemplate { get; set; }

    /// <summary>Lança <see cref="ControlIdException"/> se a operação falhou, com as mensagens do equipamento.</summary>
    public FaceImageResult EnsureSuccess() =>
        Success
            ? this
            : throw new ControlIdException("Foto de rosto recusada: " + (Errors.Count == 0
                ? "sem mensagem do equipamento."
                : string.Join("; ", Errors.Select(e => $"{e.Code} {e.Message}"))));
}

/// <summary>Usuário com foto e quando ela foi cadastrada (<c>user_list_images</c> com <c>get_timestamp</c>).</summary>
public sealed class UserImageInfo
{
    /// <summary>Usuário.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    /// <summary>Cadastro da foto (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

/// <summary>Foto de um usuário com a data de cadastro (<c>user_get_image</c> com <c>get_timestamp</c>).</summary>
public sealed class UserImageData
{
    /// <summary>Cadastro da foto (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>Imagem (JPEG).</summary>
    [JsonPropertyName("image")]
    public byte[]? Image { get; set; }
}

/// <summary>Item de <c>user_get_image_list</c>: a foto ou o erro daquele usuário.</summary>
public sealed class UserImageEntry
{
    /// <summary>Usuário.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Cadastro da foto (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    /// <summary>Imagem (JPEG), quando deu certo.</summary>
    [JsonPropertyName("image")]
    public byte[]? Image { get; set; }

    /// <summary>Erro, quando a foto não pôde ser obtida (ex.: usuário inexistente).</summary>
    [JsonPropertyName("error")]
    public FaceImageError? Error { get; set; }
}

/// <summary>Imagem para extração em lote (<c>face_template_extract_list</c>).</summary>
public sealed class FaceTemplateImage
{
    /// <summary>Cria o item.</summary>
    public FaceTemplateImage(long id, byte[] image)
    {
        Id = id;
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>Identificador do item no lote (volta em <see cref="FaceImageResult.Id"/>).</summary>
    [JsonPropertyName("id")]
    public long Id { get; }

    /// <summary>Imagem (vai em Base64).</summary>
    [JsonPropertyName("image")]
    public byte[] Image { get; }
}

/// <summary>Template facial a cadastrar (<c>face_template_enroll</c>).</summary>
public sealed class FaceTemplateEnrollment
{
    /// <summary>Cria o item; sem <paramref name="timestamp"/>, usa o momento atual.</summary>
    public FaceTemplateEnrollment(long userId, string faceTemplate, DateTimeOffset? timestamp = null)
    {
        UserId = userId;
        FaceTemplate = faceTemplate ?? throw new ArgumentNullException(nameof(faceTemplate));
        Timestamp = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
    }

    /// <summary>Usuário.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; }

    /// <summary>Momento do cadastro (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; }

    /// <summary>Template facial em Base64 (ex.: de <see cref="FaceImageResult.FaceTemplate"/>).</summary>
    [JsonPropertyName("face_template")]
    public string FaceTemplate { get; }
}

/// <summary>Câmera de <c>save_screenshot</c>.</summary>
public enum CameraType
{
    /// <summary>Colorida (<c>rgb</c>).</summary>
    Rgb,

    /// <summary>Infravermelha (<c>ir</c>).</summary>
    Infrared,
}

/// <summary>Objeto a incluir em <c>export_objects</c>.</summary>
public sealed class ExportObjectRequest
{
    /// <summary>Exporta o objeto inteiro ou só as colunas informadas.</summary>
    public ExportObjectRequest(string @object, params string[] columns)
    {
        Object = string.IsNullOrWhiteSpace(@object) ? throw new ArgumentException("Informe o objeto.", nameof(@object)) : @object;
        Columns = columns is { Length: > 0 } ? columns.ToList() : null;
    }

    /// <summary>Nome do objeto, ex.: <c>users</c>.</summary>
    [JsonPropertyName("object")]
    public string Object { get; }

    /// <summary>Colunas exportadas; <c>null</c> exporta todas.</summary>
    [JsonPropertyName("columns")]
    public List<string>? Columns { get; }
}
