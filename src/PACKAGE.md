# ControlId.Net

An unofficial, fully typed .NET SDK for [Control iD](https://www.controlid.com.br) hardware: the access control line
(iDAccess, iDFlex, iDFace, iDBox, iDUHF) and the REP iDClass time clocks, with an AFD parser for Portaria 671/2021.

SDK .NET não oficial e tipado para equipamentos Control iD: linha de acesso e REP iDClass, com parser de AFD da
Portaria 671/2021. [Guia completo em português](https://github.com/DaviLuisSantos/ControlId.Net/blob/main/docs/guia.md).

## Packages

| Package | Purpose |
| --- | --- |
| `ControlId.Net` | `AccessClient`, `RepClient`, AFD parser, Push and Monitor models |
| `ControlId.Net.DependencyInjection` | `AddControlIdAccess` / `AddControlIdRep`, many devices from `appsettings.json` |
| `ControlId.Net.AspNetCore` | Push, Monitor and Online mode endpoints for ASP.NET Core (net8.0+), closed by default (Bearer token) |
| `ControlId.Net.Push.EntityFrameworkCore` | Durable Push command queue on EF Core (net10.0) |

## Quick start

```csharp
using ControlId.Access;

using var device = new AccessClient("http://192.168.0.129", "admin", "admin");

var id = await device.CreateUserAsync(new AccessUser { Name = "Maria", Registration = "1020" });
await device.OpenDoorAsync(door: 1);

var logs = await device.GetAccessLogsAsync(afterId: 0, limit: 100);
```

```csharp
using ControlId.Rep;
using ControlId.Rep.Afd;

// TLS is validated: pin the fingerprint of the self-signed certificate
// (read it once with ControlIdCertificates.GetServerCertificateSha256Async).
using var rep = new RepClient("https://192.168.0.130", mode: RepMode.Portaria671,
    serverCertificateSha256: "AB:CD:EF:...");

AfdFile afd = await rep.GetAfdFileFromNsrAsync(1);
foreach (var punch in afd.Punches)
{
    Console.WriteLine($"{punch.Nsr} {punch.Time:yyyy-MM-dd HH:mm} {punch.EmployeeId}");
}
```

```csharp
// ASP.NET Core: callbacks answer 401 without a token; write the same token to the device with ConfigureCallbackSecurityAsync.
builder.Services.AddControlIdMonitor<AccessLogHandler>();
app.MapControlIdMonitor().RequireControlIdToken(callbackToken);
```

## Links

- [Project page, architecture and roadmap](https://github.com/DaviLuisSantos/ControlId.Net)
- [Usage guide (Portuguese)](https://github.com/DaviLuisSantos/ControlId.Net/blob/main/docs/guia.md)
- [API coverage matrix](https://github.com/DaviLuisSantos/ControlId.Net/blob/main/docs/COBERTURA.md)
- [Changelog, including breaking changes and how to migrate](https://github.com/DaviLuisSantos/ControlId.Net/blob/main/CHANGELOG.md)
- [Security notes](https://github.com/DaviLuisSantos/ControlId.Net/blob/main/SECURITY.md)

Independent project, not affiliated with Control iD. "Control iD", "iDAccess", "iDFlex", "iDFace" and "iDClass" are
trademarks of Control iD. Licensed under MIT.
