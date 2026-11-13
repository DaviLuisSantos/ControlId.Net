using System.Net.Http.Json;
using System.Text.Json;
using ControlId.Rep;
using ControlId.Rep.Afd;

namespace ControlId.Net.IntegrationTests;

public sealed class RepEndToEndTests
{
    private static async Task PunchAsync(RunningSimulator sim, long cpf)
    {
        using var response = await sim.Control.PostAsJsonAsync("/simulator/punch", new { cpf });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reports_identity_and_counters()
    {
        await using var sim = await RunningSimulator.StartRepAsync(o => o.SeedSampleData = false);
        using var rep = sim.CreateRepClient();

        var about = await rep.GetAboutAsync();
        Assert.Equal("SIM0000001", about.SerialNumber);
        Assert.Equal(104, about.ApiRevision);

        await rep.AddUsersAsync([new RepUser { Cpf = 11144477735, Name = "Ana", Admin = true }, new RepUser { Cpf = 52998224725, Name = "Bruno" }]);

        var info = await rep.GetInfoAsync();
        Assert.Equal(2, info.UserCount);
        Assert.Equal(1, info.AdministratorCount);

        var system = await rep.GetSystemInformationAsync();
        Assert.Equal(2, system.UserCount);
        Assert.True(system.PaperOk);
        Assert.Equal(3, system.LastNsr); // empresa (1) + duas inclusões

        using var health = await sim.Control.GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);
        var state = await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");
        Assert.Equal("Rep", state.GetProperty("kind").GetString());
        Assert.Equal(2, state.GetProperty("users").GetInt32());
    }

    [Fact]
    public async Task Rejects_wrong_password_and_renews_an_expired_session()
    {
        await using var sim = await RunningSimulator.StartRepAsync();

        using var wrong = sim.CreateRepClient(password: "errada");
        await Assert.ThrowsAnyAsync<ControlIdException>(() => wrong.GetAboutAsync());

        using var rep = sim.CreateRepClient();
        Assert.Equal(3, await rep.CountUsersAsync());
        Assert.True(await rep.IsSessionValidAsync());

        await sim.ExpireSessionsAsync();

        Assert.Equal(3, await rep.CountUsersAsync()); // o cliente entra de novo sozinho
        await rep.LogoutAsync();
        Assert.False(await rep.IsSessionValidAsync());
    }

    [Fact]
    public async Task Adds_loads_updates_and_removes_users_by_cpf()
    {
        await using var sim = await RunningSimulator.StartRepAsync(o => o.SeedSampleData = false);
        using var rep = sim.CreateRepClient();

        var users = Enumerable.Range(1, 130).Select(i => new RepUser { Cpf = 10000000000L + i, Name = $"Pessoa {i}", Registration = i.ToString() }).ToList();
        await rep.AddUsersAsync(users);
        Assert.Equal(130, await rep.CountUsersAsync());

        var firstPage = await rep.LoadUsersAsync(limit: 100, offset: 0);
        Assert.Equal(130, firstPage.Count);
        Assert.Equal(100, firstPage.Users.Count);
        Assert.Equal(130, (await rep.GetAllUsersAsync()).Count);

        await rep.UpdateUsersAsync([new RepUserUpdate { Cpf = 10000000001, Name = "Renomeada", Rfid = 77 }]);
        var one = Assert.Single(await rep.GetUsersAsync([10000000001]));
        Assert.Equal("Renomeada", one.Name);
        Assert.Equal(77, one.Rfid);

        await rep.RemoveUsersAsync([10000000001, 10000000002, 99999999999]); // o inexistente é ignorado
        Assert.Equal(128, await rep.CountUsersAsync());
        Assert.Empty(await rep.GetUsersAsync([10000000001]));

        await Assert.ThrowsAnyAsync<ControlIdException>(() => rep.AddUsersAsync([new RepUser { Cpf = 10000000003, Name = "Repetida" }]));
        await Assert.ThrowsAnyAsync<ControlIdException>(() => rep.UpdateUsersAsync([new RepUserUpdate { Cpf = 1, Name = "Ninguém" }]));
    }

    [Fact]
    public async Task Identifies_users_by_pis_in_legacy_mode()
    {
        await using var sim = await RunningSimulator.StartRepAsync(o => o.SeedSampleData = false);
        using var rep = sim.CreateRepClient(RepMode.Legacy);

        await rep.AddUsersAsync([new RepUser { Pis = 12345678901, Name = "Legado" }]);

        var user = Assert.Single(await rep.GetUsersAsync([12345678901]));
        Assert.Equal(12345678901, user.Pis);
        Assert.Null(user.Cpf);
        await rep.RemoveUsersAsync([12345678901]);
        Assert.Equal(0, await rep.CountUsersAsync());
    }

    [Fact]
    public async Task Sets_the_clock_with_time_zone_in_portaria_671_mode()
    {
        await using var sim = await RunningSimulator.StartRepAsync();
        using var rep = sim.CreateRepClient();

        var wanted = new DateTimeOffset(2030, 5, 17, 8, 30, 0, TimeSpan.FromHours(-4));
        await rep.SetDateTimeAsync(wanted);

        var read = await rep.GetDateTimeOffsetAsync();
        Assert.Equal(TimeSpan.FromHours(-4), read.Offset);
        Assert.InRange((read - wanted).TotalSeconds, 0, 10);

        // Sem o modo 671 o fuso não aparece, mas o horário local é o mesmo.
        using var legacy = sim.CreateRepClient(RepMode.Legacy);
        Assert.InRange((await legacy.GetDateTimeAsync() - wanted.DateTime).TotalSeconds, 0, 10);

        var afd = await rep.GetAfdFileAsync();
        var adjustment = Assert.Single(afd.ClockAdjustments);
        Assert.Equal(TimeSpan.FromHours(-4), adjustment.AfterUtcOffset);
        Assert.True(adjustment.IsCrcValid);
    }

    [Fact]
    public async Task Serves_a_valid_afd_with_punches_in_iso_8859_1()
    {
        await using var sim = await RunningSimulator.StartRepAsync(o => o.SeedSampleData = false);
        using var rep = sim.CreateRepClient();

        await rep.AddUsersAsync([new RepUser { Cpf = 11144477735, Name = "José Conceição" }]);
        await PunchAsync(sim, 11144477735);
        await PunchAsync(sim, 11144477735);

        var afd = await rep.GetAfdFileAsync();

        Assert.Empty(afd.Issues);
        Assert.Equal(AfdLayout.Portaria671, afd.Layout);
        Assert.NotNull(afd.Header);
        Assert.NotNull(afd.Trailer);
        Assert.All(afd.Records.Where(r => r.RecordType is >= 1 and <= 5), r => Assert.True(r.IsCrcValid, r.Line));
        Assert.Equal([1L, 2, 3, 4], afd.Records.Where(r => r.RecordType is >= 2 and <= 5).Select(r => r.Nsr).ToArray());

        var employee = Assert.Single(afd.EmployeeRecords);
        Assert.Equal(AfdEmployeeOperation.Include, employee.Operation);
        Assert.Equal("José Conceição", employee.Name);

        Assert.Equal(2, afd.Punches.Count);
        Assert.All(afd.Punches, p => Assert.Equal("11144477735", p.EmployeeId));
        Assert.Equal(4, afd.LastNsr);
        Assert.Equal(2, afd.Trailer!.Punches);
    }

    [Fact]
    public async Task Filters_the_afd_by_nsr_and_date_and_honors_header_top()
    {
        await using var sim = await RunningSimulator.StartRepAsync(o => o.SeedSampleData = false);
        using var rep = sim.CreateRepClient();

        await rep.AddUsersAsync([new RepUser { Cpf = 11144477735, Name = "Ana" }]);
        await PunchAsync(sim, 11144477735);
        await PunchAsync(sim, 11144477735);

        var fromNsr = await rep.GetAfdFileFromNsrAsync(4);
        Assert.Empty(fromNsr.Issues);
        Assert.Equal([4L], fromNsr.Punches.Select(p => p.Nsr).ToArray());

        var future = await rep.GetAfdFileFromDateAsync(DateTime.Today.AddDays(2));
        Assert.Empty(future.Punches);
        Assert.Empty(future.Issues);

        var recent = await rep.GetAfdFileFromDateAsync(DateTime.Today.AddDays(-1));
        Assert.Equal(2, recent.Punches.Count);

        var top = await rep.GetAfdAsync(new RepAfdQuery { HeaderTop = true });
        Assert.Equal(1, AfdParser.ParseLine(top.Split("\r\n")[0]).RecordType);
        var bottom = await rep.GetAfdAsync(new RepAfdQuery());
        Assert.NotEqual(1, AfdParser.ParseLine(bottom.Split("\r\n")[0]).RecordType);

        await using var stream = await rep.OpenAfdStreamAsync(new RepAfdQuery { InitialNsr = 4 });
        Assert.Single(AfdParser.Parse(stream).Punches);
    }

    [Fact]
    public async Task Refuses_a_punch_for_an_unknown_cpf()
    {
        await using var sim = await RunningSimulator.StartRepAsync(o => o.SeedSampleData = false);

        using var response = await sim.Control.PostAsJsonAsync("/simulator/punch", new { cpf = 11144477735L });

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_an_unknown_kind()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunningSimulator.StartAsync(o => o.Kind = "Catraca"));

        Assert.Contains("Catraca", error.Message);
    }
}
