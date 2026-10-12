using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ControlId.Access.Configuration;

/// <summary>
/// Base das seções de configuração tipadas (<c>general</c>, <c>catra</c>, <c>pjsip</c>...). Propriedades em <c>null</c>
/// não são enviadas; a API recebe tudo como texto (<c>bool</c> vira <c>"1"</c>/<c>"0"</c>).
/// Use com <c>IAccessClient.GetConfigurationSectionAsync&lt;T&gt;()</c> e
/// <see cref="IAccessClient.SetConfigurationSectionAsync"/>.
/// </summary>
public abstract class ConfigurationSection
{
    /// <summary>Cria a seção com o nome do módulo na API.</summary>
    protected ConfigurationSection(string sectionName) => SectionName = sectionName;

    /// <summary>Nome do módulo na API, ex.: <c>general</c>.</summary>
    [JsonIgnore]
    public string SectionName { get; }
}

/// <summary>Converte seções tipadas de/para o formato de <c>get/set_configuration</c>.</summary>
internal static class ConfigurationSectionMapper
{
    private static readonly ConcurrentDictionary<Type, (string Key, PropertyInfo Property)[]> Cache = new();

    public static IReadOnlyList<string> FieldNames(ConfigurationSection section) => Properties(section.GetType()).Select(p => p.Key).ToList();

    public static Dictionary<string, string> ToValues(ConfigurationSection section)
    {
        var values = new Dictionary<string, string>();
        foreach (var (key, property) in Properties(section.GetType()))
        {
            switch (property.GetValue(section))
            {
                case null:
                    break;
                case bool flag:
                    values[key] = flag ? "1" : "0";
                    break;
                case IFormattable number:
                    values[key] = number.ToString(null, CultureInfo.InvariantCulture);
                    break;
                case var other:
                    values[key] = other.ToString()!;
                    break;
            }
        }

        return values;
    }

    public static void Populate(ConfigurationSection section, IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, property) in Properties(section.GetType()))
        {
            if (!values.TryGetValue(key, out var text))
            {
                continue;
            }

            var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object? value = target switch
            {
                _ when target == typeof(string) => text,
                _ when target == typeof(bool) => text.Trim().ToLowerInvariant() switch
                {
                    "1" or "true" => true,
                    "0" or "false" => false,
                    _ => null,
                },
                _ when target == typeof(int) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
                _ when target == typeof(long) => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null,
                _ when target == typeof(double) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
                _ => null,
            };

            // Valor fora do formato esperado (firmware diferente): mantém null em vez de falhar a leitura toda.
            property.SetValue(section, value);
        }
    }

    private static (string Key, PropertyInfo Property)[] Properties(Type type) => Cache.GetOrAdd(type, static t =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Key: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name, Property: p))
            .Where(x => x.Key is not null && x.Property.CanWrite)
            .Select(x => (x.Key!, x.Property))
            .ToArray());
}
