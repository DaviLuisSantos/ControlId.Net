using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>object_metadata.fcgi</c>: esquema das tabelas de objetos (campos, restrições e joins), por nome de tabela.
    /// </summary>
    Task<IReadOnlyDictionary<string, ObjectMetadata>> GetObjectMetadataAsync(ObjectMetadataFilter filter = ObjectMetadataFilter.All, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>object_add.fcgi</c>: cria uma tabela customizada com as colunas iniciais (ao menos uma) e devolve o ID da tabela.
    /// <see cref="CustomFieldDefinition.DefaultValue"/> não vale aqui.
    /// </summary>
    Task<long> CreateCustomTableAsync(string table, string label, IEnumerable<CustomFieldDefinition> fields, long? id = null, CancellationToken cancellationToken = default);

    /// <summary><c>object_remove.fcgi</c>: remove tabelas customizadas pelos IDs. Apaga os dados das tabelas sem volta.</summary>
    Task DeleteCustomTablesAsync(DataLossConfirmation confirmation, IEnumerable<long> tableIds, CancellationToken cancellationToken = default);

    /// <summary><c>object_add_field.fcgi</c>: adiciona uma coluna a uma tabela customizada e devolve o ID da coluna.</summary>
    Task<long> AddCustomFieldAsync(string table, CustomFieldDefinition field, CancellationToken cancellationToken = default);

    /// <summary><c>object_update_field.fcgi</c>: atualiza a definição de uma coluna de tabela customizada.</summary>
    Task UpdateCustomFieldAsync(long fieldId, CustomFieldDefinition field, CancellationToken cancellationToken = default);

    /// <summary><c>object_remove_fields.fcgi</c>: remove colunas de tabelas customizadas pelos IDs e devolve os IDs removidos. Apaga os dados das colunas sem volta.</summary>
    Task<IReadOnlyList<long>> RemoveCustomFieldsAsync(DataLossConfirmation confirmation, IEnumerable<long> fieldIds, CancellationToken cancellationToken = default);

    /// <summary><c>export_object.fcgi</c>: exporta uma tabela como CSV (bytes); sem <paramref name="columns"/>, todas as colunas.</summary>
    Task<byte[]> ExportObjectCsvAsync(string @object, IEnumerable<string>? columns = null, CancellationToken cancellationToken = default);

    /// <summary><c>export_custom_tables_metadata.fcgi</c> (GET): exporta só as definições das tabelas customizadas, como CSV (bytes).</summary>
    Task<byte[]> ExportCustomTablesMetadataCsvAsync(CancellationToken cancellationToken = default);
}
