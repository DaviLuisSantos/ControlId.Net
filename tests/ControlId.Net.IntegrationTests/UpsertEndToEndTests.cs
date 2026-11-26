using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>Upsert num firmware sem <c>create_or_modify_objects</c>: a biblioteca cai para modify + create (ADR 0027).</summary>
public sealed class UpsertEndToEndTests
{
    [Fact]
    public async Task Upsert_falls_back_to_modify_and_create_when_the_command_is_missing()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("create_or_modify_objects"));
        using var device = sim.CreateClient();

        var changes = await device.CreateOrModifyObjectsAsync("users", new[]
        {
            new AccessUser { Id = 1, Name = "Ana Atualizada" },
            new AccessUser { Id = 100, Name = "Pessoa Nova", Registration = "9100" },
        });

        Assert.Equal(2, changes);
        var users = await device.GetUsersAsync();
        Assert.Equal("Ana Atualizada", users.Single(u => u.Id == 1).Name);
        Assert.Equal("Pessoa Nova", users.Single(u => u.Id == 100).Name);
        Assert.Equal("Bruno Lima", users.Single(u => u.Id == 2).Name);
    }

    [Fact]
    public async Task Missing_command_is_remembered_and_not_asked_again()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("create_or_modify_objects"));
        using var device = sim.CreateClient();

        await device.CreateOrModifyObjectsAsync("users", new[] { new AccessUser { Id = 1, Name = "Primeira" } });
        await device.CreateOrModifyObjectsAsync("users", new[] { new AccessUser { Id = 2, Name = "Segunda" } });

        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));
        Assert.Equal("Segunda", (await device.GetUsersAsync()).Single(u => u.Id == 2).Name);
    }

    [Fact]
    public async Task Upsert_uses_the_native_command_when_the_firmware_has_it()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        var changes = await device.CreateOrModifyObjectsAsync("users", new[]
        {
            new AccessUser { Id = 1, Name = "Ana Atualizada" },
            new AccessUser { Id = 101, Name = "Outra Nova" },
        });

        Assert.Equal(2, changes);
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));
        Assert.Equal("Outra Nova", (await device.GetUsersAsync()).Single(u => u.Id == 101).Name);
    }
}
