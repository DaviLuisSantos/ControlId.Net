using System.Text;
using ControlId.Access;
using ControlId.Access.Online;
using ControlId.Internal;

namespace ControlId.Tests;

/// <summary>Códigos numéricos que um firmware mais novo pode mandar e esta versão da biblioteca não conhece.</summary>
public class EnumFallbackTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    [Fact]
    public void Unknown_members_do_not_collide_with_documented_codes()
    {
        Assert.Equal(0, (int)AccessEventType.Unknown);
        Assert.Equal(0, (int)FaceImageErrorCode.Unknown);

        // O 0 já tem significado nestes dois: sem causa / conectado.
        Assert.Equal(-1, (int)AlarmCause.Unknown);
        Assert.Equal(0, (int)AlarmCause.None);
        Assert.Equal(-1, (int)VpnStatus.Unknown);
        Assert.Equal(0, (int)VpnStatus.Connected);

        foreach (var type in new[] { typeof(AccessEventType), typeof(FaceImageErrorCode), typeof(AlarmCause), typeof(VpnStatus) })
        {
            Assert.False(type.IsDefined(typeof(FlagsAttribute), inherit: false));
            var values = Enum.GetValues(type).Cast<int>().ToList();
            Assert.Equal(values.Count, values.Distinct().Count());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(-3)]
    [InlineData(int.MaxValue)]
    public void Access_event_unknown_code(int code)
    {
        var log = new AccessLog { Event = code };

        Assert.Equal(AccessEventType.Unknown, log.EventType);
        Assert.Equal(code, log.Event); // o número original continua disponível
    }

    [Fact]
    public void Access_event_known_code_is_kept()
    {
        Assert.Equal(AccessEventType.Intercom, new AccessLog { Event = 15 }.EventType);
        Assert.Equal(AccessEventType.InvalidDevice, new AccessLog { Event = 1 }.EventType);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-1)]
    [InlineData(-7)]
    public void Alarm_cause_unknown_code(int code)
    {
        Assert.Equal(AlarmCause.Unknown, new AlarmStatus { Cause = code }.CauseType);
        Assert.Equal(AlarmCause.Unknown, new AlarmLog { Cause = code }.CauseType);
    }

    [Fact]
    public void Alarm_cause_zero_is_still_none()
    {
        Assert.Equal(AlarmCause.None, new AlarmStatus { Cause = 0 }.CauseType);
        Assert.Equal(AlarmCause.MonitoredZoneOpen, new AlarmLog { Cause = 14 }.CauseType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(13)]
    public void Face_error_unknown_code(int code)
    {
        Assert.Equal(FaceImageErrorCode.Unknown, new FaceImageError { Code = code }.CodeType);
    }

    [Fact]
    public void Face_error_known_code_is_kept()
    {
        Assert.Equal(FaceImageErrorCode.MultipleFaces, new FaceImageError { Code = 12 }.CodeType);
    }

    [Theory]
    [InlineData("11", VpnStatus.Unknown)]
    [InlineData("-1", VpnStatus.Unknown)]
    [InlineData("0", VpnStatus.Connected)]
    [InlineData("10", VpnStatus.NoConnection)]
    public async Task Vpn_status_unknown_code(string status, VpnStatus expected)
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnBytes("get_vpn_status.fcgi", Encoding.UTF8.GetBytes($$"""{"status":"{{status}}"}"""), "application/json");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(expected, await client.GetVpnStatusAsync());
    }

    [Theory]
    [InlineData("42", AccessEventType.Unknown)]
    [InlineData("7", AccessEventType.AccessGranted)]
    public void Online_event_unknown_code(string code, AccessEventType expected)
    {
        var e = Assert.IsType<UserIdentifiedEvent>(OnlineEvents.Parse(
            "new_user_identified", new[] { new KeyValuePair<string, string>("event", code), new KeyValuePair<string, string>("user_id", "6") }));

        Assert.Equal(expected, e.Event);
    }

    [Fact]
    public void Monitor_access_log_with_unknown_event()
    {
        const string json = """
            { "object_changes": [ { "object": "access_logs", "type": "inserted", "values": { "id": "1", "time": "1700000000", "event": "99" } } ],
              "device_id": 1 }
            """;

        var dao = Assert.IsType<Access.Monitoring.DaoNotification>(Access.Monitoring.MonitorNotifications.Parse("dao", json));

        var log = Assert.Single(dao.GetInsertedAccessLogs());
        Assert.Equal(99, log.Event);
        Assert.Equal(AccessEventType.Unknown, log.EventType);
    }

    [Fact]
    public void To_defined_handles_values_outside_int()
    {
        Assert.Equal(AccessEventType.Unknown, EnumValues.ToDefined<AccessEventType>(long.MaxValue));
        Assert.Equal(AccessEventType.Unknown, EnumValues.ToDefined<AccessEventType>(4_294_967_303)); // 2^32 + 7 não vira 7
        Assert.Equal(AccessEventType.AccessGranted, EnumValues.ToDefined<AccessEventType>(7L));
        Assert.Equal(AlarmCause.Unknown, EnumValues.ToDefined<AlarmCause>(long.MinValue));
    }
}
