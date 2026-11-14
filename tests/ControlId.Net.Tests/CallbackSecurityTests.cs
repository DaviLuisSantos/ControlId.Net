using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

public class CallbackSecurityTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static (FakeDeviceHandler Device, AccessClient Client) Create()
    {
        var device = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson("set_configuration.fcgi", "{}");
        return (device, new AccessClient(Options, device.CreateClient()));
    }

    private static JsonDocument Body(FakeDeviceHandler device) => JsonDocument.Parse(device.RequestsTo("set_configuration.fcgi").Single().Body);

    [Fact]
    public async Task Default_section_sends_token_and_certificate()
    {
        var (device, client) = Create();
        using var c = client;

        await client.ConfigureCallbackSecurityAsync("tok", "cert");

        using var body = Body(device);
        var general = body.RootElement.GetProperty("general");
        Assert.Equal("tok", general.GetProperty("curl_bearer_token").GetString());
        Assert.Equal("cert", general.GetProperty("curl_pinned_certificate").GetString());
    }

    [Fact]
    public async Task Custom_section_and_null_certificate_is_omitted()
    {
        var (device, client) = Create();
        using var c = client;

        await client.ConfigureCallbackSecurityAsync("tok", section: "push_server");

        using var body = Body(device);
        var section = body.RootElement.GetProperty("push_server");
        Assert.Equal("tok", section.GetProperty("curl_bearer_token").GetString());
        Assert.False(section.TryGetProperty("curl_pinned_certificate", out _));
        Assert.False(body.RootElement.TryGetProperty("general", out _));
    }

    [Fact]
    public async Task Empty_string_clears_and_null_token_is_omitted()
    {
        var (device, client) = Create();
        using var c = client;

        await client.ConfigureCallbackSecurityAsync(null, "");

        using var body = Body(device);
        var general = body.RootElement.GetProperty("general");
        Assert.False(general.TryGetProperty("curl_bearer_token", out _));
        Assert.Equal("", general.GetProperty("curl_pinned_certificate").GetString());
    }

    [Fact]
    public async Task Both_null_throws()
    {
        var (device, client) = Create();
        using var c = client;

        await Assert.ThrowsAsync<ArgumentException>(() => client.ConfigureCallbackSecurityAsync(null, null));
        Assert.Empty(device.Requests);
    }
}
