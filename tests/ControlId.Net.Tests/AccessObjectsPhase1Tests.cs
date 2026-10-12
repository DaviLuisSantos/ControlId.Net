using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

public class AccessObjectsPhase1Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public void Every_model_maps_to_its_documented_object()
    {
        Assert.Equal("qrcodes", ControlIdObjects.NameOf<AccessQrCode>());
        Assert.Equal("uhf_tags", ControlIdObjects.NameOf<AccessUhfTag>());
        Assert.Equal("pins", ControlIdObjects.NameOf<AccessPin>());
        Assert.Equal("contingency_cards", ControlIdObjects.NameOf<ContingencyCard>());
        Assert.Equal("alarm_zones", ControlIdObjects.NameOf<AlarmZone>());
        Assert.Equal("timed_alarms", ControlIdObjects.NameOf<TimedAlarm>());
        Assert.Equal("user_roles", ControlIdObjects.NameOf<UserRole>());
        Assert.Equal("scheduled_unlocks", ControlIdObjects.NameOf<ScheduledUnlock>());
        Assert.Equal("actions", ControlIdObjects.NameOf<ActionScript>());
        Assert.Equal("devices", ControlIdObjects.NameOf<AccessDevice>());
        Assert.Equal("catra_infos", ControlIdObjects.NameOf<TurnstileInfo>());
        Assert.Equal("log_types", ControlIdObjects.NameOf<LogType>());
        Assert.Equal("contacts", ControlIdObjects.NameOf<SipContact>());
        Assert.Equal("custom_thresholds", ControlIdObjects.NameOf<CustomThreshold>());
        Assert.Equal("network_interlocking_rules", ControlIdObjects.NameOf<NetworkInterlockingRule>());
        Assert.Equal("change_logs", ControlIdObjects.NameOf<ChangeLog>());
        Assert.Equal("access_log_access_rules", ControlIdObjects.NameOf<AccessLogAccessRule>());

        // Modelos que já existiam também passam a funcionar com o CRUD genérico.
        Assert.Equal("users", ControlIdObjects.NameOf<AccessUser>());
        Assert.Equal("sec_boxs", ControlIdObjects.NameOf<SecBox>());
        Assert.Equal("access_events", ControlIdObjects.NameOf<AccessDeviceEvent>());
    }

    [Fact]
    public async Task Generic_crud_uses_object_name_and_key()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"timed_alarms":[{"id":1,"name":"Almoço","time":43200,"sun":0,"mon":1,"tue":1,"wed":1,"thu":1,"fri":1,"sat":0}]}""")
            .OnJson("create_objects.fcgi", """{"ids":[2]}""")
            .OnJson("modify_objects.fcgi", """{"changes":1}""")
            .OnJson("destroy_objects.fcgi", """{"changes":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var alarm = Assert.Single(await client.GetObjectsAsync<TimedAlarm>(limit: 10));
        Assert.True(alarm.Monday);
        Assert.Equal(43200, alarm.Time);

        Assert.Equal(2, await client.CreateObjectAsync(TimedAlarm.Weekdays("Saída", TimeSpan.FromHours(18))));
        Assert.Equal(1, await client.UpdateObjectAsync(new AlarmZone { Zone = 3, Enabled = true, ActiveHigh = false, AlarmDelay = 5 }));
        Assert.True(await client.DeleteObjectAsync<ActionScript>(9));

        Assert.Equal("""{"object":"timed_alarms","limit":10}""", device.RequestsTo("load_objects.fcgi").Single().Body);
        Assert.Equal(
            """{"object":"timed_alarms","values":[{"name":"Saída","time":64800,"sun":0,"mon":1,"tue":1,"wed":1,"thu":1,"fri":1,"sat":0}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);
        // alarm_zones usa "zone" como chave.
        Assert.Equal(
            """{"object":"alarm_zones","values":{"zone":3,"enabled":1,"active_level":0,"alarm_delay":5},"where":{"alarm_zones":{"zone":3}}}""",
            device.RequestsTo("modify_objects.fcgi").Single().Body);
        // actions usa "group_id" como chave.
        Assert.Equal(
            """{"object":"actions","where":{"actions":{"group_id":9}}}""",
            device.RequestsTo("destroy_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task Generic_crud_rejects_invalid_uses_before_sending()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateObjectAsync(new AccessQrCode { Value = "x" })); // sem id
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeleteObjectAsync<AccessLogAccessRule>(1)); // associação
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetObjectsAsync<DeviceAction>()); // sem [ControlIdObject]
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Credentials_filter_by_user()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"qrcodes":[{"id":1,"value":"ABC","user_id":7}]}""")
            .OnJson("load_objects.fcgi", """{"uhf_tags":[{"id":2,"value":"CAFEDAD0","user_id":7}]}""")
            .OnJson("load_objects.fcgi", """{"pins":[{"id":3,"value":"1234","user_id":7}]}""")
            .OnJson("create_objects.fcgi", """{"ids":[10]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("ABC", Assert.Single(await client.GetQrCodesAsync(7)).Value);
        Assert.Equal("CAFEDAD0", Assert.Single(await client.GetUhfTagsAsync(7)).Value);
        Assert.Equal("1234", Assert.Single(await client.GetPinsAsync(7)).Value);
        Assert.Equal(10, await client.CreatePinAsync(new AccessPin { Value = "9999", UserId = 7 }));

        var bodies = device.RequestsTo("load_objects.fcgi").Select(r => r.Body).ToList();
        Assert.Equal("""{"object":"qrcodes","where":{"qrcodes":{"user_id":7}}}""", bodies[0]);
        Assert.Equal("""{"object":"uhf_tags","where":{"uhf_tags":{"user_id":7}}}""", bodies[1]);
        Assert.Equal("""{"object":"pins","where":{"pins":{"user_id":7}}}""", bodies[2]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetAdministrator_replaces_the_role(bool isAdministrator)
    {
        var device = LoggedDevice()
            .OnJson("destroy_objects.fcgi", """{"changes":0}""")
            .OnJson("create_objects.fcgi", """{"ids":[]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetAdministratorAsync(7, isAdministrator);

        Assert.Equal("""{"object":"user_roles","where":{"user_roles":{"user_id":7}}}""", device.RequestsTo("destroy_objects.fcgi").Single().Body);
        if (isAdministrator)
        {
            Assert.Equal("""{"object":"user_roles","values":[{"user_id":7,"role":1}]}""", device.RequestsTo("create_objects.fcgi").Single().Body);
        }
        else
        {
            Assert.Empty(device.RequestsTo("create_objects.fcgi"));
        }
    }

    [Fact]
    public async Task Change_logs_and_access_log_rules()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"change_logs":[{"id":5,"operation_type":"insert","table_name":"users","table_id":7,"timestamp":1700000000}]}""")
            .OnJson("load_objects.fcgi", """{"access_log_access_rules":[{"access_log_id":9,"access_rule_id":1},{"access_log_id":9,"access_rule_id":3}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var change = Assert.Single(await client.GetChangeLogsAsync(afterId: 4));
        Assert.Equal("users", change.TableName);
        Assert.Equal(new long[] { 1, 3 }, await client.GetAccessLogRuleIdsAsync(9));

        var bodies = device.RequestsTo("load_objects.fcgi").Select(r => r.Body).ToList();
        Assert.Equal("""{"object":"change_logs","where":{"change_logs":{"id":{">":4}}},"order":["ascending","id"]}""", bodies[0]);
        Assert.Equal("""{"object":"access_log_access_rules","where":{"access_log_access_rules":{"access_log_id":9}}}""", bodies[1]);
    }

    [Fact]
    public async Task New_links_including_single_field_contingency_rule()
    {
        var device = LoggedDevice().OnJson("create_objects.fcgi", """{"ids":[]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.LinkAsync(AccessLink.PortalAction(1, 2));
        await client.LinkAsync(AccessLink.ScheduledUnlockAccessRule(3, 4));
        await client.LinkAsync(AccessLink.AlarmZoneTimeZone(5, 6));
        await client.LinkAsync(AccessLink.ContingencyCardAccessRule(1));

        Assert.Equal(
            new[]
            {
                """{"object":"portal_actions","values":[{"portal_id":1,"action_id":2}]}""",
                """{"object":"scheduled_unlock_access_rules","values":[{"scheduled_unlock_id":3,"access_rule_id":4}]}""",
                """{"object":"alarm_zone_time_zones","values":[{"alarm_zone_id":5,"time_zone_id":6}]}""",
                """{"object":"contingency_card_access_rules","values":[{"access_rule_id":1}]}""",
            },
            device.RequestsTo("create_objects.fcgi").Select(r => r.Body));
    }

    [Fact]
    public void User_reads_image_timestamp_and_last_access()
    {
        var user = JsonSerializer.Deserialize<AccessUser>("""{"id":1,"image_timestamp":1700000000,"last_access":1700000500}""")!;

        Assert.Equal(1700000000, user.ImageTimestamp);
        Assert.Equal(1700000500, user.LastAccess);
    }

    [Fact]
    public void TimedAlarm_rejects_time_outside_the_day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimedAlarm.Weekdays("x", TimeSpan.FromHours(24)));
    }
}
