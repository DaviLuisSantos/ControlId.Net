using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Resposta de <c>hid_get_key_info.fcgi</c>: o que uma referência de chave do leitor HID contém (a chave em si nunca é devolvida).</summary>
public sealed class HidKeyInfo
{
    /// <summary><c>"KEK"</c> (chave de criptografia de chaves) ou <c>"MEDIA"</c> (chave de cartão).</summary>
    [JsonPropertyName("key_type")]
    public string KeyType { get; set; } = string.Empty;

    /// <summary>
    /// Identificador gravado junto à chave, em hexadecimal, ou vazio. Só a KEK padrão aceita identificador, então chaves
    /// injetadas como KEK intermediária ou chave de mídia sempre voltam com este campo vazio.
    /// </summary>
    [JsonPropertyName("key_id")]
    public string KeyId { get; set; } = string.Empty;

    /// <summary><c>true</c> quando a referência guarda uma KEK.</summary>
    [JsonIgnore]
    public bool IsKek => string.Equals(KeyType, "KEK", StringComparison.OrdinalIgnoreCase);

    /// <summary><c>true</c> quando a referência guarda uma chave de cartão.</summary>
    [JsonIgnore]
    public bool IsMediaKey => string.Equals(KeyType, "MEDIA", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Tipo inferido do valor de um OID do leitor HID.</summary>
public enum HidOidValueKind
{
    /// <summary>Booleano (o leitor devolveu 1 byte).</summary>
    Boolean,

    /// <summary>Inteiro (o leitor devolveu 4 bytes).</summary>
    Integer,

    /// <summary>Sequência de bytes (qualquer outro tamanho), devolvida em hexadecimal.</summary>
    Bytes,
}

/// <summary>
/// Valor de um OID do leitor HID (<c>hid_get_oid.fcgi</c>). O leitor devolve bytes crus e o equipamento infere o tipo pelo
/// tamanho: 1 byte vira booleano, 4 bytes viram inteiro e o resto vira hexadecimal.
/// </summary>
public sealed class HidOidValue
{
    private HidOidValue(HidOidValueKind kind, bool boolean, int integer, string hex)
    {
        Kind = kind;
        Boolean = boolean;
        Integer = integer;
        Hex = hex;
    }

    /// <summary>Tipo inferido pelo equipamento.</summary>
    public HidOidValueKind Kind { get; }

    /// <summary>O valor, quando <see cref="Kind"/> é <see cref="HidOidValueKind.Boolean"/>.</summary>
    public bool Boolean { get; }

    /// <summary>O valor, quando <see cref="Kind"/> é <see cref="HidOidValueKind.Integer"/>.</summary>
    public int Integer { get; }

    /// <summary>O valor em hexadecimal, quando <see cref="Kind"/> é <see cref="HidOidValueKind.Bytes"/>; vazio nos demais.</summary>
    public string Hex { get; }

    /// <summary>Os bytes, quando <see cref="Kind"/> é <see cref="HidOidValueKind.Bytes"/>; vazio nos demais.</summary>
    public byte[] ToBytes() => Kind == HidOidValueKind.Bytes ? HidHex.ToBytes(Hex) : Array.Empty<byte>();

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        HidOidValueKind.Boolean => Boolean ? "true" : "false",
        HidOidValueKind.Integer => Integer.ToString(CultureInfo.InvariantCulture),
        _ => Hex,
    };

    internal static HidOidValue FromJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => new HidOidValue(HidOidValueKind.Boolean, true, 0, string.Empty),
        JsonValueKind.False => new HidOidValue(HidOidValueKind.Boolean, false, 0, string.Empty),
        JsonValueKind.Number => new HidOidValue(HidOidValueKind.Integer, false, value.GetInt32(), string.Empty),
        JsonValueKind.String => new HidOidValue(HidOidValueKind.Bytes, false, 0, value.GetString() ?? string.Empty),
        _ => throw new ControlIdException($"Valor de OID inesperado na resposta de hid_get_oid.fcgi ({value.ValueKind})."),
    };
}

/// <summary>Andamento do reset das configurações do leitor HID (<c>hid_reset_config.fcgi</c>).</summary>
public sealed class HidConfigResetStatus
{
    /// <summary><c>true</c> enquanto o reset está em andamento.</summary>
    [JsonPropertyName("resetting")]
    public bool IsResetting { get; set; }

    /// <summary><c>"running"</c> ou <c>"done"</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary><c>true</c> quando o reset terminou.</summary>
    [JsonIgnore]
    public bool IsDone => !IsResetting && string.Equals(Status, "done", StringComparison.OrdinalIgnoreCase);
}

internal static class HidHex
{
    public static bool IsHex(string value)
    {
        if (value.Length == 0 || value.Length % 2 != 0)
        {
            return false;
        }

        foreach (var c in value)
        {
            var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }

    public static string FromBytes(byte[] bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = Digit(bytes[i] >> 4);
            chars[(i * 2) + 1] = Digit(bytes[i] & 0xF);
        }

        return new string(chars);
    }

    public static byte[] ToBytes(string hex)
    {
        if (!IsHex(hex))
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }

    private static char Digit(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);
}
