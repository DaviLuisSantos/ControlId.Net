using ControlId.Access;

namespace ControlId.Tests;

public class CoverageWave02Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5"), ValidateImages = false };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Network_settings_send_interface_dhcp_and_ten_mbps()
    {
        var device = LoggedDevice().OnJson("set_system_network.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetNetworkAsync(new NetworkSettings("10.0.0.9", "255.0.0.0", "10.0.0.1") { Interface = "2", DhcpEnabled = true, TenMbps = false });

        Assert.Equal(
            """{"ip":"10.0.0.9","netmask":"255.0.0.0","gateway":"10.0.0.1","interface":"2","dhcp_enabled":true,"ten_mbps":false}""",
            device.RequestsTo("set_system_network.fcgi").Single().Body);
    }

    [Fact]
    public async Task Configure8021X_settings_send_eap_method_certificate_and_interface()
    {
        var device = LoggedDevice().OnJson("configure_802_1X.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.Configure8021XAsync(new Ieee8021XSettings
        {
            Enabled = true,
            Login = "testing",
            Password = "password",
            InnerAuthentication = Ieee8021XInnerAuthentication.Gtc,
            EapMethod = Ieee8021XEapMethod.Tls,
            UsesCertificate = true,
            Interface = "1",
        });
        await client.Configure8021XAsync(true, "u", "p");

        var bodies = device.RequestsTo("configure_802_1X.fcgi").Select(r => r.Body).ToArray();
        Assert.Equal(
            """{"enabled":true,"inner_auth":2,"login":"testing","password":"password","eap_auth":2,"uses_certificate":true,"interface":"1"}""",
            bodies[0]);
        Assert.Equal("""{"enabled":true,"inner_auth":0,"login":"u","password":"p"}""", bodies[1]);
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Configure8021XAsync((Ieee8021XSettings)null!));
    }

    [Fact]
    public async Task Factory_reset_options_send_only_the_flags_that_are_on()
    {
        var device = LoggedDevice().OnJson("reset_to_factory_default.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ResetToFactoryDefaultAsync(
            DataLossConfirmation.Confirmed, new FactoryResetOptions { KeepNetworkSettings = true, KeepAllConfiguration = true, KeepOsdpSettings = true });
        await client.ResetToFactoryDefaultAsync(DataLossConfirmation.Confirmed, keepNetworkSettings: false);

        Assert.Equal(
            new[] { """{"keep_network_info":true,"keep_all_config":true,"keep_osdp_info":true}""", """{"keep_network_info":false}""" },
            device.RequestsTo("reset_to_factory_default.fcgi").Select(r => r.Body));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ResetToFactoryDefaultAsync(DataLossConfirmation.Confirmed, (FactoryResetOptions)null!));
    }

    [Fact]
    public async Task Factory_reset_options_require_confirmation()
    {
        var device = LoggedDevice().OnJson("reset_to_factory_default.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.ResetToFactoryDefaultAsync(default, new FactoryResetOptions()));
        Assert.Empty(device.RequestsTo("reset_to_factory_default.fcgi"));
    }

    [Fact]
    public async Task Audit_logs_all_includes_boot_and_push()
    {
        var device = LoggedDevice().OnJson("export_audit_logs.fcgi", "x");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ExportAuditLogsAsync(AuditLogCategories.Boot | AuditLogCategories.Push);
        await client.ExportAuditLogsAsync();

        var bodies = device.RequestsTo("export_audit_logs.fcgi").Select(r => r.Body).ToArray();
        Assert.Equal("""{"config":0,"api":0,"usb":0,"network":0,"time":0,"online":0,"menu":0,"boot":1,"push":1}""", bodies[0]);
        Assert.Equal("""{"config":1,"api":1,"usb":1,"network":1,"time":1,"online":1,"menu":1,"boot":1,"push":1}""", bodies[1]);
        Assert.Equal(128, (int)AuditLogCategories.Boot);
        Assert.Equal(256, (int)AuditLogCategories.Push);
    }

    [Fact]
    public async Task Afd_options_send_limit_offset_and_mode()
    {
        var device = LoggedDevice().OnJson("export_afd.fcgi", "0000000011");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ExportAfdAsync(new AfdExportOptions { InitialNsr = 5, Limit = 100, Offset = 200 });
        await client.ExportAfdAsync(new AfdExportOptions { InitialDate = new DateTime(2022, 7, 11), Portaria671 = false });
        await client.ExportAfdAsync(initialNsr: 1);

        var requests = device.RequestsTo("export_afd.fcgi").ToArray();
        Assert.Equal("""{"initial_nsr":5,"limit":100,"offset":200}""", requests[0].Body);
        Assert.Contains("mode=671", requests[0].Uri.Query);
        Assert.Equal("""{"initial_date":{"day":11,"month":7,"year":2022}}""", requests[1].Body);
        Assert.DoesNotContain("mode=671", requests[1].Uri.Query);
        Assert.Equal("""{"initial_nsr":1}""", requests[2].Body);
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.ExportAfdAsync(new AfdExportOptions { InitialNsr = 1, InitialDate = DateTime.Today }));
    }

    [Fact]
    public async Task CaptureScreen_sends_frame_type_and_png_format()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var device = LoggedDevice().OnBytes("save_screenshot.fcgi", png, "image/png");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(png, await client.CaptureScreenAsync());
        await client.CaptureScreenAsync(ScreenFrame.Display);

        Assert.Equal(
            new[] { """{"frame_type":"screen","format_type":"png"}""", """{"frame_type":"display","format_type":"png"}""" },
            device.RequestsTo("save_screenshot.fcgi").Select(r => r.Body));
    }

    [Fact]
    public async Task SetUserImage_options_send_match_and_bypass_quality_score()
    {
        var device = LoggedDevice().OnJson("user_set_image.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var when = DateTimeOffset.FromUnixTimeSeconds(1624997578);

        await client.SetUserImageAsync(7, new byte[] { 1 }, new UserImageUploadOptions(), when);
        await client.SetUserImageAsync(7, new byte[] { 1 }, new UserImageUploadOptions { RejectExistingFace = false, BypassQualityScore = true }, when);
        await client.SetUserImageAsync(7, new byte[] { 1 }, timestamp: when);

        var queries = device.RequestsTo("user_set_image.fcgi").Select(r => r.Uri.Query).ToArray();
        Assert.EndsWith("user_id=7&timestamp=1624997578&match=1", queries[0]);
        Assert.EndsWith("user_id=7&timestamp=1624997578&match=0&bypass_quality_score=1", queries[1]);
        Assert.EndsWith("user_id=7&timestamp=1624997578&match=0", queries[2]);
    }

    [Fact]
    public async Task SetUserImages_options_send_match_and_bypass_quality_score()
    {
        var device = LoggedDevice().OnJson("user_set_image_list.fcgi", """{"results":[]}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var images = new[] { new UserImage(1, new byte[] { 1 }, DateTimeOffset.FromUnixTimeSeconds(1628727478)) };

        await client.SetUserImagesAsync(images, new UserImageUploadOptions { BypassQualityScore = true });
        await client.SetUserImagesAsync(images);

        var bodies = device.RequestsTo("user_set_image_list.fcgi").Select(r => r.Body).ToArray();
        Assert.Equal(
            """{"match":true,"user_images":[{"timestamp":1628727478,"user_id":1,"image":"AQ=="}],"bypass_quality_score":true}""", bodies[0]);
        Assert.StartsWith("""{"match":false,""", bodies[1]);
        Assert.DoesNotContain("bypass_quality_score", bodies[1]);
    }

    [Fact]
    public async Task EnrollFaceTemplates_rejects_existing_faces_by_default()
    {
        var device = LoggedDevice().OnJson("face_template_enroll.fcgi", """{"results":[]}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var templates = new[] { new FaceTemplateEnrollment(1, "q/HSO4", DateTimeOffset.FromUnixTimeSeconds(1628727478)) };

        await client.EnrollFaceTemplatesAsync(templates);
        await client.EnrollFaceTemplatesAsync(templates, rejectExistingFaces: false);

        var bodies = device.RequestsTo("face_template_enroll.fcgi").Select(r => r.Body).ToArray();
        Assert.StartsWith("""{"match":true,""", bodies[0]);
        Assert.StartsWith("""{"match":false,""", bodies[1]);
    }
}
