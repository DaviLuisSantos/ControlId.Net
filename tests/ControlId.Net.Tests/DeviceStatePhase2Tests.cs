using System.Net;
using ControlId.Access;
using ControlId.Access.Online;

namespace ControlId.Tests;

public class DeviceStatePhase2Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Door_states_for_relays_and_secboxes()
    {
        var device = LoggedDevice()
            .OnJson("door_state.fcgi", """{"doors":[{"id":1,"open":false},{"id":2,"open":true}]}""")
            .OnJson("door_state.fcgi", """{"sec_boxes":[{"id":122641794705017745,"open":false},{"id":65793,"open":true}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var relays = await client.GetDoorStatesAsync();
        var secBoxes = await client.GetDoorStatesAsync();

        Assert.True(relays.Doors[1].Open);
        Assert.Empty(relays.SecBoxes);
        Assert.Equal(122641794705017745, secBoxes.SecBoxes[0].Id);
        Assert.True(secBoxes.SecBoxes[1].Open);
    }

    [Fact]
    public async Task Door_state_error_without_relay_is_surfaced()
    {
        var device = LoggedDevice().On("door_state.fcgi", HttpStatusCode.BadRequest, """{"error":"Sec Box count message parameter is invalid","code":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.GetDoorStatesAsync());
        Assert.Contains("Sec Box count", ex.Message);
    }

    [Fact]
    public async Task Alarm_status_does_not_stop_and_stop_sends_true()
    {
        var device = LoggedDevice()
            .OnJson("alarm_status.fcgi", """{"active":true,"cause":7}""")
            .OnJson("alarm_status.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.GetAlarmStatusAsync();
        await client.StopAlarmAsync();

        Assert.True(status.Active);
        Assert.Equal(AlarmCause.DoorForced, status.CauseType);
        Assert.Equal(new[] { """{"stop":false}""", """{"stop":true}""" }, device.RequestsTo("alarm_status.fcgi").Select(r => r.Body));
    }

    [Fact]
    public void Alarm_log_cause_uses_the_same_numbering()
    {
        Assert.Equal(AlarmCause.PanicCard, new AlarmLog { Cause = 10 }.CauseType);
    }

    [Fact]
    public async Task Reboot_without_session_is_a_plain_get()
    {
        var device = new FakeDeviceHandler().OnJson("reboot", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.RebootWithoutSessionAsync();

        Assert.Empty(device.RequestsTo("login.fcgi"));
        var request = device.RequestsTo("reboot").Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(string.Empty, request.Uri.Query);
    }

    [Fact]
    public async Task Turnstile_led_matches_documentation()
    {
        var device = LoggedDevice().OnJson("remote_led_control.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetTurnstileLedAsync(TurnstileLedEvent.Idle);
        await client.SetTurnstileLedAsync(TurnstileLedEvent.SetIdleColor, 0xFF00FF00);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetTurnstileLedAsync(TurnstileLedEvent.SetIdleColor));

        Assert.Equal(
            new[]
            {
                """{"sec_box":{"color":"0","event":2}}""",
                """{"sec_box":{"color":"4278255360","event":4}}""", // exemplo da documentação: verde = 4278255360
            },
            device.RequestsTo("remote_led_control.fcgi").Select(r => r.Body));
    }

    [Fact]
    public async Task Execute_actions_with_interlock_status()
    {
        var device = LoggedDevice()
            .OnJson("execute_actions.fcgi", """{"actions":[{"action":"sec_box","status":"denied"}]}""")
            .OnJson("execute_actions.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        var denied = Assert.Single(await client.ExecuteActionsWithStatusAsync(new[] { DeviceAction.OpenSecBox() }));
        var withoutInterlock = await client.ExecuteActionsWithStatusAsync(new[] { DeviceAction.OpenTurnstileCollector(), DeviceAction.ActivateTurnstileRelay(2) });

        Assert.False(denied.Allowed);
        Assert.Empty(withoutInterlock);
        Assert.Equal(
            """{"actions":[{"action":"open_collector","parameters":""},{"action":"catra","parameters":"relay=2"}]}""",
            device.RequestsTo("execute_actions.fcgi").Last().Body);
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceAction.ActivateTurnstileRelay(3));
    }

    [Fact]
    public void Online_event_decodes_idblock_next_component()
    {
        // "win0" = 0x77696E00 = 2003398144, mesma codificação do identifier_id.
        var e = OnlineEvents.Parse("new_user_identified", new[] { new KeyValuePair<string, string>("component_id", "2003398144") })!;

        Assert.Equal(2003398144, e.ComponentId);
        Assert.Equal("win0", e.ComponentName);
    }
}
