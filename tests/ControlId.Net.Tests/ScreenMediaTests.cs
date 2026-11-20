using ControlId.Access;

namespace ControlId.Tests;

public class ScreenMediaTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "admin",
    };

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Slideshow_upload_sends_octet_stream_with_filename_in_query()
    {
        var device = LoggedDevice().OnJson("send_slideshow_images.fcgi", """{"success":true,"filename":"a b.jpg"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SendSlideshowImageAsync("a b.jpg", Jpeg);

        var request = device.RequestsTo("send_slideshow_images.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal("?session=s&filename=a%20b.jpg", request.Uri.Query);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendSlideshowImageAsync("a.jpg", Png));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendSlideshowImageAsync("a.jpg", Array.Empty<byte>()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendSlideshowImageAsync(" ", Jpeg));
    }

    [Fact]
    public async Task Slideshow_list_accepts_strings_and_objects()
    {
        var device = LoggedDevice().OnJson("get_slideshow_images_list.fcgi", """{"images":["myimage"],"max_files":10}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var list = await client.GetSlideshowImagesAsync();
        Assert.Equal(new[] { "myimage" }, list.Filenames);
        Assert.Equal(10, list.MaxFiles);
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_slideshow_images_list.fcgi").Single().Method);

        var objects = LoggedDevice().OnJson("get_slideshow_images_list.fcgi", """{"images":[{"filename":"a.jpg"},{"filename":"b.jpg"}],"max_files":10}""");
        using var second = new AccessClient(Options, objects.CreateClient());
        Assert.Equal(new[] { "a.jpg", "b.jpg" }, (await second.GetSlideshowImagesAsync()).Filenames);
    }

    [Fact]
    public async Task Slideshow_get_delete_and_remove_all()
    {
        var device = LoggedDevice()
            .OnBytes("get_slideshow_image.fcgi", Jpeg, "image/jpeg")
            .OnJson("delete_slideshow_image.fcgi", """{"success":true}""")
            .OnJson("remove_slideshow_images.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(Jpeg, await client.GetSlideshowImageAsync("image.jpeg"));
        await client.DeleteSlideshowImageAsync("image.jpeg");
        await client.RemoveAllSlideshowImagesAsync(DataLossConfirmation.Confirmed);

        Assert.Equal("?session=s&filename=image.jpeg", device.RequestsTo("get_slideshow_image.fcgi").Single().Uri.Query);
        var delete = device.RequestsTo("delete_slideshow_image.fcgi").Single();
        Assert.Equal(HttpMethod.Post, delete.Method);
        Assert.Equal("?session=s&filename=image.jpeg", delete.Uri.Query);
        Assert.Equal(HttpMethod.Post, device.RequestsTo("remove_slideshow_images.fcgi").Single().Method);
    }

    [Fact]
    public async Task Remove_all_slideshow_images_requires_confirmation()
    {
        var device = LoggedDevice().OnJson("remove_slideshow_images.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.RemoveAllSlideshowImagesAsync(default));
        Assert.Empty(device.RequestsTo("remove_slideshow_images.fcgi"));
    }

    [Fact]
    public async Task Kiosk_icons_round_trip()
    {
        var device = LoggedDevice()
            .OnJson("set_kiosk_icon.fcgi", """{"success":true,"filename":"menu1.png"}""")
            .OnBytes("get_kiosk_icon.fcgi", Png, "image/png")
            .OnJson("delete_kiosk_icon.fcgi", """{"success":true}""")
            .OnJson("get_kiosk_icons_list.fcgi", """{"icons":[{"filename":"menu1.png"},{"filename":"menu2.svg"}],"max_files":4}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetKioskIconAsync("menu1.png", Png);
        Assert.Equal(Png, await client.GetKioskIconAsync("menu1.png"));
        await client.DeleteKioskIconAsync("menu1.png");
        var list = await client.GetKioskIconsAsync();

        var set = device.RequestsTo("set_kiosk_icon.fcgi").Single();
        Assert.Equal("application/octet-stream", set.ContentType);
        Assert.Equal("?session=s&filename=menu1.png", set.Uri.Query);
        Assert.Equal("?session=s&filename=menu1.png", device.RequestsTo("get_kiosk_icon.fcgi").Single().Uri.Query);
        Assert.Equal(HttpMethod.Post, device.RequestsTo("delete_kiosk_icon.fcgi").Single().Method);
        Assert.Equal(new[] { "menu1.png", "menu2.svg" }, list.Filenames);
        Assert.Equal(4, list.MaxFiles);
    }

    [Fact]
    public async Task Kiosk_icon_validates_extension_and_content()
    {
        var device = LoggedDevice().OnJson("set_kiosk_icon.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SetKioskIconAsync("menu.gif", Png));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetKioskIconAsync("menu.png", Array.Empty<byte>()));
        await client.SetKioskIconAsync("MENU.SVG", new byte[] { 1, 2 });
        Assert.Single(device.RequestsTo("set_kiosk_icon.fcgi"));
    }

    [Fact]
    public async Task Phone_icon_round_trip()
    {
        var device = LoggedDevice()
            .OnJson("set_phone_icon.fcgi", "")
            .OnBytes("get_phone_icon.fcgi", Png, "image/png")
            .OnJson("remove_phone_icon.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetPhoneIconAsync(Png);
        Assert.Equal(Png, await client.GetPhoneIconAsync());
        await client.RemovePhoneIconAsync();

        var set = device.RequestsTo("set_phone_icon.fcgi").Single();
        Assert.Equal("application/octet-stream", set.ContentType);
        Assert.Equal("?session=s", set.Uri.Query);
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_phone_icon.fcgi").Single().Method);
        Assert.Equal(HttpMethod.Post, device.RequestsTo("remove_phone_icon.fcgi").Single().Method);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetPhoneIconAsync(Jpeg));
    }

    [Fact]
    public async Task Sip_conference_sends_target_only_when_given()
    {
        var device = LoggedDevice().OnJson("make_sip_conference.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.MakeSipConferenceAsync("sip:ramal@192.168.1.100");
        await client.MakeSipConferenceAsync();

        var requests = device.RequestsTo("make_sip_conference.fcgi").ToList();
        Assert.Equal("""{"target":"sip:ramal@192.168.1.100"}""", requests[0].Body);
        Assert.DoesNotContain("target", requests[1].Body);
    }

    [Fact]
    public async Task Streaming_logo_round_trip_and_limits()
    {
        var device = LoggedDevice()
            .OnJson("set_streaming_logo.fcgi", "")
            .OnBytes("get_streaming_logo.fcgi", Png, "image/png")
            .OnJson("remove_streaming_logo.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetStreamingLogoAsync(Png);
        Assert.Equal(Png, await client.GetStreamingLogoAsync());
        await client.RemoveStreamingLogoAsync();

        var set = device.RequestsTo("set_streaming_logo.fcgi").Single();
        Assert.Equal("application/octet-stream", set.ContentType);
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_streaming_logo.fcgi").Single().Method);
        Assert.Equal(HttpMethod.Post, device.RequestsTo("remove_streaming_logo.fcgi").Single().Method);

        var big = new byte[1024 * 1024];
        Array.Copy(Png, big, Png.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetStreamingLogoAsync(big));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetStreamingLogoAsync(Jpeg));
    }
}
