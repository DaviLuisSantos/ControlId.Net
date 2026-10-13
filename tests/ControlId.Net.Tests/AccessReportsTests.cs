using System.Text;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Monitoring;

namespace ControlId.Tests;

public class AccessReportsTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public void AccessLog_reads_documented_fields()
    {
        // Valores do exemplo de access_logs da documentação do Monitor, mais os campos da Lista de Objetos.
        var log = MonitorNotifications.Parse("dao", """
            {"object_changes":[{"object":"access_logs","type":"inserted","values":{
              "id":"519","time":"1532977090","event":"12","device_id":"478435","identifier_id":"0","user_id":"0",
              "portal_id":"1","identification_rule_id":"3","card_value":"0","log_type_id":"-1",
              "qrcode_value":"ABC","uhf_tag":"E200","pin_value":"1234","confidence":"1500","mask":"1"}}],"device_id":478435}
            """) is DaoNotification dao ? Assert.Single(dao.GetInsertedAccessLogs()) : throw new InvalidOperationException();

        Assert.Equal(3, log.IdentificationRuleId);
        Assert.Equal("ABC", log.QrCodeValue);
        Assert.Equal("E200", log.UhfTag);
        Assert.Equal("1234", log.PinValue);
        Assert.Equal(1500, log.Confidence);
        Assert.Equal(1, log.Mask);
        Assert.Equal(-1, log.LogTypeId);
        Assert.Equal(AccessEventType.WebInterface, log.EventType);
    }

    [Fact]
    public async Task Areas_and_area_rule_link()
    {
        var device = LoggedDevice()
            .OnJson("create_objects.fcgi", """{"ids":[4]}""")
            .OnJson("load_objects.fcgi", """{"areas":[{"id":4,"name":"Garagem"}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(4, await client.CreateAreaAsync(new AccessArea { Name = "Garagem" }));
        Assert.Equal("Garagem", Assert.Single(await client.GetAreasAsync()).Name);
        await client.LinkAsync(AccessLink.AreaAccessRule(4, 2));

        Assert.Equal(
            """{"object":"area_access_rules","values":[{"area_id":4,"access_rule_id":2}]}""",
            device.RequestsTo("create_objects.fcgi").Last().Body);
    }

    [Fact]
    public async Task Holidays_use_0_1_flags_and_full_day_factory()
    {
        var device = LoggedDevice()
            .OnJson("create_objects.fcgi", """{"ids":[1]}""")
            .OnJson("load_objects.fcgi", """{"holidays":[{"id":1,"name":"Natal","start":1798167600,"end":1798254000,"hol1":1,"hol2":0,"hol3":0,"repeats":1}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var holiday = AccessHoliday.FullDay("Natal", new DateTime(2026, 12, 25), TimeSpan.FromHours(-3), repeats: true);
        await client.CreateHolidayAsync(holiday);
        var read = Assert.Single(await client.GetHolidaysAsync());

        // 25/12/2026 00:00 -03:00 = 1798167600
        Assert.Equal(
            """{"object":"holidays","values":[{"name":"Natal","start":1798167600,"end":1798254000,"hol1":1,"hol2":0,"hol3":0,"repeats":1}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);
        Assert.True(read.Group1);
        Assert.False(read.Group2);
        Assert.True(read.Repeats);
    }

    [Fact]
    public async Task Access_events_and_alarm_logs_are_incremental()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"access_events":[{"id":8,"event":"door","type":"OPEN","identification":"1","device_id":9,"timestamp":1700000000}]}""")
            .OnJson("load_objects.fcgi", """{"alarm_logs":[{"id":3,"event":1,"cause":6,"user_id":0,"time":1700000000,"access_log_id":5,"door_id":1}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var events = await client.GetAccessEventsAsync(afterId: 7, limit: 100);
        var alarms = await client.GetAlarmLogsAsync(afterId: 2);

        var e = Assert.Single(events);
        Assert.Equal("door", e.Event);
        Assert.Equal("OPEN", e.Type);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), e.TimestampUtc);
        Assert.Equal(6, Assert.Single(alarms).Cause);

        var bodies = device.RequestsTo("load_objects.fcgi").Select(r => r.Body).ToList();
        Assert.Equal("""{"object":"access_events","where":{"access_events":{"id":{">":7}}},"order":["ascending","id"],"limit":100}""", bodies[0]);
        Assert.Equal("""{"object":"alarm_logs","where":{"alarm_logs":{"id":{">":2}}},"order":["ascending","id"]}""", bodies[1]);
    }

    [Fact]
    public async Task SecBox_reads_and_writes_flags_as_0_1()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"sec_boxs":[{"id":65793,"version":2,"name":"MAE","enabled":1,"relay_timeout":3000,"door_sensor_enabled":0,"door_sensor_idle":1,"auto_close_enabled":0}]}""")
            .OnJson("modify_objects.fcgi", """{"changes":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var box = (await client.GetSecBoxAsync())!;
        Assert.True(box.Enabled);
        Assert.True(box.DoorSensorNormallyOpen);

        box.RelayTimeoutMilliseconds = 5000;
        box.AutoCloseEnabled = true;
        await client.UpdateSecBoxAsync(box);

        // A documentação declara enabled e door_sensor_* como bool, mas o iDFace recusa: "integer expected".
        Assert.Equal(
            """{"object":"sec_boxs","values":{"id":65793,"version":2,"name":"MAE","enabled":1,"relay_timeout":5000,"door_sensor_enabled":0,"door_sensor_idle":1,"auto_close_enabled":1},"where":{"sec_boxs":{"id":65793}}}""",
            device.RequestsTo("modify_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task SecBox_absent_returns_null()
    {
        using var client = new AccessClient(Options, LoggedDevice().OnJson("load_objects.fcgi", """{"sec_boxs":[]}""").CreateClient());

        Assert.Null(await client.GetSecBoxAsync());
    }

    [Fact]
    public async Task Report_body_matches_documentation_example_and_text_is_decoded()
    {
        var csv = "Nome (Usuário);Código (Usuário)\r\nJosé;00001\r\n";
        var device = LoggedDevice().OnBytes("report_generate.fcgi", Encoding.GetEncoding("iso-8859-1").GetBytes(csv), "text/plain");
        using var client = new AccessClient(Options, device.CreateClient());

        var report = await client.GenerateReportAsync(new ReportRequest("users")
        {
            Order = new[] { "ascending", "name" },
            Delimiter = ";",
            LineBreak = "\r\n",
            Header = "Nome (Usuário);Código (Usuário)",
            Join = "LEFT",
            Columns =
            {
                ReportColumn.ObjectField("users", "name", new ReportColumnFormat { Alignment = ReportAlignment.Left, Width = 50, Fill = " ", Format = "%s" }),
                ReportColumn.ObjectField("users", "id", new ReportColumnFormat { Alignment = ReportAlignment.Right, Width = 5, Fill = "0", Format = "%s" }),
                ReportColumn.Counter(offset: 1),
                new ReportColumn { Type = "fixed", AdditionalFields = new Dictionary<string, object> { ["value"] = "X" } },
            },
        });

        Assert.Equal(csv, report);
        Assert.Equal(
            """{"object":"users","columns":[{"type":"object_field","object":"users","field":"name","format":{"adjustment":1,"fill":" ","format":"%s","width":50}},{"type":"object_field","object":"users","field":"id","format":{"adjustment":2,"fill":"0","format":"%s","width":5}},{"type":"counter","offset":1},{"type":"fixed","value":"X"}],"delimiter":";","header":"Nome (Usuário);Código (Usuário)","line_break":"\r\n","order":["ascending","name"],"join":"LEFT"}""",
            device.RequestsTo("report_generate.fcgi").Single().Body);
    }

    [Fact]
    public async Task Export_afd_filters_and_parses()
    {
        var afdLine = "000000003" + "3" + "01102026" + "0800" + "012345678901";
        var device = LoggedDevice().OnJson("export_afd.fcgi", afdLine + "\r\n");
        using var client = new AccessClient(Options, device.CreateClient());

        var afd = await client.ExportAfdFileAsync(initialDate: new DateTime(2022, 7, 11), portaria671: false);
        await client.ExportAfdAsync(initialNsr: 1);

        Assert.Equal("12345678901", Assert.Single(afd.Punches).EmployeeId);
        var requests = device.RequestsTo("export_afd.fcgi").ToList();
        Assert.Equal("""{"initial_date":{"day":11,"month":7,"year":2022}}""", requests[0].Body);
        Assert.Equal("?session=s", requests[0].Uri.Query);
        Assert.Equal("""{"initial_nsr":1}""", requests[1].Body);
        Assert.Equal("?session=s&mode=671", requests[1].Uri.Query);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExportAfdAsync(initialNsr: 1, initialDate: DateTime.Today));
    }

    [Fact]
    public async Task Audit_logs_send_one_flag_per_category()
    {
        var device = LoggedDevice().OnJson("export_audit_logs.fcgi", "2026-10-10 config changed");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("2026-10-10 config changed", await client.ExportAuditLogsAsync(AuditLogCategories.Config | AuditLogCategories.Online));
        Assert.Equal(
            """{"config":1,"api":0,"usb":0,"network":0,"time":0,"online":1,"menu":0}""",
            device.RequestsTo("export_audit_logs.fcgi").Single().Body);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ExportAuditLogsAsync(AuditLogCategories.None));
    }
}
