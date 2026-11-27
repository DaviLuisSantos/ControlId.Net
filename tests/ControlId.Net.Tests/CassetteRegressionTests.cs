using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Testing;

namespace ControlId.Tests;

/// <summary>
/// Reproduz, sem equipamento, cada cassete de <c>Cassettes/</c>. O cassete sintético cobre login e
/// <c>system_information</c> e serve de exemplo do formato; todo arquivo novo precisa de um cenário abaixo, senão o
/// teste falha antes de tentar reproduzi-lo.
/// </summary>
public class CassetteRegressionTests
{
    private const string SyntheticCassette = "sintetico-login-system-information.json";

    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    // Cada cassete tem um cenário: as chamadas que devem consumir as trocas gravadas, na ordem em que foram gravadas.
    private static readonly IReadOnlyDictionary<string, Func<AccessClient, ReplayHandler, Task>> Scenarios =
        new Dictionary<string, Func<AccessClient, ReplayHandler, Task>>(StringComparer.Ordinal)
        {
            [SyntheticCassette] = ReplayLoginAndSystemInformationAsync,
        };

    private static string CassetteDirectory => Path.Combine(AppContext.BaseDirectory, "Cassettes");

    public static IEnumerable<object[]> CassetteFiles()
    {
        if (!Directory.Exists(CassetteDirectory))
        {
            return Array.Empty<object[]>();
        }

        return Directory.GetFiles(CassetteDirectory, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => new object[] { name! })
            .OrderBy(row => (string)row[0], StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void Every_cassette_has_a_scenario()
    {
        Assert.True(Directory.Exists(CassetteDirectory), "A pasta de cassetes não foi copiada para a saída: " + CassetteDirectory);

        var files = Directory.GetFiles(CassetteDirectory, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var scenarios = Scenarios.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.Equal(scenarios, files, StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CassetteFiles))]
    public async Task Replays_cassette_without_the_device(string fileName)
    {
        Assert.True(
            Scenarios.TryGetValue(fileName, out var scenario),
            $"O cassete '{fileName}' não tem cenário em {nameof(CassetteRegressionTests)}; registre-o em {nameof(Scenarios)}.");

        var cassette = Cassette.Load(Path.Combine(CassetteDirectory, fileName));
        var replay = new ReplayHandler(cassette);
        var run = scenario!;

        using var http = new HttpClient(replay);
        using var client = new AccessClient(Options, http);

        await run(client, replay);

        Assert.Equal(0, replay.Remaining);
    }

    private static async Task ReplayLoginAndSystemInformationAsync(AccessClient client, ReplayHandler replay)
    {
        var information = await client.GetSystemInformationAsync();

        Assert.Equal("SINTETICO-0001", information.Serial);
        Assert.Equal("0.0.0-sintetico", information.Version);
        Assert.False(information.Online);
        Assert.Equal(1, information.Uptime?.Days ?? -1);
        Assert.Equal("192.0.2.10", information.Network?.Ip);
    }
}
