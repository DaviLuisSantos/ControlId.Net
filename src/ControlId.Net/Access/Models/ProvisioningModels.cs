using System;

namespace ControlId.Access;

/// <summary>
/// Equipamento de destino de <c>request_redirect.fcgi</c>: por ID na tabela local de dispositivos ou por endereço com
/// credenciais. A senha do modo por endereço nunca aparece em <see cref="ToString"/>.
/// </summary>
public sealed class RedirectTarget
{
    private readonly string? _password;

    private RedirectTarget(int? deviceId, string? address, string? login, string? password)
    {
        DeviceId = deviceId;
        Address = address;
        Login = login;
        _password = password;
    }

    /// <summary>ID do equipamento de destino (modo 1); <c>null</c> no modo por endereço.</summary>
    public int? DeviceId { get; }

    /// <summary>Endereço IP do equipamento de destino (modo 2); <c>null</c> no modo por ID.</summary>
    public string? Address { get; }

    /// <summary>Login do equipamento de destino (modo 2); <c>null</c> no modo por ID.</summary>
    public string? Login { get; }

    /// <summary>Modo 1: o destino é um equipamento já cadastrado na tabela local de dispositivos (não pode ser o próprio).</summary>
    /// <param name="deviceId">ID do equipamento de destino.</param>
    public static RedirectTarget ByDeviceId(int deviceId) => new RedirectTarget(deviceId, null, null, null);

    /// <summary>Modo 2: o destino é um IP com credenciais (o endereço não pode ser o do próprio equipamento).</summary>
    /// <param name="address">Endereço IP do destino.</param>
    /// <param name="login">Login do destino.</param>
    /// <param name="password">Senha do destino; vai na query string da requisição.</param>
    /// <exception cref="ArgumentException">Algum argumento é nulo ou vazio.</exception>
    public static RedirectTarget ByAddress(string address, string login, string password)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Valor obrigatório.", nameof(address));
        }

        if (string.IsNullOrWhiteSpace(login))
        {
            throw new ArgumentException("Valor obrigatório.", nameof(login));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Valor obrigatório.", nameof(password));
        }

        return new RedirectTarget(null, address, login, password);
    }

    internal string BuildQuery()
    {
        if (DeviceId is { } id)
        {
            return "device_id=" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return "address=" + Uri.EscapeDataString(Address!)
            + "&login=" + Uri.EscapeDataString(Login!)
            + "&password=" + Uri.EscapeDataString(_password!);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        DeviceId is { } id
            ? $"RedirectTarget {{ DeviceId = {id} }}"
            : $"RedirectTarget {{ Address = {Address}, Login = {Login}, Password = *** }}";
}
