using System;
using System.Collections.Generic;

namespace ControlId.Access.Push;

/// <summary>Resultado de um envio entregue a um <see cref="IPushResultHandler"/>.</summary>
public sealed class PushCompletion
{
    /// <summary>Cria o resultado.</summary>
    public PushCompletion(Guid commandId, long deviceId, IReadOnlyList<PushResult> results, string? error)
    {
        CommandId = commandId;
        DeviceId = deviceId;
        Results = results ?? throw new ArgumentNullException(nameof(results));
        Error = error;
    }

    /// <summary>Identificador devolvido por <see cref="PushServer.EnqueueAsync"/> (ou <see cref="PushEnvelope.Id"/>).</summary>
    public Guid CommandId { get; }

    /// <summary>Equipamento que respondeu.</summary>
    public long DeviceId { get; }

    /// <summary>Um resultado por comando, na ordem do envio. Vazio quando <see cref="Error"/> não é nulo.</summary>
    public IReadOnlyList<PushResult> Results { get; }

    /// <summary>Preenchido quando o corpo de <c>POST /result</c> não pôde ser lido; senão <c>null</c> (erros do equipamento ficam em <see cref="PushResult.Error"/>).</summary>
    public string? Error { get; }
}
