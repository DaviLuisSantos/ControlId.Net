using ControlId.Access;

namespace ControlId.Tests;

public class EnrollAndWaitTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(10);

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler()
        .OnJson("login.fcgi", """{"session":"s"}""")
        .OnJson("remote_enroll.fcgi", "{}")
        .OnJson("cancel_remote_enroll.fcgi", "{}");

    private static RemoteEnrollRequest FaceRequest() => new(RemoteEnrollType.Face) { UserId = 7, Save = true };

    [Fact]
    public async Task Succeeds_after_the_enroll_screen_closes()
    {
        var device = LoggedDevice()
            .OnJson("enroller_state.fcgi", """{"enroller_state":"NORMAL_STATE","biometry_state":"NOT_ENROLLING","last_enroll":0}""")
            .OnJson("enroller_state.fcgi", """{"enroller_state":"ENROLLING","biometry_state":"ENROLLING_FACE","last_enroll":0}""")
            .OnJson("enroller_state.fcgi", """{"enroller_state":"NORMAL_STATE","biometry_state":"NOT_ENROLLING","last_enroll":1,"last_enroll_error":"UNKNOWN"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.EnrollAndWaitAsync(FaceRequest(), pollInterval: Fast);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(7, result.UserId);
        Assert.Contains("\"sync\":false", device.RequestsTo("remote_enroll.fcgi").Single().Body);
        Assert.Empty(device.RequestsTo("cancel_remote_enroll.fcgi"));
    }

    [Fact]
    public async Task Reports_the_device_error_when_the_enroll_fails()
    {
        var device = LoggedDevice()
            .OnJson("enroller_state.fcgi", """{"biometry_state":"ENROLLING_FACE","last_enroll":0}""")
            .OnJson("enroller_state.fcgi", """{"biometry_state":"NOT_ENROLLING","last_enroll":0,"last_enroll_error":"TIMEOUT"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.EnrollAndWaitAsync(FaceRequest(), pollInterval: Fast);

        Assert.False(result.Success);
        Assert.Equal("TIMEOUT", result.Error);
    }

    [Fact]
    public async Task Times_out_and_cancels_the_enroll_on_the_device()
    {
        var device = LoggedDevice()
            .OnJson("enroller_state.fcgi", """{"biometry_state":"ENROLLING_FACE","last_enroll":0}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ControlIdTimeoutException>(() =>
            client.EnrollAndWaitAsync(FaceRequest(), timeout: TimeSpan.FromMilliseconds(150), pollInterval: Fast));

        Assert.Single(device.RequestsTo("cancel_remote_enroll.fcgi"));
    }

    [Fact]
    public async Task Cancellation_cancels_the_enroll_on_the_device()
    {
        var device = LoggedDevice()
            .OnJson("enroller_state.fcgi", """{"biometry_state":"ENROLLING_FACE","last_enroll":0}""");
        using var client = new AccessClient(Options, device.CreateClient());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.EnrollAndWaitAsync(FaceRequest(), pollInterval: Fast, cancellationToken: cts.Token));

        Assert.Single(device.RequestsTo("cancel_remote_enroll.fcgi"));
    }

    [Fact]
    public async Task Rejects_non_positive_timeout()
    {
        using var client = new AccessClient(Options, LoggedDevice().CreateClient());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.EnrollAndWaitAsync(FaceRequest(), timeout: TimeSpan.Zero));
    }
}
