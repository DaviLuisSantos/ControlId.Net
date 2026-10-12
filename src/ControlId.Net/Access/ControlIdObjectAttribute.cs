using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Liga um modelo ao objeto (tabela) da API de acesso, para os métodos genéricos
/// <see cref="IAccessClient.GetObjectsAsync{T}"/>, <see cref="IAccessClient.CreateObjectAsync{T}"/>,
/// <see cref="IAccessClient.UpdateObjectAsync{T}"/> e <see cref="IAccessClient.DeleteObjectAsync{T}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ControlIdObjectAttribute : Attribute
{
    /// <summary>Marca o modelo com o nome do objeto na API, ex.: <c>"qrcodes"</c>.</summary>
    public ControlIdObjectAttribute(string name) => Name = name;

    /// <summary>Nome do objeto na API.</summary>
    public string Name { get; }

    /// <summary>
    /// Campo que identifica o registro em alterações e remoções. Quase sempre <c>id</c>; <c>null</c> em tabelas de
    /// associação, que não têm chave própria.
    /// </summary>
    public string? Key { get; set; } = "id";
}

/// <summary>Consulta o <see cref="ControlIdObjectAttribute"/> de um modelo.</summary>
public static class ControlIdObjects
{
    private static readonly ConcurrentDictionary<Type, Info> Cache = new();

    /// <summary>Nome do objeto na API para <typeparamref name="T"/>, ex.: <c>NameOf&lt;AccessQrCode&gt;()</c> = <c>"qrcodes"</c>.</summary>
    public static string NameOf<T>() => Get(typeof(T)).Name;

    internal static Info Get(Type type) => Cache.GetOrAdd(type, static t =>
    {
        var attribute = t.GetCustomAttribute<ControlIdObjectAttribute>()
            ?? throw new InvalidOperationException(
                $"{t.Name} não tem [ControlIdObject]. Use LoadObjectsAsync/CreateObjectsAsync com o nome do objeto.");

        PropertyInfo? keyProperty = null;
        if (attribute.Key is { } key)
        {
            keyProperty = t.GetProperties().FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == key)
                ?? throw new InvalidOperationException($"{t.Name} não tem propriedade para o campo-chave '{key}'.");
        }

        return new Info(attribute.Name, attribute.Key, keyProperty);
    });

    internal sealed class Info
    {
        public Info(string name, string? key, PropertyInfo? keyProperty)
        {
            Name = name;
            Key = key;
            KeyProperty = keyProperty;
        }

        public string Name { get; }

        public string? Key { get; }

        public PropertyInfo? KeyProperty { get; }

        public long GetKey(object value)
        {
            if (Key is null || KeyProperty is null)
            {
                throw new InvalidOperationException($"'{Name}' é uma tabela de associação, sem chave: use LinkAsync/UnlinkAsync.");
            }

            return KeyProperty.GetValue(value) switch
            {
                long l => l,
                int i => i,
                null => throw new ArgumentException($"Preencha o campo-chave '{Key}' para alterar ou remover em '{Name}'."),
                var other => Convert.ToInt64(other, System.Globalization.CultureInfo.InvariantCulture),
            };
        }
    }
}
