using System;
using System.Collections.Generic;

namespace ControlId.Access.Push;

/// <summary>Situação de um envio no <see cref="IPushCommandStore"/>.</summary>
public enum PushEnvelopeStatus
{
    /// <summary>Aguardando o equipamento consultar <c>GET /push</c>.</summary>
    Pending,

    /// <summary>Entregue ao equipamento; aguardando o <c>POST /result</c>.</summary>
    InFlight,
}

/// <summary>
/// Um envio (comando avulso ou lote) guardado no <see cref="IPushCommandStore"/>. O corpo já está serializado:
/// alterar o <see cref="PushCommand"/> depois de enfileirá-lo não muda o que o equipamento recebe.
/// </summary>
public sealed class PushEnvelope
{
    /// <summary>Cria um envio pendente.</summary>
    public PushEnvelope(Guid id, long deviceId, bool batch, IReadOnlyList<string> endpoints, string payload, DateTimeOffset createdAt)
    {
        Id = id;
        DeviceId = deviceId;
        Batch = batch;
        Endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        CreatedAt = createdAt;
    }

    /// <summary>Identificador do envio.</summary>
    public Guid Id { get; }

    /// <summary>Equipamento de destino.</summary>
    public long DeviceId { get; }

    /// <summary><c>true</c> se o envio é um lote (<c>transactions</c>).</summary>
    public bool Batch { get; }

    /// <summary>Endpoints dos comandos, na ordem do envio (um só quando não é lote).</summary>
    public IReadOnlyList<string> Endpoints { get; }

    /// <summary>JSON exato entregue na resposta de <c>GET /push</c>.</summary>
    public string Payload { get; }

    /// <summary>Situação atual; o store a atualiza em <see cref="IPushCommandStore.TakeNextAsync"/>.</summary>
    public PushEnvelopeStatus Status { get; set; }

    /// <summary><c>uuid</c> informado pelo equipamento na consulta que levou o envio, quando houve.</summary>
    public string? Uuid { get; set; }

    /// <summary>Quando o envio foi criado.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Quando o envio foi entregue ao equipamento, ou <c>null</c> se ainda não foi.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Descrição curta para mensagens de erro.</summary>
    internal string Description => Batch ? $"lote de {Endpoints.Count} comandos" : (Endpoints.Count > 0 ? Endpoints[0] : "comando");
}
