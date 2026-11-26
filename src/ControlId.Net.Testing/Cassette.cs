using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ControlId.Testing;

/// <summary>Uma troca gravada: a requisição enviada ao equipamento e a resposta que ele deu.</summary>
public sealed class CassetteEntry
{
    /// <summary>Método HTTP (POST, GET...).</summary>
    public string Method { get; set; } = "POST";

    /// <summary>Caminho e consulta da requisição, com o valor de <c>session</c> mascarado.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Corpo da requisição, com os campos sensíveis mascarados; <c>null</c> se não havia corpo.</summary>
    public string? RequestBody { get; set; }

    /// <summary>Indica que <see cref="RequestBody"/> está em base64 (corpo que não é texto).</summary>
    public bool RequestBodyIsBase64 { get; set; }

    /// <summary>Código HTTP da resposta.</summary>
    public int StatusCode { get; set; } = 200;

    /// <summary>Tipo de conteúdo da resposta.</summary>
    public string? ResponseContentType { get; set; }

    /// <summary>Corpo da resposta, com os campos sensíveis mascarados.</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>Indica que <see cref="ResponseBody"/> está em base64 (resposta que não é texto).</summary>
    public bool ResponseBodyIsBase64 { get; set; }
}

/// <summary>
/// Conversa gravada com um equipamento, em ordem. Gravada por <see cref="RecordingHandler"/>, reproduzida por
/// <see cref="ReplayHandler"/> e guardada como JSON.
/// </summary>
public sealed class Cassette
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Texto que substitui o valor dos campos mascarados.</summary>
    public const string MaskValue = "***";

    /// <summary>Campos cujo valor é sempre mascarado: <c>session</c>, <c>login</c>, <c>password</c>, <c>salt</c>, <c>image</c> e <c>template</c>.</summary>
    public static IReadOnlyCollection<string> DefaultMaskedFields { get; } =
        new[] { "session", "login", "password", "salt", "image", "template" };

    /// <summary>As trocas gravadas, na ordem em que aconteceram.</summary>
    public IList<CassetteEntry> Entries { get; set; } = new List<CassetteEntry>();

    /// <summary>Lê um cassete de um arquivo JSON.</summary>
    /// <exception cref="InvalidDataException">O arquivo não é um cassete válido.</exception>
    public static Cassette Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Informe o caminho do cassete.", nameof(path));
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        try
        {
            return JsonSerializer.Deserialize<Cassette>(json, JsonOptions)
                ?? throw new InvalidDataException($"O arquivo '{path}' está vazio e não é um cassete.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"O arquivo '{path}' não é um cassete válido: {ex.Message}", ex);
        }
    }

    /// <summary>Grava o cassete num arquivo JSON, criando a pasta se preciso.</summary>
    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Informe o caminho do cassete.", nameof(path));
        }

        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json;
        lock (Entries)
        {
            json = JsonSerializer.Serialize(this, JsonOptions);
        }

        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    /// <summary>Troca por <see cref="MaskValue"/> o valor dos campos sensíveis num JSON; texto que não é JSON volta como está.</summary>
    internal static string MaskJson(string text, ISet<string> fields)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return text;
        }

        if (node is null)
        {
            return text;
        }

        MaskNode(node, fields);
        return node.ToJsonString();
    }

    private static void MaskNode(JsonNode node, ISet<string> fields)
    {
        if (node is JsonObject obj)
        {
            foreach (var name in obj.Select(p => p.Key).ToList())
            {
                if (fields.Contains(name) && obj[name] is JsonValue)
                {
                    obj[name] = MaskValue;
                }
                else if (obj[name] is { } child)
                {
                    MaskNode(child, fields);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    MaskNode(item, fields);
                }
            }
        }
    }

    /// <summary>Mascara o valor de <c>session</c> (e dos demais campos) na consulta da URL.</summary>
    internal static string MaskPath(Uri uri, ISet<string> fields)
    {
        var query = uri.Query;
        if (query.Length <= 1)
        {
            return uri.AbsolutePath;
        }

        var parts = query.Substring(1).Split('&');
        for (var i = 0; i < parts.Length; i++)
        {
            var eq = parts[i].IndexOf('=');
            if (eq > 0 && fields.Contains(Uri.UnescapeDataString(parts[i].Substring(0, eq))))
            {
                parts[i] = parts[i].Substring(0, eq + 1) + MaskValue;
            }
        }

        return uri.AbsolutePath + "?" + string.Join("&", parts);
    }

    internal static HashSet<string> BuildFields(IEnumerable<string>? extra)
    {
        var set = new HashSet<string>(DefaultMaskedFields, StringComparer.OrdinalIgnoreCase);
        if (extra is not null)
        {
            foreach (var field in extra)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    set.Add(field);
                }
            }
        }

        return set;
    }

    internal static bool IsText(string? mediaType) =>
        mediaType is not null
        && (mediaType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0
            || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0
            || mediaType.IndexOf("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>Compara dois corpos: JSON pela estrutura (ignora espaços), o resto pelo texto.</summary>
    internal static bool BodiesEqual(string? expected, string? actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return true;
        }

        if (expected is null || actual is null)
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
