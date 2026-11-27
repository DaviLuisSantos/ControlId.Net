using System.Net.Http;
using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>
/// Falha no meio do fallback do upsert (#66): o simulador não tem <c>create_or_modify_objects</c> e um handler derruba a
/// N-ésima requisição de <c>modify_objects</c> ou de <c>create_objects</c>. O que já foi gravado fica no equipamento e
/// repetir com os mesmos valores termina o que faltou, sem duplicar registros.
/// </summary>
public sealed class UpsertPartialFailureEndToEndTests
{
    [Fact]
    public async Task CreateOrModifyObjectsAsync_fails_midway_through_the_modifies_and_repeating_converges()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("create_or_modify_objects"));
        using var http = new HttpClient(new FailNthCommandHandler("modify_objects", failAt: 3)) { Timeout = TimeSpan.FromSeconds(10) };
        using var device = new AccessClient(Options(sim), http);
        var values = new[]
        {
            new AccessUser { Id = 1, Name = "Ana Atualizada" },
            new AccessUser { Id = 2, Name = "Bruno Atualizado" },
            new AccessUser { Id = 100, Name = "Pessoa Nova", Registration = "9100" },
            new AccessUser { Id = 101, Name = "Outra Nova", Registration = "9101" },
        };

        var failure = await Assert.ThrowsAsync<ControlIdOutcomeUnknownException>(
            () => device.CreateOrModifyObjectsAsync("users", values));

        // O transporte considera que a escrita pode ter chegado ao equipamento: a conexão caiu depois do envio.
        Assert.Equal("modify_objects", failure.Command);
        Assert.False(failure.TimedOut);

        // Os dois modifies anteriores foram gravados; o terceiro (o primeiro item novo) e todos os creates não foram.
        var partial = await device.GetUsersAsync();
        Assert.Equal(3, partial.Count);
        Assert.Equal("Ana Atualizada", partial.Single(u => u.Id == 1).Name);
        Assert.Equal("Bruno Atualizado", partial.Single(u => u.Id == 2).Name);
        Assert.Equal("Carla Dias", partial.Single(u => u.Id == 3).Name);
        Assert.DoesNotContain(partial, u => u.Id == 100);
        Assert.DoesNotContain(partial, u => u.Id == 101);
        Assert.Equal(2, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(0, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));

        var changes = await device.CreateOrModifyObjectsAsync("users", values);

        // Os dois itens já gravados foram alterados de novo e os dois novos criados, sem repetir o comando nativo.
        Assert.Equal(4, changes);
        var converged = await device.GetUsersAsync();
        Assert.Equal(5, converged.Count);
        Assert.Equal("Ana Atualizada", converged.Single(u => u.Id == 1).Name);
        Assert.Equal("Bruno Atualizado", converged.Single(u => u.Id == 2).Name);
        Assert.Equal("Pessoa Nova", converged.Single(u => u.Id == 100).Name);
        Assert.Equal("Outra Nova", converged.Single(u => u.Id == 101).Name);
        Assert.Equal(1, converged.Count(u => u.Id == 100));
        Assert.Equal(1, converged.Count(u => u.Id == 101));
        Assert.Equal(6, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));
    }

    [Fact]
    public async Task CreateOrModifyObjectsAsync_fails_midway_through_the_creates_and_repeating_converges()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("create_or_modify_objects"));
        using var http = new HttpClient(new FailNthCommandHandler("create_objects", failAt: 2)) { Timeout = TimeSpan.FromSeconds(10) };

        // Uma pessoa por create força dois pedidos de create; o segundo cai no meio da etapa de criação.
        using var device = new AccessClient(Options(sim, maxObjectsPerRequest: 1), http);
        var values = new[]
        {
            new AccessUser { Id = 1, Name = "Ana Atualizada" },
            new AccessUser { Id = 100, Name = "Pessoa Nova", Registration = "9100" },
            new AccessUser { Id = 101, Name = "Outra Nova", Registration = "9101" },
        };

        var failure = await Assert.ThrowsAsync<ControlIdOutcomeUnknownException>(
            () => device.CreateOrModifyObjectsAsync("users", values));

        Assert.Equal("create_objects", failure.Command);
        Assert.False(failure.TimedOut);

        // Todos os modifies terminaram; a primeira pessoa nova foi criada e a segunda não.
        var partial = await device.GetUsersAsync();
        Assert.Equal(4, partial.Count);
        Assert.Equal("Ana Atualizada", partial.Single(u => u.Id == 1).Name);
        Assert.Equal("Bruno Lima", partial.Single(u => u.Id == 2).Name);
        Assert.Equal("Pessoa Nova", partial.Single(u => u.Id == 100).Name);
        Assert.DoesNotContain(partial, u => u.Id == 101);
        Assert.Equal(3, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));

        var changes = await device.CreateOrModifyObjectsAsync("users", values);

        // A pessoa que já existia é alterada e só a que faltava é criada; nenhum id aparece duas vezes.
        Assert.Equal(3, changes);
        var converged = await device.GetUsersAsync();
        Assert.Equal(5, converged.Count);
        Assert.Equal("Ana Atualizada", converged.Single(u => u.Id == 1).Name);
        Assert.Equal("Pessoa Nova", converged.Single(u => u.Id == 100).Name);
        Assert.Equal("Outra Nova", converged.Single(u => u.Id == 101).Name);
        Assert.Equal(1, converged.Count(u => u.Id == 100));
        Assert.Equal(1, converged.Count(u => u.Id == 101));
        Assert.Equal(6, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(2, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));
    }

    [Fact]
    public async Task SyncUsersAsync_reports_outcome_unknown_when_a_modify_fails_midway_and_repeating_converges()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("create_or_modify_objects"));
        using var http = new HttpClient(new FailNthCommandHandler("modify_objects", failAt: 3)) { Timeout = TimeSpan.FromSeconds(10) };
        using var device = new AccessClient(Options(sim), http);
        var people = new[]
        {
            new DesiredUser { Id = 1, Name = "Ana Sincronizada" },
            new DesiredUser { Id = 2, Name = "Bruno Sincronizado" },
            new DesiredUser { Id = 100, Name = "Pessoa Nova", Registration = "9100" },
            new DesiredUser { Id = 101, Name = "Outra Nova", Registration = "9101" },
        };

        var first = await device.SyncUsersAsync(people);

        // A queda de conexão numa escrita é ControlIdOutcomeUnknownException: o lote inteiro fica como resultado
        // desconhecido, mesmo os itens que já tinham sido gravados antes da falha.
        Assert.Equal(4, first.Count);
        Assert.All(first, r => Assert.False(r.Success));
        Assert.All(first, r => Assert.Equal(UserSyncStatus.OutcomeUnknown, r.Status));

        var partial = await device.GetUsersAsync();
        Assert.Equal(3, partial.Count);
        Assert.Equal("Ana Sincronizada", partial.Single(u => u.Id == 1).Name);
        Assert.Equal("Bruno Sincronizado", partial.Single(u => u.Id == 2).Name);
        Assert.Equal("Carla Dias", partial.Single(u => u.Id == 3).Name);
        Assert.DoesNotContain(partial, u => u.Id == 100);
        Assert.DoesNotContain(partial, u => u.Id == 101);
        Assert.Equal(2, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(0, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));

        var retry = await device.SyncUsersAsync(people);
        var byId = retry.ToDictionary(r => r.UserId);

        // Na nova tentativa, quem já estava correto não é escrito e quem faltava é criado.
        Assert.Equal(UserSyncStatus.Unchanged, byId[1].Status);
        Assert.Equal(UserSyncStatus.Unchanged, byId[2].Status);
        Assert.Equal(UserSyncStatus.Created, byId[100].Status);
        Assert.Equal(UserSyncStatus.Created, byId[101].Status);
        var converged = await device.GetUsersAsync();
        Assert.Equal(5, converged.Count);
        Assert.Equal("Ana Sincronizada", converged.Single(u => u.Id == 1).Name);
        Assert.Equal("Bruno Sincronizado", converged.Single(u => u.Id == 2).Name);
        Assert.Equal("Pessoa Nova", converged.Single(u => u.Id == 100).Name);
        Assert.Equal("Outra Nova", converged.Single(u => u.Id == 101).Name);
        Assert.Equal(1, converged.Count(u => u.Id == 100));
        Assert.Equal(1, converged.Count(u => u.Id == 101));
        Assert.Equal(4, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));
    }

    [Fact]
    public async Task SyncUsersAsync_reports_outcome_unknown_when_a_create_fails_midway_and_repeating_converges()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("create_or_modify_objects"));
        using var http = new HttpClient(new FailNthCommandHandler("create_objects", failAt: 2)) { Timeout = TimeSpan.FromSeconds(10) };

        // Uma pessoa por create força dois pedidos de create; o segundo cai depois de a primeira já ter sido criada.
        using var device = new AccessClient(Options(sim, maxObjectsPerRequest: 1), http);
        var people = new[]
        {
            new DesiredUser { Id = 100, Name = "Pessoa Nova", Registration = "9100" },
            new DesiredUser { Id = 101, Name = "Outra Nova", Registration = "9101" },
        };

        var first = await device.SyncUsersAsync(people);

        Assert.Equal(2, first.Count);
        Assert.All(first, r => Assert.False(r.Success));
        Assert.All(first, r => Assert.Equal(UserSyncStatus.OutcomeUnknown, r.Status));

        var partial = await device.GetUsersAsync();
        Assert.Equal(4, partial.Count);
        Assert.Equal("Pessoa Nova", partial.Single(u => u.Id == 100).Name);
        Assert.DoesNotContain(partial, u => u.Id == 101);
        Assert.Equal(2, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));

        var retry = await device.SyncUsersAsync(people);
        var byId = retry.ToDictionary(r => r.UserId);

        Assert.Equal(UserSyncStatus.Unchanged, byId[100].Status);
        Assert.Equal(UserSyncStatus.Created, byId[101].Status);
        var converged = await device.GetUsersAsync();
        Assert.Equal(5, converged.Count);
        Assert.Equal("Pessoa Nova", converged.Single(u => u.Id == 100).Name);
        Assert.Equal("Outra Nova", converged.Single(u => u.Id == 101).Name);
        Assert.Equal(1, converged.Count(u => u.Id == 100));
        Assert.Equal(1, converged.Count(u => u.Id == 101));
        Assert.Equal(3, await sim.RequestCountAsync("modify_objects"));
        Assert.Equal(2, await sim.RequestCountAsync("create_objects"));
        Assert.Equal(1, await sim.RequestCountAsync("create_or_modify_objects"));
    }

    private static ControlIdDeviceOptions Options(RunningSimulator sim, int? maxObjectsPerRequest = null)
    {
        var options = new ControlIdDeviceOptions
        {
            BaseAddress = sim.BaseAddress,
            Login = "admin",
            Password = "admin",
            Timeout = TimeSpan.FromSeconds(10),
        };

        if (maxObjectsPerRequest is { } max)
        {
            options.MaxObjectsPerRequest = max;
        }

        return options;
    }

    /// <summary>Deixa tudo passar e derruba a N-ésima requisição de um comando, como uma conexão que cai depois do envio.</summary>
    private sealed class FailNthCommandHandler : DelegatingHandler
    {
        private readonly string _command;
        private readonly int _failAt;
        private int _seen;

        public FailNthCommandHandler(string command, int failAt)
            : base(new HttpClientHandler())
        {
            _command = command;
            _failAt = failAt;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var endpoint = request.RequestUri?.AbsolutePath.Trim('/') ?? string.Empty;
            if (string.Equals(endpoint, _command + ".fcgi", StringComparison.Ordinal)
                && Interlocked.Increment(ref _seen) == _failAt)
            {
                throw new HttpRequestException($"Falha simulada na requisição {_failAt} de '{_command}'.");
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
