using ControlId.Access;
using ControlId.Access.Configuration;

namespace ControlId.Net.IntegrationTests;

public sealed class AccessClientEndToEndTests
{
    [Fact]
    public async Task Creates_reads_and_deletes_a_user_with_cards()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        var id = await device.CreateUserAsync(new AccessUser { Name = "Maria", Registration = "1020" });
        await device.CreateCardAsync(new AccessCard { UserId = id, Value = 123456789 });

        var user = Assert.Single(await device.GetUsersAsync());
        Assert.Equal(id, user.Id);
        Assert.Equal("Maria", user.Name);
        Assert.Equal(123456789, Assert.Single(await device.GetCardsAsync(id)).Value);

        Assert.True(await device.DeleteUserAsync(id));
        Assert.Empty(await device.GetUsersAsync());
    }

    [Fact]
    public async Task Enumerates_a_large_user_base_page_by_page()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        var users = Enumerable.Range(1, 1234).Select(i => new AccessUser { Name = $"Pessoa {i}", Registration = i.ToString() });
        await device.CreateObjectsAsync("users", users);

        var names = new List<string?>();
        await foreach (var user in device.EnumerateUsersAsync(pageSize: 500))
        {
            names.Add(user.Name);
        }

        Assert.Equal(1234, names.Count);
        Assert.Equal("Pessoa 1", names[0]);
        Assert.Equal("Pessoa 1234", names[^1]);
    }

    [Fact]
    public async Task Deletes_in_batches_until_nothing_is_left()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        var id = await device.CreateUserAsync(new AccessUser { Name = "Com muitos cartões" });
        await device.CreateObjectsAsync("cards", Enumerable.Range(1, 450).Select(i => new AccessCard { UserId = id, Value = i }));

        var removed = await device.DestroyObjectsAsync("cards", Where.Equal("cards", "user_id", id));

        Assert.Equal(450, removed);
        Assert.Empty(await device.GetCardsAsync(id));
    }

    [Fact]
    public async Task Logs_in_again_when_the_session_expires()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        Assert.NotEmpty(await device.GetUsersAsync());
        await sim.ExpireSessionsAsync();   // como um reinício do equipamento

        Assert.NotEmpty(await device.GetUsersAsync());
    }

    [Fact]
    public async Task Rejects_wrong_credentials()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient(password: "errada");

        await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => device.GetUsersAsync());
    }

    [Fact]
    public async Task Uses_the_new_password_after_changing_the_login()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.ChangeLoginAsync("operador", "nova-senha");
        await sim.ExpireSessionsAsync();

        Assert.NotEmpty(await device.GetUsersAsync());
        using var old = sim.CreateClient();
        await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => old.GetUsersAsync());
    }

    [Fact]
    public async Task Reads_a_typed_section_even_with_parameters_the_device_does_not_have()
    {
        // O simulador recusa a leitura inteira por um parâmetro inexistente, como o iDFace 6.9.12 (ADR 0007).
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.SetConfigurationSectionsAsync(new ConfigurationSection[] { new GeneralSettings { BeepEnabled = false } });
        var general = await device.GetConfigurationSectionAsync<GeneralSettings>();

        Assert.False(general.BeepEnabled);
        Assert.Equal("pt_BR", general.Language);
    }

    [Fact]
    public async Task Stores_and_returns_the_user_photo()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9];

        var result = await device.SetUserImageAsync(1, jpeg);

        Assert.True(result.Success);
        Assert.Equal(jpeg, await device.GetUserImageAsync(1));
    }

    [Fact]
    public async Task Opens_the_door()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.OpenDoorAsync(1);

        var doors = await device.GetDoorStatesAsync();
        Assert.True(Assert.Single(doors.Doors).Open);
    }

    [Fact]
    public async Task Collects_access_logs_incrementally()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        var first = await sim.IdentifyAsync(userId: 1);
        await sim.IdentifyAsync(cardValue: 4194301002);   // cartão da Bruno, dos dados de exemplo
        await sim.IdentifyAsync(userId: 999);             // não cadastrado

        var logs = await device.GetAccessLogsAsync(afterId: first);

        Assert.Collection(
            logs,
            l => Assert.Equal((AccessEventType.AccessGranted, 2L), (l.EventType, l.UserId!.Value)),
            l => Assert.Equal(AccessEventType.NotIdentified, l.EventType));
    }

    [Fact]
    public async Task Reports_the_device_error_for_an_unknown_command()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        var error = await Assert.ThrowsAsync<ControlIdException>(() => device.SendRawAsync("nao_existe"));

        Assert.Contains("Invalid command", error.Message + error.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_system_information()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.Serial = "TESTE123");
        using var device = sim.CreateClient();

        var info = await device.GetSystemInformationAsync();

        Assert.Equal("TESTE123", info.Serial);
    }
}
