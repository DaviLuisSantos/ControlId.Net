using System.Globalization;
using System.Text.Json.Nodes;

namespace ControlId.Simulator;

/// <summary>
/// Tabelas do equipamento em memória, com a semântica de <c>load_objects</c>, <c>create_objects</c>,
/// <c>modify_objects</c> e <c>destroy_objects</c>: filtro <c>where</c>, ordem, <c>limit</c>/<c>offset</c> e projeção.
/// Não é thread-safe: quem usa segura o lock do <see cref="DeviceState"/>.
/// </summary>
public sealed class ObjectStore
{
    // Tabelas cuja chave é um "id" gerado pelo equipamento. As demais (associações como user_groups) não têm ID.
    private static readonly HashSet<string> IdTables = new(StringComparer.Ordinal)
    {
        "access_events", "access_logs", "access_rules", "alarm_logs", "areas", "cards", "catra_infos", "change_logs",
        "contacts", "contingency_cards", "custom_thresholds", "devices", "groups", "holidays", "log_types",
        "network_interlocking_rules", "pins", "portals", "qrcodes", "scheduled_unlocks", "sec_boxs", "templates",
        "time_spans", "time_zones", "timed_alarms", "uhf_tags", "users",
    };

    private readonly Dictionary<string, List<JsonObject>> _tables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastIds = new(StringComparer.Ordinal);

    /// <summary>Linhas de uma tabela (vazia se não existir).</summary>
    public IReadOnlyList<JsonObject> Rows(string table) =>
        _tables.TryGetValue(table, out var rows) ? rows : Array.Empty<JsonObject>();

    /// <summary>Nomes das tabelas com pelo menos uma linha.</summary>
    public IEnumerable<string> TableNames => _tables.Where(t => t.Value.Count > 0).Select(t => t.Key);

    /// <summary>Insere as linhas e devolve os IDs gerados (vazio para tabelas sem ID).</summary>
    public List<long> Insert(string table, IEnumerable<JsonObject> values)
    {
        var rows = Table(table);
        var ids = new List<long>();
        foreach (var value in values)
        {
            var row = (JsonObject)value.DeepClone();
            if (IdTables.Contains(table))
            {
                var id = TryGetLong(row["id"], out var given) && given > 0 ? given : NextId(table);
                if (rows.Any(r => TryGetLong(r["id"], out var existing) && existing == id))
                {
                    throw new DeviceException($"UNIQUE constraint failed: {table}.id");
                }

                _lastIds[table] = Math.Max(_lastIds.GetValueOrDefault(table), id);
                row["id"] = id;
                ids.Add(id);
            }

            rows.Add(row);
        }

        return ids;
    }

    /// <summary>Consulta com a semântica de <c>load_objects</c>.</summary>
    public List<JsonObject> Query(string table, JsonNode? where, IList<string>? order, int? limit, int? offset, IList<string>? fields)
    {
        IEnumerable<JsonObject> rows = Rows(table).Where(r => Matches(table, r, where));

        if (order is { Count: > 0 })
        {
            var descending = string.Equals(order[0], "descending", StringComparison.OrdinalIgnoreCase);
            var keys = order.Where(o => o is not "ascending" and not "descending").ToList();
            IOrderedEnumerable<JsonObject>? sorted = null;
            foreach (var key in keys)
            {
                sorted = sorted is null
                    ? descending ? rows.OrderByDescending(r => r[key], NodeComparer.Instance) : rows.OrderBy(r => r[key], NodeComparer.Instance)
                    : descending ? sorted.ThenByDescending(r => r[key], NodeComparer.Instance) : sorted.ThenBy(r => r[key], NodeComparer.Instance);
            }

            rows = sorted ?? rows;
        }

        if (offset is > 0)
        {
            rows = rows.Skip(offset.Value);
        }

        if (limit is >= 0)
        {
            rows = rows.Take(limit.Value);
        }

        return rows.Select(r => Project(r, fields)).ToList();
    }

    /// <summary>Altera as linhas que atendem ao filtro; devolve quantas mudaram.</summary>
    public List<JsonObject> Modify(string table, JsonObject values, JsonNode? where)
    {
        var changed = new List<JsonObject>();
        foreach (var row in Rows(table).Where(r => Matches(table, r, where)))
        {
            foreach (var (key, value) in values)
            {
                if (key == "id" && IdTables.Contains(table))
                {
                    continue; // a chave não muda
                }

                row[key] = value?.DeepClone();
            }

            changed.Add(row);
        }

        return changed;
    }

    /// <summary>Remove até <paramref name="limit"/> linhas que atendem ao filtro.</summary>
    public List<JsonObject> Delete(string table, JsonNode? where, int? limit)
    {
        if (!_tables.TryGetValue(table, out var rows))
        {
            return new List<JsonObject>();
        }

        var removed = rows.Where(r => Matches(table, r, where)).Take(limit is > 0 ? limit.Value : int.MaxValue).ToList();
        foreach (var row in removed)
        {
            rows.Remove(row);
        }

        return removed;
    }

    /// <summary>Apaga tudo.</summary>
    public void Clear()
    {
        _tables.Clear();
        _lastIds.Clear();
    }

    private List<JsonObject> Table(string table)
    {
        if (!_tables.TryGetValue(table, out var rows))
        {
            rows = new List<JsonObject>();
            _tables[table] = rows;
        }

        return rows;
    }

    private long NextId(string table) => _lastIds.GetValueOrDefault(table) + 1;

    private static JsonObject Project(JsonObject row, IList<string>? fields)
    {
        if (fields is not { Count: > 0 })
        {
            return (JsonObject)row.DeepClone();
        }

        var projected = new JsonObject();
        foreach (var field in fields)
        {
            projected[field] = row[field]?.DeepClone();
        }

        return projected;
    }

    // where = { "tabela": { "campo": valor | { "operador": valor } } }. Condições de outras tabelas (joins) são ignoradas.
    private static bool Matches(string table, JsonObject row, JsonNode? where)
    {
        if (where is not JsonObject conditions || conditions[table] is not JsonObject fields)
        {
            return true;
        }

        foreach (var (field, condition) in fields)
        {
            var actual = row[field];
            if (condition is JsonObject operators)
            {
                foreach (var (op, expected) in operators)
                {
                    if (!Compare(actual, op, expected))
                    {
                        return false;
                    }
                }
            }
            else if (NodeComparer.Instance.Compare(actual, condition) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Compare(JsonNode? actual, string op, JsonNode? expected)
    {
        var cmp = NodeComparer.Instance;
        return op.ToUpperInvariant() switch
        {
            "=" or "==" => cmp.Compare(actual, expected) == 0,
            "!=" or "<>" => cmp.Compare(actual, expected) != 0,
            ">" => cmp.Compare(actual, expected) > 0,
            ">=" => cmp.Compare(actual, expected) >= 0,
            "<" => cmp.Compare(actual, expected) < 0,
            "<=" => cmp.Compare(actual, expected) <= 0,
            "IN" => expected is JsonArray list && list.Any(v => cmp.Compare(actual, v) == 0),
            "NOT IN" => expected is JsonArray list && list.All(v => cmp.Compare(actual, v) != 0),
            "LIKE" => Like(actual, expected),
            _ => throw new DeviceException($"Invalid operator: {op}"),
        };
    }

    private static bool Like(JsonNode? actual, JsonNode? pattern)
    {
        var text = NodeComparer.AsString(actual) ?? string.Empty;
        var like = NodeComparer.AsString(pattern) ?? string.Empty;
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(like).Replace("%", ".*", StringComparison.Ordinal).Replace("_", ".", StringComparison.Ordinal) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    internal static bool TryGetLong(JsonNode? node, out long value)
    {
        value = 0;
        if (node is not JsonValue v)
        {
            return false;
        }

        var text = v.TryGetValue(out string? s) ? s : v.ToJsonString();
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>Compara valores JSON como o equipamento: números (inclusive em texto) por valor; o resto como texto.</summary>
internal sealed class NodeComparer : IComparer<JsonNode?>
{
    public static readonly NodeComparer Instance = new();

    public int Compare(JsonNode? x, JsonNode? y)
    {
        if (x is null || y is null)
        {
            return (x is null ? 0 : 1) - (y is null ? 0 : 1);
        }

        if (TryNumber(x, out var a) && TryNumber(y, out var b))
        {
            return a.CompareTo(b);
        }

        return string.CompareOrdinal(AsString(x), AsString(y));
    }

    internal static string? AsString(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out string? s) => s,
        _ => node.ToJsonString(),
    };

    private static bool TryNumber(JsonNode node, out decimal value)
    {
        value = 0;
        if (node is not JsonValue v)
        {
            return false;
        }

        // Valores vindos do JSON e valores criados em código (long, bool) têm representações diferentes no JsonValue;
        // o texto JSON é o denominador comum.
        if (v.TryGetValue(out string? s))
        {
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        var json = v.ToJsonString();
        if (json is "true" or "false")
        {
            value = json == "true" ? 1 : 0;
            return true;
        }

        return decimal.TryParse(json, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>Erro que o equipamento devolve como HTTP 400 com <c>{"error": ..., "code": 1}</c>.</summary>
public sealed class DeviceException(string message) : Exception(message);
