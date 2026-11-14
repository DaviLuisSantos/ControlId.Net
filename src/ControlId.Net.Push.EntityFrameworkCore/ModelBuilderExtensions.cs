using Microsoft.EntityFrameworkCore;

namespace ControlId.Push.EntityFrameworkCore;

/// <summary>Configuração do modelo do EF Core para a fila de Push durável.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Acrescenta ao modelo a tabela dos envios de Push (chame em <c>OnModelCreating</c>). A estrutura da tabela
    /// entra nas migrações do seu <see cref="DbContext"/> como qualquer outra entidade.
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo.</param>
    /// <param name="table">Nome da tabela.</param>
    /// <param name="schema">Schema da tabela, ou <c>null</c> para o padrão do banco.</param>
    public static ModelBuilder ApplyControlIdPush(this ModelBuilder modelBuilder, string table = "controlid_push_commands", string? schema = null)
    {
        if (modelBuilder is null)
        {
            throw new ArgumentNullException(nameof(modelBuilder));
        }

        if (string.IsNullOrWhiteSpace(table))
        {
            throw new ArgumentException("O nome da tabela é obrigatório.", nameof(table));
        }

        modelBuilder.Entity<PushCommandRecord>(entity =>
        {
            entity.ToTable(table, schema);
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedNever();
            entity.Property(record => record.Endpoints).IsRequired();
            entity.Property(record => record.Payload).IsRequired();
            entity.HasIndex(record => new { record.DeviceId, record.Status, record.CreatedAtTicks });
        });

        return modelBuilder;
    }
}
