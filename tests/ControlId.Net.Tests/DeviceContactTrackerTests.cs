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

    [Fact]
    public void AcimaDoLimite_EquipamentoNovoEIgnorado()
    {
        var t = new DeviceContactTracker(maxDevices: 2);
        t.Record(1, DeviceContactSource.Push);
        t.Record(2, DeviceContactSource.Push);
        t.Record(3, DeviceContactSource.Push);

        Assert.Null(t.GetLastContact(3));
        Assert.Equal(2, t.GetAll().Count);

        // Os já conhecidos continuam atualizados.
        t.Record(1, DeviceContactSource.Online);
        Assert.Equal(DeviceContactSource.Online, t.GetLastContact(1)!.Source);
    }

    [Fact]
    public void Limite_ConcorrenciaNaoUltrapassa()
    {
        var t = new DeviceContactTracker(maxDevices: 10);
        Parallel.For(0, 5000, i => t.Record(i % 500, DeviceContactSource.Monitor));
        Assert.Equal(10, t.GetAll().Count);
    }

    [Fact]
    public void PadraoAcompanha10000()
    {
        var t = new DeviceContactTracker();
        for (long i = 0; i < 10_001; i++)
        {
            t.Record(i, DeviceContactSource.Push);
        }

        Assert.Equal(10_000, t.GetAll().Count);
        Assert.NotNull(t.GetLastContact(9_999));
        Assert.Null(t.GetLastContact(10_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LimiteInvalido_Lanca(int max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeviceContactTracker(max));
    }
}
