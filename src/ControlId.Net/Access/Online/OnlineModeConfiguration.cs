using System;

namespace ControlId.Access.Online;

/// <summary>Modo online do equipamento.</summary>
public enum OnlineMode
{
    /// <summary>Pro: o equipamento identifica a pessoa; o servidor só autoriza (<c>new_user_identified</c>).</summary>
    Pro,

    /// <summary>Enterprise: o servidor identifica e autoriza a partir da credencial crua (<c>new_card</c>, biometria...).</summary>
    Enterprise,
}

/// <summary>Parâmetros de <see cref="IAccessClient.ConfigureOnlineModeAsync"/>.</summary>
public sealed class OnlineModeConfiguration
{
    /// <summary>
    /// Endereço do servidor; as rotas são concatenadas a ele. Ex.: <c>http://meuservidor:5000/api</c> faz o
    /// equipamento chamar <c>http://meuservidor:5000/api/new_user_identified.fcgi</c>.
    /// </summary>
    public string ServerAddress { get; set; } = string.Empty;

    /// <summary>Nome do registro do servidor na tabela <c>devices</c> do equipamento.</summary>
    public string ServerName { get; set; } = "ControlId.Net";

    /// <summary>Pro (padrão) ou Enterprise.</summary>
    public OnlineMode Mode { get; set; } = OnlineMode.Pro;

    /// <summary>Equipamento é uma IHM (exige <c>ihm_enterprise_mode</c> no Enterprise).</summary>
    public bool IsIhm { get; set; }

    /// <summary>Enterprise com biometria: <c>true</c> envia template (<c>new_biometric_template</c>), <c>false</c> a imagem.</summary>
    public bool? ExtractTemplate { get; set; }

    /// <summary>Entra em contingência (decide localmente) quando o servidor não responde.</summary>
    public bool? ContingencyEnabled { get; set; }

    /// <summary>Tentativas sem resposta antes da contingência (padrão do equipamento: 3).</summary>
    public int? MaxRequestAttempts { get; set; }

    /// <summary>Quanto o equipamento espera cada resposta do servidor (máximo permitido: 5 s).</summary>
    public TimeSpan? RequestTimeout { get; set; }

    /// <summary>Intervalo das tentativas de voltar ao modo online durante a contingência (<c>device_is_alive</c>).</summary>
    public TimeSpan? AliveInterval { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerAddress))
        {
            throw new ArgumentException("Informe o endereço do servidor (ServerAddress).", nameof(ServerAddress));
        }

        if (RequestTimeout is { } timeout && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(5)))
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout), timeout, "O equipamento aceita de 1 ms a 5 s.");
        }

        if (MaxRequestAttempts is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRequestAttempts), MaxRequestAttempts, "Use ao menos 1 tentativa.");
        }
    }
}
