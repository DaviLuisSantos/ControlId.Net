using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>Fotos em lote: divisão por tamanho e retorno ao envio unitário quando o firmware recusa o lote.</summary>
public sealed class ImageBatchEndToEndTests
{
    // JPEG com SOF0 16x16 (a validação de fotos aceita), completado com zeros até o tamanho pedido.
    private static byte[] Jpeg(int totalBytes)
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00, 0x10, 0x03, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0, 0xFF, 0xD9];
        var bytes = new byte[totalBytes];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private static UserImage[] Photos() => new[]
    {
        new UserImage(1, Jpeg(1500)),
        new UserImage(2, Jpeg(1500)),
        new UserImage(3, Jpeg(1500)),
    };

    [Fact]
    public async Task Batch_goes_in_a_single_request_when_it_fits()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        var results = await device.SetUserImagesAsync(Photos());

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(1, await sim.RequestCountAsync("user_set_image_list"));
        Assert.Equal(0, await sim.RequestCountAsync("user_set_image"));
    }

    [Fact]
    public async Task Batch_is_split_by_the_configured_byte_limit()
    {
        await using var sim = await RunningSimulator.StartAsync();
        using var device = new AccessClient(new ControlIdDeviceOptions
        {
            BaseAddress = sim.BaseAddress,
            Timeout = TimeSpan.FromSeconds(10),
            MaxImageBatchBytes = 4500,
        });

        var results = await device.SetUserImagesAsync(Photos());

        Assert.Equal(new long?[] { 1, 2, 3 }, results.Select(r => r.UserId).ToArray());
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(2, await sim.RequestCountAsync("user_set_image_list"));
        Assert.Equal(Jpeg(1500), await device.GetUserImageAsync(3));
    }

    [Fact]
    public async Task Batch_falls_back_to_single_uploads_when_the_device_refuses_the_body()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.MaxImageListBytes = 3000);
        using var device = sim.CreateClient();

        var results = await device.SetUserImagesAsync(Photos());

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(3, await sim.RequestCountAsync("user_set_image"));
        Assert.Equal(Jpeg(1500), await device.GetUserImageAsync(2));
    }

    [Fact]
    public async Task Batch_reports_not_supported_when_the_firmware_has_no_list_command()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.DisabledCommands.Add("user_set_image_list"));
        using var device = sim.CreateClient();

        // "Não suportado" não cai para o envio unitário: quem chama decide (ADR 0027).
        var error = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => device.SetUserImagesAsync(Photos()));

        Assert.Equal("user_set_image_list", error.Command);
        Assert.Equal(0, await sim.RequestCountAsync("user_set_image"));
    }
}
