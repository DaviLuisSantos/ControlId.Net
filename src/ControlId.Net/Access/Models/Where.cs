using System.Collections.Generic;

namespace ControlId.Access;

/// <summary>
/// Atalhos para montar o filtro <c>where</c> da API de acesso:
/// <c>{ "&lt;objeto&gt;": { "&lt;campo&gt;": { "&lt;operador&gt;": valor } } }</c>.
/// </summary>
public static class Where
{
    /// <summary><c>campo = valor</c>.</summary>
    public static object Equal(string @object, string field, object value) =>
        Wrap(@object, field, value);

    /// <summary><c>campo &lt;operador&gt; valor</c>. Operadores: =, !=, &gt;, &lt;, &gt;=, &lt;=, LIKE, NOT LIKE, IN, NOT IN.</summary>
    public static object Compare(string @object, string field, string @operator, object value) =>
        Wrap(@object, field, new Dictionary<string, object> { [@operator] = value });

    /// <summary><c>campo IN (valores)</c>. O equipamento recusa listas com mais de 999 itens.</summary>
    public static object In<T>(string @object, string field, IEnumerable<T> values) =>
        Compare(@object, field, "IN", values);

    private static object Wrap(string @object, string field, object condition) =>
        new Dictionary<string, object>
        {
            [@object] = new Dictionary<string, object> { [field] = condition },
        };
}
