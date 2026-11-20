using System.Text.RegularExpressions;
using ControlId.Access;
using ControlId.Internal;

namespace ControlId.Tests;

/// <summary>Imagens de teste com cabeçalho JPEG/PNG válido (só o cabeçalho; o resto é preenchimento).</summary>
internal static class TestImages
{
    /// <summary>JPEG com um SOF0 de <paramref name="width"/> x <paramref name="height"/>, completado com zeros até <paramref name="totalBytes"/>.</summary>
    public static byte[] Jpeg(int width, int height, int totalBytes = 0)
    {
        var header = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08,
            (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
            0x03, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0, 0xFF, 0xD9,
        };
        return Pad(header, totalBytes);
    }

    /// <summary>PNG com um IHDR de <paramref name="width"/> x <paramref name="height"/>, completado com zeros até <paramref name="totalBytes"/>.</summary>
    public static byte[] Png(int width, int height, int totalBytes = 0)
    {
        var header = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52,
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8, 2, 0, 0, 0,
        };
        return Pad(header, totalBytes);
    }

    private static byte[] Pad(byte[] header, int totalBytes)
    {
        if (totalBytes <= header.Length)
        {
            return header;
        }

        var padded = new byte[totalBytes];
        Array.Copy(header, padded, header.Length);
        return padded;
    }
}

public class ImageValidationTests
{
    private static ControlIdDeviceOptions Options(Action<ControlIdDeviceOptions>? configure = null)
    {
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5") };
        configure?.Invoke(options);
        return options;
    }

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public void Header_reader_reads_jpeg_and_png_sizes()
    {
        Assert.True(ImageHeader.TryReadSize(TestImages.Jpeg(640, 480), out var jw, out var jh));
        Assert.Equal((640, 480), (jw, jh));

        Assert.True(ImageHeader.TryReadSize(TestImages.Png(1080, 1920), out var pw, out var ph));
        Assert.Equal((1080, 1920), (pw, ph));
    }

    [Fact]
    public void Header_reader_skips_other_jpeg_segments_before_the_frame_header()
    {
        // SOI, APP0 (JFIF, 16 bytes), DQT (4 bytes de dados) e só então o SOF2 (progressivo).
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0 };
        jpeg.AddRange(new byte[] { 0xFF, 0xDB, 0x00, 0x06, 0, 1, 2, 3 });
        jpeg.AddRange(new byte[] { 0xFF, 0xC2, 0x00, 0x0B, 0x08, 0x02, 0x58, 0x03, 0x20, 0x01, 1, 0x11, 0 });

        Assert.True(ImageHeader.TryReadSize(jpeg.ToArray(), out var width, out var height));
        Assert.Equal((800, 600), (width, height));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xC0 })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1 })]
    public void Header_reader_rejects_unknown_or_truncated_data(byte[] data) =>
        Assert.False(ImageHeader.TryReadSize(data, out _, out _));

    [Fact]
    public void Header_reader_rejects_jpeg_that_reaches_the_scan_without_a_frame_header()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x04, 0, 0 };

        Assert.False(ImageHeader.TryReadSize(jpeg, out _, out _));
    }

    [Fact]
    public async Task Photo_of_4000x3000_fails_without_any_request()
    {
        var device = LoggedDevice().OnJson("user_set_image.fcgi", "");
        using var client = new AccessClient(Options(), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdImageRejectedException>(() => client.SetUserImageAsync(7, TestImages.Jpeg(4000, 3000)));

        Assert.Equal(ImageRejectionReason.Dimensions, error.Reason);
        Assert.Equal(7, error.UserId);
        Assert.Equal(4000, error.Width);
        Assert.Equal(3000, error.Height);
        Assert.Contains("4000x3000", error.Message);
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Photo_within_limits_is_sent_in_landscape_and_portrait()
    {
        var device = LoggedDevice().OnJson("user_set_image.fcgi", "");
        using var client = new AccessClient(Options(), device.CreateClient());

        await client.SetUserImageAsync(1, TestImages.Jpeg(1920, 1080));
        await client.SetUserImageAsync(2, TestImages.Png(1080, 1920));

        Assert.Equal(2, device.RequestsTo("user_set_image.fcgi").Count());
    }

    [Fact]
    public async Task Photo_with_the_short_side_over_the_limit_is_rejected()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options(), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdImageRejectedException>(() => client.SetUserImageAsync(1, TestImages.Png(1500, 1500)));

        Assert.Equal(ImageRejectionReason.Dimensions, error.Reason);
    }

    [Fact]
    public async Task Photo_that_is_not_jpeg_or_png_fails_with_format_reason()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options(), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdImageRejectedException>(() => client.SetUserImageAsync(3, new byte[] { 1, 2, 3, 4, 5 }));

        Assert.Equal(ImageRejectionReason.Format, error.Reason);
        Assert.Equal(3, error.UserId);
        Assert.Null(error.Width);
        Assert.Equal(5, error.ByteCount);
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Photo_over_the_byte_limit_fails_with_size_reason()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options(o => o.MaxImageBytes = 1000), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdImageRejectedException>(() => client.SetUserImageAsync(1, TestImages.Jpeg(100, 100, 1001)));

        Assert.Equal(ImageRejectionReason.Size, error.Reason);
        Assert.Equal(1001, error.ByteCount);
        Assert.Equal(100, error.Width);
    }

    [Fact]
    public async Task Photo_between_1_and_2_MiB_is_accepted_by_default()
    {
        var device = LoggedDevice().OnJson("user_set_image.fcgi", "");
        using var client = new AccessClient(Options(), device.CreateClient());

        await client.SetUserImageAsync(1, TestImages.Jpeg(640, 480, 1536 * 1024));

        Assert.Single(device.RequestsTo("user_set_image.fcgi"));
    }

    [Fact]
    public async Task ValidateImages_false_sends_the_photo_without_reading_the_header()
    {
        var device = LoggedDevice().OnJson("user_set_image.fcgi", "");
        using var client = new AccessClient(Options(o => o.ValidateImages = false), device.CreateClient());

        await client.SetUserImageAsync(1, TestImages.Jpeg(4000, 3000));
        await client.SetUserImageAsync(1, new byte[] { 1, 2, 3 });

        Assert.Equal(2, device.RequestsTo("user_set_image.fcgi").Count());
    }

    [Fact]
    public async Task Empty_photo_is_an_argument_error()
    {
        using var client = new AccessClient(Options(), LoggedDevice().CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SetUserImageAsync(1, Array.Empty<byte>()));
    }

    [Fact]
    public async Task Batch_turns_each_invalid_photo_into_a_failed_result_for_that_user()
    {
        var device = LoggedDevice().OnJson(
            "user_set_image_list.fcgi",
            """{"results":[{"user_id":1,"success":true},{"user_id":3,"success":true}]}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var results = await client.SetUserImagesAsync(new[]
        {
            new UserImage(1, TestImages.Jpeg(640, 480)),
            new UserImage(2, TestImages.Jpeg(4000, 3000)),
            new UserImage(3, TestImages.Png(640, 480)),
            new UserImage(4, new byte[] { 9, 9, 9 }),
        });

        Assert.Equal(4, results.Count);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal(2, results[1].UserId);
        Assert.Contains("4000x3000", Assert.Single(results[1].Errors).Message);
        Assert.True(results[2].Success);
        Assert.Equal(3, results[2].UserId);
        Assert.False(results[3].Success);
        Assert.Equal(4, results[3].UserId);

        var body = Assert.Single(device.RequestsTo("user_set_image_list.fcgi")).Body;
        Assert.Equal(2, Regex.Matches(body, "\"user_id\"").Count);
    }

    [Fact]
    public async Task Batch_with_only_invalid_photos_makes_no_request()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options(), device.CreateClient());

        var results = await client.SetUserImagesAsync(new[] { new UserImage(1, TestImages.Jpeg(5000, 5000)) });

        Assert.False(Assert.Single(results).Success);
        Assert.Empty(device.Requests);
    }
}
