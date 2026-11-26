using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, ObjectMetadata>> GetObjectMetadataAsync(
        ObjectMetadataFilter filter = ObjectMetadataFilter.All, CancellationToken cancellationToken = default)
    {
        object? body = filter == ObjectMetadataFilter.All
            ? null
            : new Dictionary<string, object> { ["filter"] = filter == ObjectMetadataFilter.Custom ? "custom" : "default" };
        var response = await _transport.PostAsync<Dictionary<string, ObjectMetadata>>("object_metadata.fcgi", body, cancellationToken).ConfigureAwait(false);
        return response ?? new Dictionary<string, ObjectMetadata>();
    }

    /// <inheritdoc/>
    public async Task<long> CreateCustomTableAsync(
        string table, string label, IEnumerable<CustomFieldDefinition> fields, long? id = null, CancellationToken cancellationToken = default)
    {
        var columns = (fields ?? throw new ArgumentNullException(nameof(fields))).ToList();
        if (columns.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um campo.", nameof(fields));
        }

        var body = new Dictionary<string, object>
        {
            ["object"] = Required(table, nameof(table)),
            ["name"] = Required(label, nameof(label)),
        };
        if (id is not null)
        {
            body["id"] = id.Value;
        }

        body["fields"] = columns.Select(f => CustomFieldBody(f, creating: true, allowDefaultValue: false, fieldId: null)).ToList();
        var response = await _transport.PostAsync<IdsResponse>("object_add.fcgi", body, cancellationToken).ConfigureAwait(false);
        return FirstId(response, "object_add.fcgi");
    }

    /// <inheritdoc/>
    public Task DeleteCustomTablesAsync(DataLossConfirmation confirmation, IEnumerable<long> tableIds, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, "Remover tabelas customizadas");
        return _transport.PostRawAsync("object_remove.fcgi", new Dictionary<string, object> { ["ids"] = IdList(tableIds, nameof(tableIds)) }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<long> AddCustomFieldAsync(string table, CustomFieldDefinition field, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["object"] = Required(table, nameof(table)) };
        foreach (var pair in CustomFieldBody(field, creating: true, allowDefaultValue: true, fieldId: null))
        {
            body[pair.Key] = pair.Value;
        }

        var response = await _transport.PostAsync<IdsResponse>("object_add_field.fcgi", body, cancellationToken).ConfigureAwait(false);
        return FirstId(response, "object_add_field.fcgi");
    }

    /// <inheritdoc/>
    public Task UpdateCustomFieldAsync(long fieldId, CustomFieldDefinition field, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "object_update_field.fcgi",
            CustomFieldBody(field, creating: false, allowDefaultValue: true, fieldId: fieldId),
            cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<long>> RemoveCustomFieldsAsync(DataLossConfirmation confirmation, IEnumerable<long> fieldIds, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, "Remover campos de tabelas customizadas");
        var response = await _transport.PostAsync<IdsResponse>(
            "object_remove_fields.fcgi", new Dictionary<string, object> { ["ids"] = IdList(fieldIds, nameof(fieldIds)) }, cancellationToken).ConfigureAwait(false);
        return response?.Ids ?? new List<long>();
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportObjectCsvAsync(string @object, IEnumerable<string>? columns = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["object"] = Required(@object, nameof(@object)) };
        var list = columns?.ToList();
        if (list is { Count: > 0 })
        {
            body["columns"] = list;
        }

        var (content, _) = await _transport.PostForBytesAsync("export_object.fcgi", body, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public Task<byte[]> ExportCustomTablesMetadataCsvAsync(CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("export_custom_tables_metadata.fcgi", cancellationToken);

    private static long FirstId(IdsResponse? response, string endpoint) =>
        response?.Ids is { Count: > 0 } ids
            ? ids[0]
            : throw new ControlIdException($"{endpoint} não devolveu o ID criado.");

    private static List<long> IdList(IEnumerable<long> ids, string paramName)
    {
        var list = (ids ?? throw new ArgumentNullException(paramName)).ToList();
        return list.Count == 0 ? throw new ArgumentException("Informe ao menos um ID.", paramName) : list;
    }

    private static string CustomFieldTypeName(CustomFieldType type) => type switch
    {
        CustomFieldType.Integer => "INTEGER",
        CustomFieldType.BigInt => "BIGINT",
        CustomFieldType.Text => "TEXT",
        CustomFieldType.Blob => "BLOB",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de campo desconhecido."),
    };

    /// <summary>
    /// Corpo de uma coluna. <paramref name="creating"/>: <c>object_add</c>/<c>object_add_field</c> (nome obrigatório,
    /// <c>default_value</c> obrigatório com NOT_NULL); senão, <c>object_update_field</c> (<c>default_value</c> obrigatório com unique).
    /// </summary>
    private static Dictionary<string, object> CustomFieldBody(CustomFieldDefinition field, bool creating, bool allowDefaultValue, long? fieldId)
    {
        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        var body = new Dictionary<string, object>();
        if (fieldId is not null)
        {
            body["id"] = fieldId.Value;
        }

        body["column_name"] = Required(field.ColumnName, nameof(field));
        if (creating)
        {
            body["name"] = Required(field.Name ?? string.Empty, nameof(field));
        }
        else if (!string.IsNullOrWhiteSpace(field.Name))
        {
            body["name"] = field.Name!;
        }

        body["type"] = CustomFieldTypeName(field.Type);
        body["constraint"] = field.NotNull ? "NOT_NULL" : "NONE";
        if (!creating || field.Unique)
        {
            body["unique"] = field.Unique;
        }

        if (allowDefaultValue)
        {
            if (field.DefaultValue is null && (creating ? field.NotNull : field.Unique))
            {
                throw new ArgumentException(
                    creating ? "Informe DefaultValue: o equipamento exige valor padrão para campo NOT_NULL." : "Informe DefaultValue ao definir o campo como unique.",
                    nameof(field));
            }

            if (field.DefaultValue is not null)
            {
                body["default_value"] = field.DefaultValue;
            }
        }

        if (field.ForeignKey is not null)
        {
            body["foreign_key"] = field.ForeignKey;
        }

        return body;
    }
}
