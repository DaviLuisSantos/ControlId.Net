namespace ControlId.Net.IntegrationTests;

/// <summary>O equipamento grava a hora local codificada como epoch; <c>LocalTime</c> devolve essa hora sem converter fuso (ADR 0028).</summary>
public sealed class LocalTimeEndToEndTests
{
    [Fact]
    public async Task Access_log_local_time_is_the_wall_clock_of_the_device()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        var before = DateTime.SpecifyKind(DateTime.Now.AddSeconds(-5), DateTimeKind.Unspecified);

        await sim.IdentifyAsync(userId: 1);
        var after = DateTime.SpecifyKind(DateTime.Now.AddSeconds(5), DateTimeKind.Unspecified);

        var log = Assert.Single(await device.GetAccessLogsAsync());
        Assert.Equal(DateTimeKind.Unspecified, log.LocalTime.Kind);
        Assert.InRange(log.LocalTime, before, after);
    }
}
