using System;
using System.Text.Json;
using ControlId.Internal;

namespace ControlId.Access.Monitoring;

/// <summary>
/// Interpreta as notificações do Monitor. O servidor HTTP é seu (ASP.NET Core, HttpListener...):
/// receba o <c>POST</c> em <c>{path}/{endpoint}</c>, passe a rota e o corpo para <see cref="Parse"/> e responda 200.
/// </summary>
public static class MonitorNotifications
{
    /// <summary>
    /// Converte a notificação para o tipo concreto conforme a rota
    /// (aceita <c>dao</c>, <c>/api/notifications/dao</c> ou a URL inteira).
    /// </summary>
    /// <exception cref="ControlIdException">O corpo não é um JSON válido.</exception>
    public static MonitorNotification Parse(string endpoint, string json)
    {
        if (endpoint is null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        var name = LastSegment(endpoint);
        if (string.IsNullOrWhiteSpace(json))
        {
            json = "{}";
        }

        try
        {
            MonitorNotification notification = name switch
            {
                "dao" => ControlIdJson.Deserialize<DaoNotification>(json),
                "door" => ControlIdJson.Deserialize<DoorNotification>(json),
                "secbox" => ControlIdJson.Deserialize<SecBoxNotification>(json),
                "catra_event" => ControlIdJson.Deserialize<TurnstileNotification>(json),
                "operation_mode" => ControlIdJson.Deserialize<OperationModeNotification>(json),
                "device_is_alive" => ControlIdJson.Deserialize<DeviceIsAliveNotification>(json),
                "access_photo" => ControlIdJson.Deserialize<AccessPhotoNotification>(json),
                "usb_drive" => ControlIdJson.Deserialize<UsbDriveNotification>(json),
                "card" => RemoteEnroll(json, RemoteEnrollType.Card),
                "user_image" or "face_template" => RemoteEnroll(json, RemoteEnrollType.Face),
                "template" => RemoteEnroll(json, RemoteEnrollType.Biometry),
                "pin" => RemoteEnroll(json, RemoteEnrollType.Pin),
                "password" => RemoteEnroll(json, RemoteEnrollType.Password),
                _ => ControlIdJson.Deserialize<UnknownMonitorNotification>(json),
            };

            using var document = JsonDocument.Parse(json);
            notification.Raw = document.RootElement.Clone();
            notification.Endpoint = name;
            return notification;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            throw new ControlIdException($"Notificação '{name}' inválida.", responseBody: json, innerException: ex);
        }
    }

    private static RemoteEnrollNotification RemoteEnroll(string json, RemoteEnrollType type)
    {
        var notification = ControlIdJson.Deserialize<RemoteEnrollNotification>(json);
        notification.EnrollType = type;
        notification.Result = RemoteEnrollResult.Parse(json);
        return notification;
    }

    private static string LastSegment(string endpoint)
    {
        var path = endpoint;
        var query = path.IndexOf('?');
        if (query >= 0)
        {
            path = path.Substring(0, query);
        }

        path = path.TrimEnd('/');
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }
}
