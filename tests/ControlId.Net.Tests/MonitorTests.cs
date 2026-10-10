using ControlId.Access;
using ControlId.Access.Monitoring;

namespace ControlId.Tests;

public class MonitorTests
{
    [Fact]
    public void Dao_reads_access_log_sent_as_strings()
    {
        // Exemplo da documentação: os números chegam como texto.
        const string json = """
            {
              "object_changes": [
                {
                  "object": "access_logs",
                  "type": "inserted",
                  "values": {
                    "id": "519", "time": "1532977090", "event": "12", "device_id": "478435",
                    "identifier_id": "0", "user_id": "0", "portal_id": "1",
                    "identification_rule_id": "0", "card_value": "0", "log_type_id": "-1"
                  }
                },
                { "object": "cards", "type": "deleted", "values": { "id": "3" } }
              ],
              "device_id": 478435
            }
            """;

        var dao = Assert.IsType<DaoNotification>(MonitorNotifications.Parse("/api/notifications/dao", json));

        Assert.Equal("dao", dao.Endpoint);
        Assert.Equal(478435, dao.DeviceId);
        Assert.Equal(2, dao.ObjectChanges.Count);
        Assert.Equal(ObjectChangeType.Deleted, dao.ObjectChanges[1].ChangeType);

        var log = Assert.Single(dao.GetInsertedAccessLogs());
        Assert.Equal(519, log.Id);
        Assert.Equal(12, log.Event);
        Assert.Equal(1, log.PortalId);
    }

    [Fact]
    public void Door_notification()
    {
        var door = Assert.IsType<DoorNotification>(MonitorNotifications.Parse(
            "door",
            """{"door":{"id":1,"open":true},"access_event_id":15,"device_id":1038508,"time":1575475894}"""));

        Assert.Equal(1, door.Door!.Id);
        Assert.True(door.Door.Open);
        Assert.Equal(15, door.AccessEventId);
        Assert.Equal(1575475894, door.Time);
    }

    [Fact]
    public void SecBox_notification_without_device_id()
    {
        var secBox = Assert.IsType<SecBoxNotification>(MonitorNotifications.Parse(
            "http://server:8000/api/notifications/secbox?x=1",
            """{"secbox":{"id":122641794705028910,"open":false},"access_event_id":15}"""));

        Assert.Equal(122641794705028910, secBox.SecBox!.Id);
        Assert.False(secBox.SecBox.Open);
        Assert.Null(secBox.DeviceId);
    }

    [Fact]
    public void Turnstile_operation_mode_and_alive()
    {
        var catra = Assert.IsType<TurnstileNotification>(MonitorNotifications.Parse(
            "catra_event",
            """{"event":{"type":7,"name":"TURN LEFT","time":1484126902,"uuid":"0e039178"},"device_id":935107,"time":1484126902}"""));
        Assert.Equal("TURN LEFT", catra.Event!.Name);

        var mode = Assert.IsType<OperationModeNotification>(MonitorNotifications.Parse(
            "operation_mode",
            """{"operation_mode":{"mode":0,"mode_name":"DEFAULT","time":1490271121,"last_offline":0,"exception_mode":"none"},"device_id":123456}"""));
        Assert.Equal("DEFAULT", mode.OperationMode!.ModeName);
        Assert.Equal(0, mode.OperationMode.LastOffline);

        var alive = Assert.IsType<DeviceIsAliveNotification>(MonitorNotifications.Parse(
            "device_is_alive",
            """{"access_logs":42,"device_id":6613047045004349,"time":1739376235}"""));
        Assert.Equal(42, alive.AccessLogs);
        Assert.Equal(6613047045004349, alive.DeviceId);
    }

    [Fact]
    public void Access_photo_decodes_base64()
    {
        var photo = Assert.IsType<AccessPhotoNotification>(MonitorNotifications.Parse(
            "access_photo",
            """{"device_id":"478435","time":"1532977090","portal_id":"1","identifier_id":"0","event":"7","user_id":"9","access_photo":"/9j/4A=="}"""));

        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, photo.AccessPhoto);
        Assert.Equal(9, photo.UserId);
        Assert.Equal(7, photo.Event);
    }

    [Fact]
    public void Unknown_endpoint_keeps_raw_json()
    {
        var unknown = Assert.IsType<UnknownMonitorNotification>(MonitorNotifications.Parse("future_event", """{"value":123,"device_id":1}"""));

        Assert.Equal("future_event", unknown.Endpoint);
        Assert.Equal(1, unknown.DeviceId);
        Assert.Equal(123, unknown.Raw.GetProperty("value").GetInt64());
    }

    [Fact]
    public void Invalid_json_throws_controlid_exception()
    {
        Assert.Throws<ControlIdException>(() => MonitorNotifications.Parse("door", "{not json"));
    }
}
