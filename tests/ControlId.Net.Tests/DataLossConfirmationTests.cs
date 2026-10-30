using ControlId.Access;

namespace ControlId.Tests;

public class DataLossConfirmationTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    public static TheoryData<string, Func<IAccessClient, DataLossConfirmation, Task>> Operations => new()
    {
        { "reset_to_factory_default.fcgi", (c, confirm) => c.ResetToFactoryDefaultAsync(confirm, keepNetworkSettings: true) },
        { "delete_admins.fcgi", (c, confirm) => c.DeleteAllAdministratorsAsync(confirm) },
        { "user_destroy_image.fcgi", (c, confirm) => c.DeleteAllUserImagesAsync(confirm) },
        { "face_template_destroy.fcgi", (c, confirm) => c.DeleteAllFaceTemplatesAsync(confirm) },
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Without_confirmation_nothing_is_sent(string endpoint, Func<IAccessClient, DataLossConfirmation, Task> operation)
    {
        var device = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson(endpoint, "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => operation(client, default));

        Assert.Contains("DataLossConfirmation.Confirmed", ex.Message);
        Assert.Empty(device.RequestsTo(endpoint));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task With_confirmation_the_request_is_sent(string endpoint, Func<IAccessClient, DataLossConfirmation, Task> operation)
    {
        var device = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson(endpoint, "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await operation(client, DataLossConfirmation.Confirmed);

        Assert.Single(device.RequestsTo(endpoint));
    }

#pragma warning disable CS0618 // as sobrecargas antigas continuam funcionando até saírem
    [Fact]
    public async Task Obsolete_overloads_still_work()
    {
        var device = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson("delete_admins.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.DeleteAllAdministratorsAsync();

        Assert.Single(device.RequestsTo("delete_admins.fcgi"));
    }
#pragma warning restore CS0618
}
