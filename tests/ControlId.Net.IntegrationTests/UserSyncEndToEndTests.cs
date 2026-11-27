using System.Net.Http.Json;
using System.Text.Json;
using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>Sincronização de pessoas (#54) contra o simulador: idempotência provada pelo contador de requisições.</summary>
public sealed class UserSyncEndToEndTests
{
    // Comandos que escrevem no equipamento. O simulador não tem user_hash_password, então estes testes não usam senha.
    private static readonly string[] WriteCommands =
    [
        "create_objects", "modify_objects", "destroy_objects", "create_or_modify_objects",
        "user_set_image", "user_set_image_list", "user_destroy_image", "user_hash_password",
    ];

    // JPEG mínimo com cabeçalho SOF0 válido (16x16), que a validação de fotos da biblioteca aceita.
    private static readonly byte[] Photo =
        [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00, 0x10, 0x03, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0, 0xFF, 0xD9];

    private static byte[] OtherPhoto() => [.. Photo, 0x01, 0x02, 0x03];

    private static async Task<Dictionary<string, long>> RequestsAsync(RunningSimulator sim)
    {
        var state = await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");
        return state.GetProperty("requests").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt64());
    }

    private static async Task<long> TableCountAsync(RunningSimulator sim, string table)
    {
        var state = await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");
        return state.GetProperty("tables").GetProperty(table).GetInt64();
    }

    private static long Writes(Dictionary<string, long> requests) =>
        WriteCommands.Sum(command => requests.TryGetValue(command, out var count) ? count : 0);

    private static DesiredUser[] People() =>
    [
        new DesiredUser
        {
            Id = 1, Name = "Maria", Registration = "1020", Cards = [111, 112], GroupIds = [1, 2], Photo = Photo,
            BeginTime = new DateTime(2026, 1, 2, 8, 0, 0),
        },
        new DesiredUser { Id = 2, Name = "João", Cards = [221], GroupIds = [1] },
    ];

    [Fact]
    public async Task Second_run_with_the_same_state_does_not_write_anything()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        var first = await device.SyncUsersAsync(People());

        Assert.All(first, r => Assert.True(r.Success));
        Assert.All(first, r => Assert.True(r.Created));
        Assert.All(first, r => Assert.Equal(UserSyncStatus.Created, r.Status));
        Assert.Equal(new[] { "João", "Maria" }, (await device.GetUsersAsync()).Select(u => u.Name).Order());
        Assert.Equal(new long[] { 111, 112 }, (await device.GetCardsAsync(1)).Select(c => c.Value).Order());
        Assert.Equal(Photo, await device.GetUserImageAsync(1));
        var afterFirst = await RequestsAsync(sim);
        Assert.True(Writes(afterFirst) > 0);

        var second = await device.SyncUsersAsync(People());

        var afterSecond = await RequestsAsync(sim);
        Assert.Equal(Writes(afterFirst), Writes(afterSecond));
        Assert.All(second, r => Assert.True(r.Success));
        Assert.All(second, r => Assert.False(r.Changed));
        Assert.All(second, r => Assert.Equal(UserSyncStatus.Unchanged, r.Status));
        Assert.True(afterSecond["load_objects"] > afterFirst["load_objects"], "a segunda execução só lê");
    }

    [Fact]
    public async Task A_change_writes_only_the_difference()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();
        await device.SyncUsersAsync(People());
        var before = await RequestsAsync(sim);

        var people = People();
        people[0].Cards = [111, 113];
        people[0].Photo = OtherPhoto();
        var results = await device.SyncUsersAsync(people, new UserSyncOptions { RemoveUnlisted = true });

        var after = await RequestsAsync(sim);
        Assert.Equal(new long[] { 112 }, results[0].CardsRemoved);
        Assert.Equal(new long[] { 113 }, results[0].CardsAdded);
        Assert.True(results[0].PhotoUpdated);
        Assert.Equal(UserSyncStatus.Changed, results[0].Status);
        Assert.False(results[1].Changed);
        // um destroy_objects (cartão 112), um create_objects (cartão 113) e uma foto.
        Assert.Equal(Writes(before) + 3, Writes(after));
        Assert.Equal(new long[] { 111, 113 }, (await device.GetCardsAsync(1)).Select(c => c.Value).Order());
        Assert.Equal(OtherPhoto(), await device.GetUserImageAsync(1));
    }

    [Fact]
    public async Task By_default_surplus_cards_and_groups_are_not_removed()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();
        await device.SyncUsersAsync(People());
        var groupLinks = await TableCountAsync(sim, "user_groups");

        var people = People();
        people[0].Cards = [111];
        people[0].GroupIds = [1];
        var before = await RequestsAsync(sim);
        var results = await device.SyncUsersAsync(people);
        var after = await RequestsAsync(sim);

        Assert.Equal(Writes(before), Writes(after));
        Assert.Equal(new long[] { 111, 112 }, (await device.GetCardsAsync(1)).Select(c => c.Value).Order());
        Assert.Empty(results[0].CardsRemoved);
        Assert.Empty(results[0].GroupsRemoved);
        Assert.False(results[0].Changed);
        Assert.Equal(UserSyncStatus.Unchanged, results[0].Status);
        Assert.Equal(groupLinks, await TableCountAsync(sim, "user_groups"));
    }

    [Fact]
    public async Task A_card_that_belongs_to_someone_else_shows_up_only_in_that_persons_report()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();
        var ownerId = await device.CreateUserAsync(new AccessUser { Name = "Dono do cartão" });
        await device.CreateCardAsync(new AccessCard { UserId = ownerId, Value = 777 });

        var results = await device.SyncUsersAsync(
        [
            new DesiredUser { Id = 50, Name = "Ana", Cards = [777, 778] },
            new DesiredUser { Id = 51, Name = "Bia", Cards = [881] },
        ]);

        Assert.False(results[0].Success);
        Assert.Equal("card:777", Assert.Single(results[0].Errors).Item);
        Assert.Equal(UserSyncStatus.Failed, results[0].Status);
        Assert.Equal(new long[] { 778 }, results[0].CardsAdded);
        Assert.True(results[1].Success);
        Assert.Equal(new long[] { 881 }, results[1].CardsAdded);
        Assert.Equal(new long[] { 777 }, (await device.GetCardsAsync(ownerId)).Select(c => c.Value));
        Assert.Equal(new long[] { 778 }, (await device.GetCardsAsync(50)).Select(c => c.Value));
    }

    [Fact]
    public async Task RemoveMissing_removes_the_extra_people_only_with_confirmation()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();
        await device.SyncUsersAsync(People());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            device.SyncUsersAsync([People()[0]], new UserSyncOptions { RemoveMissing = true }));
        Assert.Equal(2, (await device.GetUsersAsync()).Count);

        var results = await device.SyncUsersAsync(
            [People()[0]], new UserSyncOptions { RemoveMissing = true, DataLossConfirmation = DataLossConfirmation.Confirmed });

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Changed);
        Assert.Equal(UserSyncStatus.Unchanged, results[0].Status);
        Assert.True(results[1].Removed);
        Assert.Equal(UserSyncStatus.Removed, results[1].Status);
        Assert.Equal(2, results[1].UserId);
        Assert.Equal("Maria", Assert.Single(await device.GetUsersAsync()).Name);
    }
}
