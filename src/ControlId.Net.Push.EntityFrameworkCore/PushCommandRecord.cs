using ControlId.Access.Push;

namespace ControlId.Push.EntityFrameworkCore;

/// <summary>Linha da tabela de envios de Push: um <see cref="PushEnvelope"/> persistido.</summary>
internal sealed class PushCommandRecord
{
    public Guid Id { get; set; }

    public long DeviceId { get; set; }

    public bool Batch { get; set; }

    /// <summary>Endpoints em JSON (array de strings).</summary>
    public string Endpoints { get; set; } = "[]";

    public string Payload { get; set; } = string.Empty;

    public PushEnvelopeStatus Status { get; set; }

    public string? Uuid { get; set; }

    /// <summary>Criação em ticks UTC (ordenável em qualquer banco, ao contrário de <see cref="DateTimeOffset"/>).</summary>
    public long CreatedAtTicks { get; set; }

    public long? DeliveredAtTicks { get; set; }
}
