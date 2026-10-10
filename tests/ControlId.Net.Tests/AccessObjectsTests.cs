using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;

namespace ControlId.Tests;

public class AccessObjectsTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task GetCards_filters_by_user_when_informed()
    {
        var device = LoggedDevice().OnJson("load_objects.fcgi", """{"cards":[{"id":1,"value":123456789,"user_id":7}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var cards = await client.GetCardsAsync(userId: 7);

        Assert.Equal(123456789, Assert.Single(cards).Value);
        Assert.Equal(
            """{"object":"cards","where":{"cards":{"user_id":7}}}""",
            device.RequestsTo("load_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task GetGroups_without_filter_sends_only_object()
    {
        var device = LoggedDevice().OnJson("load_objects.fcgi", """{"groups":[{"id":1,"name":"Portaria"}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var groups = await client.GetGroupsAsync();

        Assert.Equal("Portaria", Assert.Single(groups).Name);
        Assert.Equal("""{"object":"groups"}""", device.RequestsTo("load_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task CreateAccessRule_serializes_type_as_number()
    {
        var device = LoggedDevice().OnJson("create_objects.fcgi", """{"ids":[3]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var id = await client.CreateAccessRuleAsync(new AccessRule { Name = "Horário comercial", Type = AccessRuleType.Allow, Priority = 0 });

        Assert.Equal(3, id);
        Assert.Equal(
            """{"object":"access_rules","values":[{"name":"Horário comercial","type":1,"priority":0}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task DeleteTemplate_destroys_by_id()
    {
        var device = LoggedDevice().OnJson("destroy_objects.fcgi", """{"changes":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.DeleteTemplateAsync(9));
        Assert.Equal(
            """{"object":"templates","where":{"templates":{"id":9}}}""",
            device.RequestsTo("destroy_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task TimeSpan_days_go_as_0_and_1()
    {
        var device = LoggedDevice()
            .OnJson("create_objects.fcgi", """{"ids":[5]}""")
            .OnJson("load_objects.fcgi", """{"time_spans":[{"id":5,"time_zone_id":2,"start":28800,"end":64800,"sun":0,"mon":1,"tue":1,"wed":1,"thu":1,"fri":1,"sat":0,"hol1":0,"hol2":0,"hol3":0}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.CreateTimeSpanAsync(AccessTimeSpan.Weekdays(2, TimeSpan.FromHours(8), TimeSpan.FromHours(18)));
        var spans = await client.GetTimeSpansAsync(timeZoneId: 2);

        Assert.Equal(
            """{"object":"time_spans","values":[{"time_zone_id":2,"start":28800,"end":64800,"sun":0,"mon":1,"tue":1,"wed":1,"thu":1,"fri":1,"sat":0,"hol1":0,"hol2":0,"hol3":0}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);

        var span = Assert.Single(spans);
        Assert.True(span.Monday);
        Assert.False(span.Sunday);
        Assert.Equal(64800, span.End);
    }

    [Fact]
    public void TimeSpan_rejects_time_outside_the_day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AccessTimeSpan.EveryDay(1, TimeSpan.Zero, TimeSpan.FromHours(25)));
    }

    [Fact]
    public async Task Link_and_unlink_user_group()
    {
        var device = LoggedDevice()
            .OnJson("create_objects.fcgi", """{"ids":[]}""")
            .OnJson("destroy_objects.fcgi", """{"changes":0}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.LinkAsync(AccessLink.UserGroup(userId: 7, groupId: 2));
        var removed = await client.UnlinkAsync(AccessLink.UserGroup(userId: 7, groupId: 2));

        Assert.False(removed);
        Assert.Equal(
            """{"object":"user_groups","values":[{"user_id":7,"group_id":2}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);
        Assert.Equal(
            """{"object":"user_groups","where":{"user_groups":{"user_id":7,"group_id":2}}}""",
            device.RequestsTo("destroy_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task SetUserImage_posts_octet_stream_with_user_id_in_query()
    {
        var device = LoggedDevice().OnJson("user_set_image.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetUserImageAsync(7, Encoding.ASCII.GetBytes("JPEG"));

        var request = device.RequestsTo("user_set_image.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("?session=s&user_id=7", request.Uri.Query);
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal("JPEG", request.Body);
    }

    [Fact]
    public async Task SetUserImage_rejects_images_of_1MB_or_more()
    {
        using var client = new AccessClient(Options, LoggedDevice().CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SetUserImageAsync(7, new byte[1024 * 1024]));
    }

    [Fact]
    public async Task SetUserImages_sends_base64_list()
    {
        var device = LoggedDevice().OnJson("user_set_image_list.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetUserImagesAsync(new[] { new UserImage(7, new byte[] { 1, 2, 3 }) });

        Assert.Equal(
            """{"user_images":[{"user_id":7,"image":"AQID"}]}""",
            device.RequestsTo("user_set_image_list.fcgi").Single().Body);
    }

    [Fact]
    public async Task GetUserImage_uses_get_and_returns_bytes()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var device = LoggedDevice().OnBytes("user_get_image.fcgi", jpeg, "image/jpeg");
        using var client = new AccessClient(Options, device.CreateClient());

        var image = await client.GetUserImageAsync(7);

        Assert.Equal(jpeg, image);
        var request = device.RequestsTo("user_get_image.fcgi").Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("?session=s&user_id=7", request.Uri.Query);
    }

    [Fact]
    public async Task GetUserImage_relogs_when_session_expires()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"old"}""")
            .OnJson("login.fcgi", """{"session":"new"}""")
            .On("user_get_image.fcgi", HttpStatusCode.Unauthorized)
            .OnBytes("user_get_image.fcgi", new byte[] { 1 }, "image/jpeg");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(new byte[] { 1 }, await client.GetUserImageAsync(7));
        Assert.Equal("?session=new&user_id=7", device.RequestsTo("user_get_image.fcgi").Last().Uri.Query);
    }

    [Fact]
    public async Task GetUserImage_surfaces_device_error()
    {
        var device = LoggedDevice().On("user_get_image.fcgi", HttpStatusCode.BadRequest, """{"error":"Image not found","code":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.GetUserImageAsync(7));
        Assert.Contains("Image not found", ex.Message);
    }

    [Fact]
    public async Task GetUserIdsWithImage_reads_user_ids()
    {
        var device = LoggedDevice().OnJson("user_list_images.fcgi", """{"user_ids":[1,2,3]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(new long[] { 1, 2, 3 }, await client.GetUserIdsWithImageAsync());
    }

    [Fact]
    public async Task ConfigureMonitor_sends_only_informed_fields_as_strings()
    {
        var device = LoggedDevice().OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ConfigureMonitorAsync(new MonitorConfiguration
        {
            Hostname = "192.168.0.20",
            Port = 8000,
            Path = "/api/notifications/",
            RequestTimeout = TimeSpan.FromSeconds(5),
            EnablePhotoUpload = true,
        });

        Assert.Equal(
            """{"monitor":{"hostname":"192.168.0.20","port":"8000","path":"api/notifications","request_timeout":"5000","enable_photo_upload":"1"}}""",
            device.RequestsTo("set_configuration.fcgi").Single().Body);
    }

    [Fact]
    public async Task ConfigurePush_sends_push_server_section()
    {
        var device = LoggedDevice().OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ConfigurePushAsync(new PushConfiguration
        {
            RemoteAddress = "192.168.0.20:8080",
            RequestPeriod = TimeSpan.FromSeconds(5),
            RequestTimeout = TimeSpan.FromSeconds(15),
        });

        Assert.Equal(
            """{"push_server":{"push_remote_address":"192.168.0.20:8080","push_request_timeout":"15000","push_request_period":"5"}}""",
            device.RequestsTo("set_configuration.fcgi").Single().Body);
    }

    [Fact]
    public async Task Delete_images_by_user_and_all()
    {
        var device = LoggedDevice().OnJson("user_destroy_image.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.DeleteUserImageAsync(7);
        await client.DeleteAllUserImagesAsync();

        var bodies = device.RequestsTo("user_destroy_image.fcgi").Select(r => r.Body).ToList();
        Assert.Equal(new[] { """{"user_id":7}""", """{"all":true}""" }, bodies);
    }
}
