using ControlId.Access;
using ControlId.Access.Online;
using ControlId.Internal;

namespace ControlId.Tests;

public class DeviceLocalTimeTests
{
    // O equipamento grava a hora de parede local (10:00) como se fosse epoch UTC.
    private static readonly long Ten = new DateTimeOffset(2024, 3, 5, 10, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    private static readonly DateTime Expected = new(2024, 3, 5, 10, 0, 0);

    [Fact]
    public void FromEpoch_keeps_the_wall_clock_and_leaves_the_kind_unspecified()
    {
        var value = DeviceLocalTime.FromEpoch(Ten);

        Assert.Equal(Expected, value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void AccessLog_local_time()
    {
        var value = new AccessLog { Time = Ten }.LocalTime;

        Assert.Equal(Expected, value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void AccessDeviceEvent_local_timestamp()
    {
        var value = new AccessDeviceEvent { Timestamp = Ten }.LocalTimestamp;

        Assert.Equal(Expected, value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void AlarmLog_local_time()
    {
        var value = new AlarmLog { Time = Ten }.LocalTime;

        Assert.Equal(Expected, value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void ChangeLog_local_timestamp()
    {
        var value = new ChangeLog { Timestamp = Ten }.LocalTimestamp;

        Assert.Equal(Expected, value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void ExitButtonLog_local_time()
    {
        var value = new ExitButtonLog { Time = Ten }.LocalTime;

        Assert.Equal(Expected, value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void OnlineIdentificationEvent_local_time()
    {
        var form = OnlineEvents.ParseForm($"device_id=1&identifier_id=0&event=7&user_id=6&time={Ten}&portal_id=1");
        var e = Assert.IsType<UserIdentifiedEvent>(OnlineEvents.Parse("/api/new_user_identified.fcgi", form));

        Assert.Equal(Expected, e.LocalTime);
        Assert.Equal(DateTimeKind.Unspecified, e.LocalTime!.Value.Kind);
    }
}
