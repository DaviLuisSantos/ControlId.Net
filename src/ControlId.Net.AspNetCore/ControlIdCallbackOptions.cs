using System;

namespace ControlId.AspNetCore;

/// <summary>
/// Limites dos callbacks (Push, Monitor, Online) que o equipamento chama. Configure com
/// <c>services.Configure&lt;ControlIdCallbackOptions&gt;(o =&gt; ...)</c>; sem configuração valem os padrões.
/// </summary>
public sealed class ControlIdCallbackOptions
{
    /// <summary>Padrão de <see cref="MaxRequestBodySize"/>: 4 MiB.</summary>
    public const long DefaultMaxRequestBodySize = 4 * 1024 * 1024;

    private long _maxRequestBodySize = DefaultMaxRequestBodySize;

    /// <summary>
    /// Maior corpo aceito em um callback, em bytes (padrão 4 MiB, suficiente para fotos e templates). Acima disso a resposta
    /// é 413 e o corpo não é lido até o fim: o <c>Content-Length</c> é conferido antes, o limite do servidor
    /// (<c>IHttpMaxRequestBodySizeFeature</c>) é ajustado quando possível e a leitura para no primeiro byte excedente.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Valor menor ou igual a zero.</exception>
    public long MaxRequestBodySize
    {
        get => _maxRequestBodySize;
        set => _maxRequestBodySize = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "O limite do corpo dos callbacks deve ser maior que zero.");
    }

    /// <summary>
    /// Lista de equipamentos aceitos, a partir do <c>deviceId</c> do callback (query ou corpo). Quando devolve
    /// <see langword="false"/>, a resposta é 403 e nada chega ao <c>PushServer</c>, ao <c>DeviceContactTracker</c> nem aos
    /// handlers. <see langword="null"/> (padrão) aceita qualquer equipamento. Callbacks sem <c>deviceId</c> (ex.: uma
    /// notificação sem <c>device_id</c>) não passam por esta função. Chamada a cada callback: deve ser rápida e thread-safe.
    /// </summary>
    public Func<long, bool>? IsKnownDevice { get; set; }
}
