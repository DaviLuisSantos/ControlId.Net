# ControlId.Net

Cliente .NET **não oficial** para equipamentos [Control iD](https://www.controlid.com.br):

- **Linha de acesso**: iDAccess, iDFlex, iDFace, iDBox, iDUHF (API `*.fcgi` sobre HTTP).
- **Relógios de ponto REP iDClass**: API HTTPS, incluindo AFD no layout da Portaria 671.

Alvos: `net8.0` e `netstandard2.0`, então também roda em projetos .NET Framework 4.6.1+.

> Projeto independente, sem vínculo com a Control iD. "Control iD", "iDAccess", "iDFlex", "iDFace" e "iDClass" são marcas da Control iD.

## Instalação

```bash
dotnet add package ControlId.Net --prerelease
```

Pacotes complementares:

| Pacote | Para quê |
| --- | --- |
| `ControlId.Net.DependencyInjection` | `AddControlIdAccess` / `AddControlIdRep` no `IServiceCollection`, com vários equipamentos e `appsettings.json` |
| `ControlId.Net.AspNetCore` | Rotas de Push e Monitor no ASP.NET Core (net8.0+); inclui o pacote de DI |

## Linha de acesso

```csharp
using ControlId.Access;

using var device = new AccessClient("http://192.168.0.129", "admin", "admin");

// O login é feito sozinho na primeira chamada e refeito se a sessão expirar.
var users = await device.GetUsersAsync();

var id = await device.CreateUserAsync(new AccessUser { Name = "Maria", Registration = "1020" });

await device.OpenDoorAsync(door: 1);
await device.ReleaseTurnstileAsync(TurnstileDirection.Clockwise);

var logs = await device.GetAccessLogsAsync(afterId: 0, limit: 100);
```

Qualquer objeto da API (`groups`, `access_rules`, `cards`, `templates`...) pode ser lido e escrito pelos métodos genéricos:

```csharp
var cards = await device.LoadObjectsAsync<AccessCard>(new LoadObjectsRequest("cards")
{
    Where = Where.Equal("cards", "user_id", id),
});

await device.ModifyObjectsAsync("users", new { name = "Maria Silva" }, Where.Equal("users", "id", id));
await device.DestroyObjectsAsync("cards", Where.In("cards", "id", new[] { 1L, 2L }));
```

### Cartões, digitais e fotos

```csharp
await device.CreateCardAsync(new AccessCard { UserId = id, Value = 123456789 });
var digitais = await device.GetTemplatesAsync(userId: id);

await device.SetUserImageAsync(id, File.ReadAllBytes("maria.jpg")); // JPEG < 1 MB
byte[] foto = await device.GetUserImageAsync(id);
```

### Cadastro remoto (cartão, face, digital, PIN, senha)

O equipamento pede a credencial à pessoa e devolve o resultado:

```csharp
// Síncrono: a chamada espera a pessoa concluir.
var resultado = await device.RemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Face)
{
    UserId = id,
    Save = true,                         // grava no usuário; false só captura e devolve
    Auto = true,
    Countdown = TimeSpan.FromSeconds(3),
});

if (!resultado.Success)
{
    Console.WriteLine(resultado.Error);  // ex.: FACE_EXISTS (resultado.MatchUserId diz de quem é a face)
}
```

- A espera síncrona é limitada pelo `Timeout` do cliente (30 s por padrão). Se ele estourar ou o `CancellationToken` for cancelado, o cadastro é cancelado no equipamento (`cancel_remote_enroll`), para ele não ficar preso na tela de cadastro.
- Para esperas longas, use `StartRemoteEnrollAsync(...)`: a chamada volta na hora e o resultado chega pelo Monitor como `RemoteEnrollNotification` (com `EnrollType` e o mesmo `RemoteEnrollResult`).
- Com o equipamento em **modo online**, o resultado assíncrono não vai pelo Monitor: chega em `card_create`, `face_create`, `fingerprint_create`, `template_create`, `pin_create` ou `password_create`, que o `MapControlIdOnline` entrega em `IOnlineAccessHandler.OnRemoteEnrollAsync`. Nesse caso a foto da face vem em `Result.Face`.
- Via Push: `PushCommand.RemoteEnroll(request)` e `result.ResponseAs<RemoteEnrollResult>()`.
- Falhas do cadastro (timeout da pessoa, face repetida) vêm em `Error`, sem exceção; use `EnsureSuccess()` se preferir exceção.

### Grupos, horários e regras de acesso

```csharp
var grupo = await device.CreateGroupAsync(new AccessGroup { Name = "Administrativo" });
var horario = await device.CreateTimeZoneAsync(new AccessTimeZone { Name = "Comercial" });
await device.CreateTimeSpanAsync(AccessTimeSpan.Weekdays(horario, TimeSpan.FromHours(8), TimeSpan.FromHours(18)));
var regra = await device.CreateAccessRuleAsync(new AccessRule { Name = "Comercial", Type = AccessRuleType.Allow, Priority = 0 });

await device.LinkAsync(AccessLink.UserGroup(id, grupo));
await device.LinkAsync(AccessLink.GroupAccessRule(grupo, regra));
await device.LinkAsync(AccessLink.AccessRuleTimeZone(regra, horario));
await device.LinkAsync(AccessLink.PortalAccessRule(portalId: 1, regra));
```

### Comandos do equipamento

```csharp
await device.ShowMessageAsync("Bem-vindo!", TimeSpan.FromSeconds(5));   // sem duração: fica até ser trocada
await device.BuzzAsync(TimeSpan.FromMilliseconds(500));                 // até 3 s por chamada

await device.SetLogoAsync(File.ReadAllBytes("logo.png"), slot: 1);
await device.ShowLogoAsync(1);

var sensor = await device.GetGpioStateAsync(5);                          // iDAccess: sensor de porta 1
var hash = await device.HashPasswordAsync("1234");                       // para AccessUser.Password/Salt

await device.ChangeLoginAsync("operador", "nova-senha");                 // o cliente passa a usar as novas credenciais
await device.SetNetworkAsync(new NetworkSettings("192.168.0.33", "255.255.255.0", "192.168.0.1"));
await device.RebootAsync();
```

| Método | Endpoint |
| --- | --- |
| `ShowMessageAsync` / `ClearMessageAsync` | `message_to_screen` |
| `BuzzAsync` | `buzzer_buzz` |
| `ChangeLoginAsync` | `change_login` |
| `HashPasswordAsync` | `user_hash_password` |
| `RebootAsync` / `RebootToUpdateModeAsync` | `reboot` / `reboot_recovery` |
| `ResetToFactoryDefaultAsync` (apaga tudo) | `reset_to_factory_default` |
| `DeleteAllAdministratorsAsync` | `delete_admins` |
| `SetNetworkAsync` | `set_system_network` |
| `GetLogoAsync` / `SetLogoAsync` / `DeleteLogoAsync` / `ShowLogoAsync` | `logo`, `logo_change`, `logo_destroy`, `general.show_logo` |
| `GetGpioStateAsync` | `gpio_state` |
| `RefreshLedAsync` | `led_rgb_refresh` |
| `SetUsbPortEnabledAsync` | `general.usb_port_enabled` |
| `CreateOrModifyObjectsAsync` | `create_or_modify_objects` |

Via Push: `PushCommand.ShowMessage`, `PushCommand.Buzz` e `PushCommand.Reboot`.

### Configurações

```csharp
var monitor = await device.GetConfigurationAsync("monitor", new[] { "hostname", "port" });
Console.WriteLine(monitor["hostname"]);

await device.SetConfigurationAsync(new { general = new { beep_enabled = "1", relay1_timeout = "3000" } }); // valores sempre em texto
```

### Endpoints sem método próprio

A API tem muitos comandos que ainda não têm método tipado (cadastro remoto, mensagem na tela, rede, logotipo,
reinício...). Todos podem ser chamados pelos métodos genéricos, que valem para `AccessClient` e `RepClient`
(interface `IControlIdClient`) e mantêm a sessão e o re-login automáticos:

```csharp
await device.SendRawAsync("message_to_screen", new { message = "Bem-vindo!", timeout = 5000 });

var resposta = await device.SendAsync<JsonElement>("endpoint", corpo, queryString: "user_id=7");
await device.SendBytesAsync("endpoint", bytes, "application/octet-stream", "id=1");   // corpo binário
byte[] arquivo = await device.DownloadAsync("endpoint", method: HttpMethod.Get);     // resposta binária
```

O endpoint pode vir com ou sem `.fcgi`. Consulte o corpo de cada comando na
[documentação da Control iD](https://www.controlid.com.br/docs/access-api-pt/). No REP, o `mode=671` não é
acrescentado automaticamente nessas chamadas: passe-o em `queryString` quando o comando exigir.

### Monitor e Push (tempo real)

Nos dois modos é o **equipamento que chama o seu servidor**. A biblioteca não sobe servidor HTTP:
ela interpreta as notificações (Monitor) e mantém a fila de comandos (Push), e você liga isso nas rotas
do framework que já usa.

- **Monitor**: o equipamento faz `POST` em `{path}/{endpoint}` a cada evento (log de acesso, porta, catraca, modo de operação...).
- **Push**: o equipamento consulta `GET /push` periodicamente, executa o comando recebido e devolve o resultado em `POST /result`. Serve para equipamentos atrás de NAT, sem IP acessível.

```csharp
// Uma vez, pelo cliente HTTP normal:
await device.ConfigureMonitorAsync(new MonitorConfiguration { Hostname = "192.168.0.20", Port = 5000, Path = "api/notifications" });
await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = "192.168.0.20:5000", RequestPeriod = TimeSpan.FromSeconds(5) });
```

Com ASP.NET Core, o pacote `ControlId.Net.AspNetCore` monta as rotas:

```bash
dotnet add package ControlId.Net.AspNetCore --prerelease
```

```csharp
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;

builder.Services.AddControlIdPush();                      // PushServer singleton
builder.Services.AddControlIdMonitor<MeuHandler>();       // ou AddControlIdMonitor((notificacao, ct) => ...)

app.MapControlIdPush();                                   // GET /push e POST /result
app.MapControlIdMonitor();                                // POST /api/notifications/{endpoint}

public sealed class MeuHandler : IMonitorNotificationHandler   // scoped: pode receber DbContext etc.
{
    public Task HandleAsync(MonitorNotification notificacao, CancellationToken ct)
    {
        if (notificacao is DaoNotification dao)
        {
            foreach (var log in dao.GetInsertedAccessLogs())
            {
                Console.WriteLine($"Acesso: usuário {log.UserId}, evento {log.Event}");
            }
        }

        return Task.CompletedTask;
    }
}
```

Os dois `Map...` devolvem o `RouteGroupBuilder`, então dá para encadear `RequireHost`, filtros etc.
O equipamento espera a resposta HTTP do Monitor: mantenha o handler curto ou jogue o trabalho numa fila.

Em qualquer lugar da aplicação, comande o equipamento pelo `PushServer` injetado. Cada chamada espera o
equipamento buscar o comando e responder:

```csharp
using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await push.Device(deviceId).OpenDoorAsync(door: 1, prazo.Token);
var usuarios = await push.Device(deviceId).LoadObjectsAsync<AccessUser>(new LoadObjectsRequest("users"), prazo.Token);

// Vários comandos numa única consulta do equipamento (objeto "transactions"):
var resultados = await push.Device(deviceId).SendBatchAsync(new[]
{
    PushCommand.SetConfiguration(new { general = new { beep_enabled = "0" } }),
    PushCommand.ExecuteActions(new DeviceAction("door", "door=1")),
}, prazo.Token);
// Um PushResult por comando, na mesma ordem; a falha de um não cancela os outros.
```

Qualquer endpoint da API pode ir via Push com `new PushCommand("endpoint", body)`.
Sempre passe um `CancellationToken` com prazo: se o equipamento estiver offline, o comando fica na fila até ele aparecer.
`push.GetLastSeen(deviceId)` informa a última consulta de cada equipamento.

Sem ASP.NET Core, use `MonitorNotifications.Parse(rota, corpo)`, `PushServer.HandlePush(deviceId, uuid)` e
`PushServer.HandleResult(deviceId, uuid, corpo)` nas rotas do seu servidor HTTP.
O sample `samples/ControlId.Net.Server` tem um servidor completo.

### Modo online (Pro e Enterprise)

No modo online o equipamento pergunta ao **seu servidor**, a cada tentativa de acesso, se deve liberar:

- **Pro**: o equipamento identifica a pessoa e envia o ID do usuário (`UserIdentifiedEvent`).
- **Enterprise**: o equipamento envia a credencial crua e o servidor identifica: cartão, QR Code, tag UHF, ID e senha, imagem ou template da digital (`CardEvent`, `QrCodeEvent`, `UhfTagEvent`, `UserIdAndPasswordEvent`, `BiometricEvent`).

```csharp
using ControlId.Access.Online;

// No equipamento, uma vez: registra o servidor e liga o modo online.
await device.ConfigureOnlineModeAsync(new OnlineModeConfiguration
{
    ServerAddress = "http://192.168.0.20:5000/api",
    Mode = OnlineMode.Pro,
    RequestTimeout = TimeSpan.FromSeconds(3),   // máx. 5 s
    MaxRequestAttempts = 3,                     // falhas antes da contingência
});
```

No servidor (pacote `ControlId.Net.AspNetCore`):

```csharp
builder.Services.AddControlIdOnline<RegrasDeAcesso>();
app.MapControlIdOnline("/api");   // o mesmo caminho do ServerAddress

public sealed class RegrasDeAcesso(MeuDbContext db) : IOnlineAccessHandler
{
    public async Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent tentativa, CancellationToken ct)
    {
        var pessoa = tentativa switch
        {
            UserIdentifiedEvent u => await db.Pessoas.FindAsync([u.UserId], ct),
            CardEvent c => await db.Pessoas.FirstOrDefaultAsync(p => p.Cartao == c.CardValue, ct),
            _ => null,
        };

        if (pessoa is null) return OnlineAuthorization.NotIdentified();
        if (!pessoa.Ativa) return OnlineAuthorization.Deny(pessoa.Id, pessoa.Nome, "Cadastro inativo");

        return OnlineAuthorization.Grant(pessoa.Id, pessoa.Nome, DeviceAction.OpenDoor(1));
        // iDFlex/iDAccess Pro/Nano: DeviceAction.OpenSecBox(); iDBlock: DeviceAction.ReleaseTurnstile(...)
    }

    // Opcionais: botoeira, resultado de cadastro remoto e foto exibida no display (com UserImage = true na autorização).
    public Task OnExitButtonAsync(ExitButtonEvent e, CancellationToken ct) => Task.CompletedTask;
    public Task OnRemoteEnrollAsync(OnlineEnrollEvent e, CancellationToken ct) => Task.CompletedTask;
    public Task<byte[]?> GetUserImageAsync(long userId, CancellationToken ct) => Task.FromResult<byte[]?>(null);
}
```

- O equipamento espera a resposta por no máximo `RequestTimeout` (até 5 s). Exceção ou demora contam como falha; depois de `MaxRequestAttempts` falhas ele entra em **contingência** e decide sozinho com os dados locais, até o servidor voltar (`device_is_alive`, já respondido pelas rotas).
- `client.AuthorizeRemotelyAsync(...)` libera um acesso em tempo real a partir do servidor (`remote_user_authorization`).
- Sem ASP.NET Core: `OnlineEvents.Parse(rota, campos, corpo)` interpreta a requisição, `authorization.ToResponseJson()` gera a resposta e `OnlineEvents.ParseEnroll(rota, json)` lê os resultados de cadastro remoto.

## REP iDClass

```csharp
using ControlId.Rep;

// O REP usa HTTPS com certificado autoassinado; esse construtor já aceita o certificado.
using var rep = new RepClient("https://192.168.0.130", mode: RepMode.Portaria671);

var info = await rep.GetSystemInformationAsync();
string afdTexto = await rep.GetAfdFromNsrAsync(info.LastNsr - 100);   // texto cru

var users = await rep.GetAllUsersAsync();
await rep.SetDateTimeAsync(DateTime.Now);
```

### AFD tipado

O parser lê os leiautes da **Portaria 671/2021** e da **Portaria 1510/2009** (detecta sozinho, linha a linha):

```csharp
using ControlId.Rep.Afd;

AfdFile afd = await rep.GetAfdFileFromNsrAsync(ultimoNsrLido + 1);

foreach (var marcacao in afd.Punches)                // tipos 3 (REP-C/REP-A) e 7 (REP-P)
{
    Console.WriteLine($"{marcacao.Nsr} {marcacao.Time:dd/MM/yyyy HH:mm} {marcacao.EmployeeId}");  // CPF (671) ou PIS (1510)
}

ultimoNsrLido = afd.LastNsr ?? ultimoNsrLido;         // leitura incremental
```

- Também lê arquivos: `AfdParser.Parse(File.OpenRead("AFD.txt"))` (detecta UTF-8 ou ISO-8859-1) ou `AfdParser.ReadRecords(reader)` para ler registro a registro, sem carregar o arquivo todo.
- Disponíveis: cabeçalho (`Header`), empresa (`CompanyRecords`), empregados (`EmployeeRecords`), ajustes de relógio (`ClockAdjustments`), eventos sensíveis (`SensitiveEvents`), trailer e assinatura.
- Datas são o horário de parede gravado pelo REP; no leiaute 671 o fuso vem em `UtcOffset` (e `TimeWithOffset`).
- Uma linha corrompida não derruba a leitura: ela vira `AfdInvalidRecord` e aparece em `afd.Issues`, junto com divergências do trailer e NSR fora de ordem.
- `IsCrcValid` confere o CRC-16/KERMIT, o algoritmo que a portaria exige para REP-A e REP-P. Para REP-C a portaria não define o algoritmo, então trate o valor como informativo.
- O `RepClient` decodifica o AFD como ISO-8859-1 quando o REP não informa o charset, para não corromper nomes acentuados.

## Injeção de dependência

```bash
dotnet add package ControlId.Net.DependencyInjection --prerelease
```

Um equipamento só (fica injetável como `IAccessClient` / `IRepClient`):

```csharp
builder.Services.AddControlIdAccess(o =>
{
    o.BaseAddress = new Uri("http://192.168.0.129");
    o.Password = builder.Configuration["ControlId:Senha"]!;
});

builder.Services.AddControlIdRep(o => o.BaseAddress = new Uri("https://192.168.0.130")); // aceita o certificado autoassinado

public class PortariaService(IAccessClient portaria) { ... }
```

Vários equipamentos, direto do `appsettings.json` (a chave vira o nome):

```json
{
  "ControlId": {
    "Access": {
      "portaria": { "BaseAddress": "http://192.168.0.129", "Password": "..." },
      "garagem":  { "BaseAddress": "http://192.168.0.131", "Timeout": "00:00:10" }
    },
    "Rep": {
      "rh": { "BaseAddress": "https://192.168.0.130", "Mode": "Portaria671" }
    }
  }
}
```

```csharp
builder.Services.AddControlIdAccessDevices(builder.Configuration.GetSection("ControlId:Access"));
builder.Services.AddControlIdRepDevices(builder.Configuration.GetSection("ControlId:Rep"));

public class Sincronizacao(IControlIdClientFactory equipamentos)
{
    public async Task RodarAsync()
    {
        foreach (var nome in equipamentos.AccessDeviceNames)
        {
            var logs = await equipamentos.GetAccessClient(nome).GetAccessLogsAsync();
        }

        var afd = await equipamentos.GetRepClient("rh").GetAfdFileAsync();
    }
}
```

- Cada equipamento tem um cliente único durante a vida da aplicação, então a sessão de login é reaproveitada. Não descarte os clientes: o container faz isso.
- O `HttpClient` vem do `IHttpClientFactory`. `AddControlIdAccess`/`AddControlIdRep` devolvem o `IHttpClientBuilder`, para encadear handlers (proxy, retry, logging).
- Nome desconhecido ou configuração inválida geram `InvalidOperationException` com o nome do equipamento.
- O `ControlId.Net.AspNetCore` já inclui este pacote.

## HttpClient externo

Sem o pacote de DI, os dois clientes também aceitam um `HttpClient` externo, que eles não descartam:

```csharp
services.AddHttpClient("portaria-principal");

var http = factory.CreateClient("portaria-principal");
var device = new AccessClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.168.0.129"),
    Login = "admin",
    Password = "admin",
}, http);
```

## Erros

| Exceção | Quando |
| --- | --- |
| `ControlIdAuthenticationException` | Usuário ou senha recusados no `login.fcgi` |
| `ControlIdException` | HTTP de erro do equipamento (com `StatusCode`, `DeviceErrorCode` e `ResponseBody`), timeout ou falha de rede |

## Roadmap

- [x] Transporte com sessão automática e re-login
- [x] Acesso: objetos genéricos, usuários, logs, porta/SecBox/catraca, sistema, data/hora
- [x] REP: about, sistema, data/hora, usuários, AFD
- [x] Acesso: cartões, digitais, fotos, grupos, horários, portais e regras de acesso tipados
- [x] Acesso: modo Push e Monitor (eventos em tempo real)
- [x] Acesso: Push em lote (`transactions`) e pacote `ControlId.Net.AspNetCore` com `MapControlIdPush`/`MapControlIdMonitor`
- [x] REP: parser do AFD (Portaria 671 e legado) para objetos tipados
- [x] Extensões para `IServiceCollection` (`AddControlIdAccess`, `AddControlIdRep`)
- [x] Chamadas genéricas a qualquer endpoint (`SendAsync`, `SendRawAsync`, `SendBytesAsync`, `DownloadAsync`) e `get_configuration`
- [x] Acesso: cadastro remoto (`remote_enroll`) síncrono, assíncrono via Monitor e via Push
- [x] Acesso: modo online (Pro/Enterprise): configuração, eventos tipados, `MapControlIdOnline` e autorização remota
- [x] Acesso: comandos tipados (mensagem, buzzer, login, hash de senha, reinício, reset, rede, logotipo, GPIO, LED, USB, upsert de objetos)
- [ ] Testes de integração contra equipamentos reais
- [ ] Outras marcas de controle de acesso, em pacotes separados

## Desenvolvimento

```bash
dotnet build
dotnet test
dotnet run --project samples/ControlId.Net.Sample -- access http://192.168.0.129 admin admin
dotnet run --project samples/ControlId.Net.Server -- --urls http://0.0.0.0:5000
```

Para publicar, crie uma tag `vX.Y.Z` e faça push. O workflow `publish.yml` empacota `ControlId.Net`, `ControlId.Net.DependencyInjection` e `ControlId.Net.AspNetCore` com a versão da tag e envia ao NuGet.org usando o secret `NUGET_API_KEY`.

## Licença

[MIT](LICENSE)
