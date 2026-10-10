using System.Net;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;

namespace ControlId.Tests;

public class RemoteEnrollTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Sync_face_enroll_sends_documented_body_and_reads_image()
    {
        var device = LoggedDevice().OnJson(
            "remote_enroll.fcgi", """{"success":true,"user_id":123,"device_id":935107,"user_image":"/9j/4A=="}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Face)
        {
            UserId = 123,
            Save = true,
            Auto = true,
            Countdown = TimeSpan.FromSeconds(3),
            Message = "Olhe para a câmera",
        });

        Assert.True(result.Success);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, result.UserImage);
        Assert.Equal(935107, result.DeviceId);
        Assert.Equal(
            """{"type":"face","save":true,"sync":true,"user_id":123,"message":"Olhe para a câmera","auto":true,"countdown":3}""",
            device.RequestsTo("remote_enroll.fcgi").Single().Body);
    }

    [Fact]
    public async Task Biometry_result_has_finger_images()
    {
        var device = LoggedDevice().OnJson("remote_enroll.fcgi", """
            {"success":true,"user_id":1,"device_id":2,"finger_type":1,
             "fingerprints":[{"image":"AQID","width":300,"height":200},{"image":"BAUG","width":300,"height":200}]}
            """);
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Biometry) { UserId = 1, Save = true, PanicFinger = true });

        Assert.Equal(1, result.FingerType);
        Assert.Equal(2, result.Fingerprints!.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Fingerprints[0].Image);
        Assert.Contains("\"panic_finger\":1", device.RequestsTo("remote_enroll.fcgi").Single().Body);
    }

    [Fact]
    public async Task Card_capture_without_saving()
    {
        var device = LoggedDevice().OnJson("remote_enroll.fcgi", """{"success":true,"device_id":2,"card_value":"132456789"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Card));

        Assert.Equal(132456789, result.CardValue);
        Assert.Equal("""{"type":"card","save":false,"sync":true}""", device.RequestsTo("remote_enroll.fcgi").Single().Body);
    }

    [Fact]
    public async Task Pin_sent_as_number_is_read_as_text()
    {
        var device = LoggedDevice().OnJson("remote_enroll.fcgi", """{"success":true,"pin_value":1324}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("1324", (await client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Pin))).PinValue);
    }

    [Fact]
    public async Task Face_exists_is_a_result_not_an_exception()
    {
        var device = LoggedDevice().OnJson(
            "remote_enroll.fcgi", """{"success":false,"user_id":5,"device_id":2,"error":"FACE_EXISTS","info":{"match_user_id":42}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Face) { UserId = 5, Save = true });

        Assert.False(result.Success);
        Assert.Equal("FACE_EXISTS", result.Error);
        Assert.Equal(42, result.MatchUserId);
        var ex = Assert.Throws<ControlIdException>(() => result.EnsureSuccess());
        Assert.Contains("42", ex.Message);
    }

    [Fact]
    public async Task Save_requires_user_id()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Card) { Save = true }));
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Timeout_cancels_the_enroll_on_the_device()
    {
        var device = LoggedDevice()
            .OnJson("remote_enroll.fcgi", "{}")
            .OnJson("cancel_remote_enroll.fcgi", "");
        var http = new HttpClient(new DelayingHandler(device, "remote_enroll.fcgi", TimeSpan.FromSeconds(5))) { Timeout = TimeSpan.FromMilliseconds(200) };
        using var client = new AccessClient(Options, http);

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Card)));

        Assert.Contains("Timeout", ex.Message);
        Assert.Single(device.RequestsTo("cancel_remote_enroll.fcgi"));
    }

    [Fact]
    public async Task Caller_cancellation_also_cancels_on_the_device()
    {
        var device = LoggedDevice()
            .OnJson("remote_enroll.fcgi", "{}")
            .OnJson("cancel_remote_enroll.fcgi", "");
        using var client = new AccessClient(Options, new HttpClient(new DelayingHandler(device, "remote_enroll.fcgi", TimeSpan.FromSeconds(5))));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Face), cts.Token));

        Assert.Single(device.RequestsTo("cancel_remote_enroll.fcgi"));
    }

    [Fact]
    public async Task Start_is_async_and_cancel_has_no_body()
    {
        var device = LoggedDevice()
            .OnJson("remote_enroll.fcgi", "")
            .OnJson("cancel_remote_enroll.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.StartRemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Password) { UserId = 9, Save = true, Registration = "1020" });
        await client.CancelRemoteEnrollAsync();

        Assert.Equal(
            """{"type":"password","save":true,"sync":false,"user_id":9,"registration":"1020"}""",
            device.RequestsTo("remote_enroll.fcgi").Single().Body);
        Assert.Equal("{}", device.RequestsTo("cancel_remote_enroll.fcgi").Single().Body);
    }

    [Theory]
    [InlineData("card", RemoteEnrollType.Card, """{"success":true,"user_id":1,"device_id":2,"card_value":99}""")]
    [InlineData("user_image", RemoteEnrollType.Face, """{"success":true,"user_id":1,"device_id":2,"user_image":"AQID"}""")]
    [InlineData("face_template", RemoteEnrollType.Face, """{"success":true,"user_id":1,"device_id":2,"face_template":"abc"}""")]
    [InlineData("template", RemoteEnrollType.Biometry, """{"success":true,"user_id":1,"device_id":2,"fingerprints":[]}""")]
    [InlineData("pin", RemoteEnrollType.Pin, """{"success":true,"user_id":1,"device_id":2,"pin_value":"1324"}""")]
    [InlineData("password", RemoteEnrollType.Password, """{"success":false,"user_id":1,"device_id":2,"error":"Timeout"}""")]
    public void Monitor_parses_async_enroll_results(string endpoint, RemoteEnrollType type, string json)
    {
        var notification = Assert.IsType<RemoteEnrollNotification>(MonitorNotifications.Parse("/api/notifications/" + endpoint, json));

        Assert.Equal(type, notification.EnrollType);
        Assert.Equal(2, notification.DeviceId);
        Assert.Equal(1, notification.Result.UserId);
        Assert.Equal(endpoint != "password", notification.Result.Success);
        Assert.Equal(JsonValueKind.Object, notification.Result.Raw.ValueKind);
    }

    [Fact]
    public async Task Push_remote_enroll_round_trip()
    {
        var server = new PushServer();
        var task = server.SendAsync(7, PushCommand.RemoteEnroll(new RemoteEnrollRequest(RemoteEnrollType.Card) { UserId = 3, Save = true }));

        Assert.Contains("\"endpoint\":\"remote_enroll\"", server.HandlePush(7, "u"));
        server.HandleResult(7, "u", """{"response":{"success":true,"user_id":3,"card_value":555}}""");

        var result = (await task.WaitAsync(TimeSpan.FromSeconds(5))).ResponseAs<RemoteEnrollResult>();
        Assert.Equal(555, result.CardValue);
    }

    /// <summary>Atrasa um endpoint para simular a pessoa demorando no equipamento.</summary>
    private sealed class DelayingHandler : DelegatingHandler
    {
        private readonly string _endpoint;
        private readonly TimeSpan _delay;

        public DelayingHandler(HttpMessageHandler inner, string endpoint, TimeSpan delay)
            : base(inner)
        {
            _endpoint = endpoint;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/" + _endpoint, StringComparison.Ordinal))
            {
                await Task.Delay(_delay, cancellationToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
