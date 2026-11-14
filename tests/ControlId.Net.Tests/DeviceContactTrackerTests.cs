using ControlId.Access;

namespace ControlId.Tests;

public class DeviceContactTrackerTests
{
    [Fact]
    public void Desconhecido_RetornaNull()
    {
        Assert.Null(new DeviceContactTracker().GetLastContact(1));
    }

    [Fact]
    public void UltimoVence()
    {
        var t = new DeviceContactTracker();
        t.Record(1, DeviceContactSource.Push);
        t.Record(1, DeviceContactSource.Monitor);
        var c = t.GetLastContact(1);
        Assert.NotNull(c);
        Assert.Equal(DeviceContactSource.Monitor, c!.Source);
        Assert.Single(t.GetAll());
    }

    [Fact]
    public void Concorrencia_RegistraTodos()
    {
        var t = new DeviceContactTracker();
        Parallel.For(0, 1000, i => t.Record(i % 50, (DeviceContactSource)(i % 3)));
        var all = t.GetAll();
        Assert.Equal(50, all.Count);
        Assert.All(all.Values, c => Assert.True(c.At > default(DateTimeOffset)));
    }
}
