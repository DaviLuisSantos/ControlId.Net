using System.Text;
using ControlId.Access;

namespace ControlId.Tests;

public class MediaPhase4Tests
{
    private const int MB = 1024 * 1024;

    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Video_is_sent_in_parts_of_2MB_with_current_and_total()
    {
        var video = new byte[5 * MB];
        for (var i = 0; i < video.Length; i++)
        {
            video[i] = (byte)(i / MB); // cada MB com um valor, para conferir a ordem
        }

        var device = LoggedDevice().OnJson("send_video.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.UploadAdvertisingVideoAsync(video);

        var parts = device.RequestsTo("send_video.fcgi").ToList();
        Assert.Equal(new[] { "?session=s&current=1&total=3", "?session=s&current=2&total=3", "?session=s&current=3&total=3" }, parts.Select(p => p.Uri.Query));
        Assert.All(parts, p => Assert.Equal("application/octet-stream", p.ContentType));
        // Bytes 0 a 4 são ASCII: o tamanho do texto é o número de bytes, e o primeiro byte mostra a ordem.
        Assert.Equal(new[] { 2 * MB, 2 * MB, MB }, parts.Select(p => p.Body.Length));
        Assert.Equal(new[] { 0, 2, 4 }, parts.Select(p => (int)p.Body[0]));
    }

    [Fact]
    public async Task Size_limits_are_checked_before_sending()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.UploadAdvertisingVideoAsync(new byte[20 * MB + 1]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UploadAccessAudioAsync(AccessAudioEvent.Authorized, new byte[5 * MB + 1]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UploadSipCallAudioAsync(Array.Empty<byte>()));
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Advertising_mode_and_removal()
    {
        var device = LoggedDevice()
            .OnJson("set_custom_video.fcgi", "{}")
            .OnJson("remove_custom_video.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetAdvertisingModeAsync(true);
        await client.SetAdvertisingModeAsync(false);
        await client.RemoveAdvertisingVideoAsync();

        Assert.Equal(
            new[] { """{"custom_video_enabled":"1"}""", """{"custom_video_enabled":"0"}""" },
            device.RequestsTo("set_custom_video.fcgi").Select(r => r.Body));
        Assert.Single(device.RequestsTo("remove_custom_video.fcgi"));
    }

    [Fact]
    public async Task Access_audio_upload_download_and_status()
    {
        var device = LoggedDevice()
            .OnJson("set_audio_access_message.fcgi", "")
            .OnBytes("get_audio_access_message.fcgi", new byte[] { 0x52, 0x49 }, "audio/wav")
            .OnJson("has_audio_access_messages.fcgi", """{"not_identified":false,"authorized":true,"not_authorized":false,"use_mask":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.UploadAccessAudioAsync(AccessAudioEvent.Authorized, new byte[] { 1, 2, 3 });
        var audio = await client.GetAccessAudioAsync(AccessAudioEvent.UseMask);
        var status = await client.GetAccessAudioStatusAsync();

        Assert.Equal("?session=s&event=authorized&current=1&total=1", device.RequestsTo("set_audio_access_message.fcgi").Single().Uri.Query);
        Assert.Equal("""{"event":"use_mask"}""", device.RequestsTo("get_audio_access_message.fcgi").Single().Body);
        Assert.Equal(new byte[] { 0x52, 0x49 }, audio);
        Assert.True(status.Authorized);
        Assert.False(status.NotIdentified);
    }

    [Fact]
    public async Task Sip_calls_status_and_call_audio()
    {
        var device = LoggedDevice()
            .OnJson("make_sip_call.fcgi", "")
            .OnJson("finalize_sip_call.fcgi", "")
            .OnJson("get_sip_status.fcgi", """{"status":200,"in_call":true}""")
            .OnJson("set_pjsip_audio_message.fcgi", "")
            .OnBytes("get_pjsip_audio_message.fcgi", new byte[] { 9 }, "audio/wav")
            .OnJson("has_pjsip_audio_message.fcgi", """{"file_exists":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.MakeSipCallAsync("503");
        await client.FinalizeSipCallAsync();
        var status = await client.GetSipStatusAsync();
        await client.UploadSipCallAudioAsync(new byte[] { 1 });

        Assert.Equal("""{"target":"503"}""", device.RequestsTo("make_sip_call.fcgi").Single().Body);
        Assert.True(status.Registered);
        Assert.True(status.InCall);
        Assert.Equal("?session=s&current=1&total=1", device.RequestsTo("set_pjsip_audio_message.fcgi").Single().Uri.Query);
        Assert.Equal(new byte[] { 9 }, await client.GetSipCallAudioAsync());
        Assert.True(await client.HasSipCallAudioAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => client.MakeSipCallAsync(" "));
    }

    [Fact]
    public void Rtsp_uri_follows_documented_pattern()
    {
        using var client = new AccessClient(new ControlIdDeviceOptions { BaseAddress = new Uri("http://192.168.0.50:8080") });

        Assert.Equal("rtsp://192.168.0.50:554/main_stream", client.GetRtspStreamUri().ToString());
        Assert.Equal("rtsp://admin:p%40ss@192.168.0.50:556/main_stream", client.GetRtspStreamUri(556, "admin", "p@ss").ToString());
    }
}
