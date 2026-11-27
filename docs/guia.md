# Guia do ControlId.Net

Referência de uso dos pacotes, com exemplos para cada área da API. Para a visão geral do projeto, veja o
[README](../README.pt-BR.md). A matriz de cobertura endpoint por endpoint está em [COBERTURA.md](COBERTURA.md) e as
decisões de arquitetura, em [adr/](adr/README.md).

## Linha de acesso

O pacote cobre **toda a documentação pública da API de acesso**: os 85 endpoints, os 37 objetos, os eventos do Monitor,
do Push e do modo online e as 34 seções de configuração têm métodos, modelos ou rotas tipados. A matriz completa, com
as divergências da documentação e como foram tratadas, está em [COBERTURA.md](COBERTURA.md). O que já foi validado em
equipamento real (modelo × firmware × operação) — e o que ainda não foi — está em [matriz-validacao.md](matriz-validacao.md).

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

await device.SetUserImageAsync(id, File.ReadAllBytes("maria.jpg"));
byte[] foto = await device.GetUserImageAsync(id);
```

#### Validação de fotos e `user_test_image` (#56)

Com `ValidateImages` (padrão `true`), o cliente lê o cabeçalho do JPEG ou do PNG e recusa a foto **antes** da requisição se
ela passar dos limites: `MaxImageLongSide` (1920), `MaxImageShortSide` (1080) e `MaxImageBytes` (2 MiB). A recusa é uma
`ControlIdImageRejectedException`, com o motivo (`ImageRejectionReason.Format`, `Dimensions` ou `Size`), as dimensões, o
tamanho e o `UserId`. Os limites são opções e dão para ajustar ao modelo do equipamento.

```csharp
try
{
    await device.SetUserImageAsync(id, File.ReadAllBytes("grande.jpg"));
}
catch (ControlIdImageRejectedException e)
{
    Console.WriteLine($"{e.Reason}: {e.Width}x{e.Height}, {e.ByteCount} bytes (usuário {e.UserId})");
}

// Antes de gravar, o equipamento diz se enxerga um rosto aproveitável (nada é gravado):
FaceImageResult teste = await device.TestUserImageAsync(File.ReadAllBytes("maria.jpg"));
Console.WriteLine(teste.Success ? "Foto aceita" : "Sem rosto aproveitável");
```

`SetUserImagesAsync` valida cada foto: a inválida volta como `FaceImageResult` com `Success = false` e o motivo, sem
derrubar as outras. As válidas viram requisições de até `MaxImageBatchBytes` (1 MiB de Base64) e `MaxImagesPerBatch`
(100) fotos. Se o equipamento recusar um lote, só as fotos dele são regravadas uma a uma. Os resultados voltam na ordem
de entrada:

```csharp
var resultados = await device.SetUserImagesAsync(fotos.Select(f => new UserImage(f.UserId, f.Bytes)));
foreach (var r in resultados.Where(r => !r.Success))
{
    Console.WriteLine($"Foto do usuário {r.UserId} recusada");
}
```

#### Número do cartão e `ignore_facility` (#57)

O valor de 64 bits de um cartão com facility code é `facility × 2^32 + número`. `CardNumber` converte do e para o
formato impresso no cartão (`facility,número`). Com a configuração `ignore_facility` ligada, o equipamento compara só o
número, e o cartão pode ser cadastrado com facility 0. O `identifier_id` dos eventos de acesso traz o mesmo valor de 64 bits.

```csharp
long valor = CardNumber.Parse("123,45678");          // também aceita 123.45678, 123-45678 e 12345678 (facility 0)
await device.CreateCardAsync(new AccessCard { UserId = id, Value = valor });

var (facility, numero) = CardNumber.ToFacility(valor);   // (123, 45678)
string impresso = CardNumber.Format(valor);              // "123,45678"
bool ok = CardNumber.TryParse("texto", out _);           // false
```

### Áreas, feriados, eventos, alarmes e SecBox

```csharp
var garagem = await device.CreateAreaAsync(new AccessArea { Name = "Garagem" });
await device.LinkAsync(AccessLink.AreaAccessRule(garagem, regra));

await device.CreateHolidayAsync(AccessHoliday.FullDay("Natal", new DateTime(2026, 12, 25), TimeSpan.FromHours(-3), repeats: true));

var aberturas = await device.GetAccessEventsAsync(afterId: ultimoEvento);   // portas, SecBox e catraca
var alarmes = await device.GetAlarmLogsAsync(afterId: ultimoAlarme);

var secBox = await device.GetSecBoxAsync();                                   // iDFlex, iDAccess Pro/Nano
secBox!.RelayTimeoutMilliseconds = 5000;
await device.UpdateSecBoxAsync(secBox);
```

### Relatórios e exportações

```csharp
string csv = await device.GenerateReportAsync(new ReportRequest("users")
{
    Delimiter = ";",
    Header = "Nome;Matrícula",
    Columns = { ReportColumn.ObjectField("users", "name"), ReportColumn.ObjectField("users", "registration") },
});

AfdFile afd = await device.ExportAfdFileAsync(initialNsr: 1);           // AFD do equipamento de acesso (iDFlex Attendance)
string auditoria = await device.ExportAuditLogsAsync(AuditLogCategories.Config | AuditLogCategories.Api);
```

Os textos devolvidos são decodificados como o AFD do REP: usa o charset informado; sem ele, UTF-8 se for válido e,
senão, ISO-8859-1.

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
- Para esperas longas, use `StartRemoteEnrollAsync(...)`: a chamada volta na hora e o resultado chega pelo Monitor como `RemoteEnrollNotification` (com `EnrollType` e o mesmo `RemoteEnrollResult`). No iDFace 6.9.12 isso não acontece: com `Save = false` a tela de cadastro não abre, e com `Save = true` a face é gravada sem notificação; confira lendo o usuário ou use o cadastro síncrono.
- Com o equipamento em **modo online**, o resultado assíncrono não vai pelo Monitor: chega em `card_create`, `face_create`, `fingerprint_create`, `template_create`, `pin_create` ou `password_create`, que o `MapControlIdOnline` entrega em `IOnlineAccessHandler.OnRemoteEnrollAsync`. Nesse caso a foto da face vem em `Result.Face`.
- Via Push: `PushCommand.RemoteEnroll(request)` e `result.ResponseAs<RemoteEnrollResult>()`.
- Falhas do cadastro (timeout da pessoa, face repetida) vêm em `Error`, sem exceção; use `EnsureSuccess()` se preferir exceção.

#### `EnrollAndWaitAsync` (#36)

Para esperas longas sem depender do Monitor, `EnrollAndWaitAsync` inicia o cadastro assíncrono e acompanha
`enroller_state.fcgi` até o equipamento sair do estado de cadastro. O resultado é montado de `LastEnrollSucceeded` e
`LastEnrollError`:

```csharp
var resultado = await device.EnrollAndWaitAsync(
    new RemoteEnrollRequest(RemoteEnrollType.Face) { UserId = id, Save = true },
    timeout: TimeSpan.FromSeconds(60),             // padrão 60 s
    pollInterval: TimeSpan.FromMilliseconds(500)); // padrão 500 ms

if (!resultado.Success)
{
    Console.WriteLine(resultado.Error);
}
```

- Se o `timeout` vencer, o cadastro é cancelado no equipamento (`TryCancelRemoteEnrollAsync`) e a chamada lança `ControlIdTimeoutException`. Se o `CancellationToken` for cancelado, o cadastro também é cancelado e a `OperationCanceledException` segue.
- Limitações do iDFace 6.9.12: com `Save = false` a tela de cadastro não abre (use `Save = true` e `UserId`), e o estado final não traz o valor capturado, só sucesso, erro e `UserId`. Para o PIN o equipamento costuma devolver o erro `UNKNOWN` mesmo quando grava: confira o usuário depois de terminar.

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
| `ResetToFactoryDefaultAsync(DataLossConfirmation.Confirmed, ...)` (apaga tudo) | `reset_to_factory_default` |
| `DeleteAllAdministratorsAsync(DataLossConfirmation.Confirmed)` | `delete_admins` |
| `SetNetworkAsync` | `set_system_network` |
| `GetLogoAsync` / `SetLogoAsync` / `DeleteLogoAsync` / `ShowLogoAsync` | `logo`, `logo_change`, `logo_destroy`, `general.show_logo` |
| `GetGpioStateAsync` | `gpio_state` |
| `RefreshLedAsync` | `led_rgb_refresh` |
| `SetUsbPortEnabledAsync` | `general.usb_port_enabled` |
| `CreateOrModifyObjectsAsync` | `create_or_modify_objects` |

Via Push: `PushCommand.ShowMessage`, `PushCommand.Buzz` e `PushCommand.Reboot`.

### Recursos específicos de produto

Wi-Fi, certificados EAP, mídia da tela, SecBox, OSDP e calibração da câmera têm métodos próprios. Cada um diz no
summary em que produto e firmware existe; a lista completa está na [cobertura](COBERTURA.md).

```csharp
await device.SetWifiSourceAsync(WifiSource.Internal);
var redes = await device.ScanWifiAsync();
var resultado = await device.SetWifiSettingsAsync(redes[0].Ssid!, "senha-da-rede");   // a senha não vai para logs

await device.SendSlideshowImageAsync("promo.jpg", File.ReadAllBytes("promo.jpg"));
await device.SetStreamingLogoAsync(File.ReadAllBytes("logo.png"));

if (await device.DeviceHasSecBoxAsync() && await device.UpdateSecBoxFirmwareAsync())
{
    await device.WaitSecBoxFirmwareUpdateAsync();                                       // consulta o status até terminar
}

await device.StartCameraCalibrationAsync();
var calibracao = await device.WaitCameraCalibrationAsync();
```

Esquecer a rede Wi-Fi, apagar todos os slides e trocar ou zerar a chave do SecBox exigem
`DataLossConfirmation.Confirmed`. Segredos (`psk`, `scbk`, chave privada EAP, segredo do dual sided) saem mascarados no
`ToString` dos modelos.

### Chaves do leitor HID e código OEM

Operações que podem inutilizar um leitor ou mudar a marca do equipamento ficam fora do `IAccessClient`, num cliente
criado só a pedido ([ADR 0024](adr/0024-cliente-de-provisionamento-separado.md)). Ele usa a mesma conexão e sessão:

```csharp
var provisionamento = device.CreateProvisioningClient();

// Hierarquia do OMNIKEY: KEK padrão, depois KEKs intermediárias e chaves de cartão (modo plain: passe a KEK atual).
await provisionamento.InjectHidDefaultKekAsync(novaKek, kekDeFabrica, keyId: "4445464B45593031");
await provisionamento.InjectHidMediaKeyAsync("030101", "03F000", "FFFFFFFFFFFF", novaKek);
var info = await provisionamento.GetHidKeyInfoAsync("030101");                        // a chave nunca volta

await provisionamento.ResetHidConfigAsync(DataLossConfirmation.Confirmed);
await provisionamento.WaitHidConfigResetAsync();

var oem = await provisionamento.GetOemCodeAsync();
await provisionamento.SetOemApprovedFirmwareAsync(oem, licenca, "7.14.0");
```

Esses métodos seguem a documentação oficial e ainda não foram testados num leitor HID real: comece por um leitor de
bancada.

### Configurações

Cada seção documentada tem uma classe tipada em `ControlId.Access.Configuration`:

```csharp
using ControlId.Access.Configuration;

var geral = await device.GetConfigurationSectionAsync<GeneralSettings>();
Console.WriteLine(geral.Language);

// Envia só o que foi preenchido; várias seções numa chamada.
await device.SetConfigurationSectionsAsync(new ConfigurationSection[]
{
    new GeneralSettings { BeepEnabled = true, CatraTimeout = 3000 },
    new PjsipSettings { Enabled = true, ServerIp = "pbx.local", ServerPort = 5060 },
});
```

O acesso genérico, por nome de seção e campo, continua disponível:

```csharp
var monitor = await device.GetConfigurationAsync("monitor", new[] { "hostname", "port" });
await device.SetConfigurationAsync(new { general = new { beep_enabled = "1" } }); // valores sempre em texto
```

Um parâmetro que o modelo não tem fica `null` na leitura tipada: o cliente o tira da lista, repete a leitura e lembra
dele para não pedir de novo (log 1010, com o nome `seção.campo`). Essa lista é zerada a cada novo login (o equipamento
reiniciou ou foi atualizado) e com `device.ClearConfigurationFieldCache()`.

### Qualquer objeto, tipado

Todos os objetos da documentação têm modelo e funcionam com o CRUD genérico:

```csharp
var alarmes = await device.GetObjectsAsync<TimedAlarm>();
await device.CreateObjectAsync(TimedAlarm.Weekdays("Almoço", TimeSpan.FromHours(12)));
await device.UpdateObjectAsync(new AlarmZone { Zone = 1, Enabled = true });
await device.DeleteObjectAsync<ScheduledUnlock>(3);
```

### Endpoints sem método próprio

Todo endpoint documentado tem método tipado. Para um comando que chegou num firmware novo, ou que a biblioteca
decidiu não cobrir (`dual_sided_query`, `tests.fcgi`), use os métodos genéricos, que valem para `AccessClient` e
`RepClient` (interface `IControlIdClient`) e mantêm a sessão e o re-login automáticos:

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

var token = builder.Configuration["ControlId:CallbackToken"]!;   // o mesmo gravado no equipamento

app.MapControlIdPush().RequireControlIdToken(token);      // GET /push e POST /result
app.MapControlIdMonitor().RequireControlIdToken(token);   // POST /api/notifications/{endpoint}

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

As rotas são **fechadas por padrão**: sem `RequireControlIdToken`, respondem 401. O token vai para o equipamento com
`ConfigureCallbackSecurityAsync`; detalhes, rotação e limites em [Callbacks: token, limites e rate limiting](#callbacks-token-limites-e-rate-limiting).
Os dois `Map...` devolvem o `RouteGroupBuilder`, então dá para encadear `RequireRateLimiting`, `RequireHost`, filtros etc.
`MapControlIdPush` sem `AddControlIdPush()` lança `InvalidOperationException` já no mapeamento.
O equipamento espera a resposta HTTP do Monitor: mantenha o handler curto ou jogue o trabalho numa fila. Vários
handlers rodam todos, mesmo que um lance (a resposta é 500 no fim, e a falha vira o log 2008).

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
Se o equipamento estiver offline, o comando fica na fila até ele aparecer, mas a tarefa não espera para sempre: sem
resposta em 5 minutos (`CommandTimeout`), o envio sai da fila e a chamada falha com `TimeoutException`. Um prazo menor
no `CancellationToken` vale também; o que vencer primeiro.
`push.GetLastSeen(deviceId)` informa a última consulta de cada equipamento.

Sem ASP.NET Core, use `MonitorNotifications.Parse(rota, corpo)`, `PushServer.HandlePushAsync(deviceId, uuid)` e
`PushServer.HandleResultAsync(deviceId, uuid, corpo)` nas rotas do seu servidor HTTP, e proteja-as você mesmo.
O sample `samples/ControlId.Net.Server` tem um servidor completo.

### Prazos e limites do Push

`PushServerOptions` define os prazos e limites da fila ([ADR 0023](adr/0023-limites-da-fila-de-push.md)):

```csharp
builder.Services.AddControlIdPush(o =>
{
    o.CommandTimeout = TimeSpan.FromSeconds(30);   // SendAsync sem resposta: TimeoutException e sai da fila (padrão 5 min)
    o.CommandTtl = TimeSpan.FromHours(1);          // pendente mais velho não é entregue (padrão 24 h)
});

// Sem ASP.NET Core:
var push = new PushServer(store, new PushServerOptions { CommandTimeout = TimeSpan.FromSeconds(30), LoggerFactory = loggerFactory });
```

| Opção | Padrão | O que faz |
| --- | --- | --- |
| `CommandTimeout` | 5 min | Prazo de `SendAsync`/`SendBatchAsync`, contado do enfileiramento. Não vale para `EnqueueAsync` |
| `CommandTtl` | 24 h | Pendente mais velho que isso não é entregue: sai da fila e falha como expirado (log 1102) |
| `InFlightTimeout` | 10 min | Envio entregue sem resultado; usado pela varredura |
| `MaxPendingPerDevice` | 1.000 | Fila máxima por equipamento; acima, `PushQueueFullException` (`0` desliga) |
| `MaxTrackedDevices` | 10.000 | Equipamentos acompanhados em `GetLastSeen` (log 1104 ao atingir) |
| `RequireUuid` | `false` | `/push` e `/result` sem `uuid` são recusados: um resultado atrasado ou repetido não completa outro envio |
| `ResultHandlerTimeout` | 30 s | Prazo de cada chamada ao `IPushResultHandler` |
| `PurgeInterval` | 1 min | Intervalo da varredura automática do pacote ASP.NET Core |

- `null` desliga os prazos opcionais. Os valores são lidos uma vez, quando o `PushServer` é criado; valor inválido lança
  na criação, e com `AddControlIdPush` o host nem sobe.
- A **varredura** (`PurgeExpiredAsync`) remove pendentes vencidos e envios sem resultado há mais de `InFlightTimeout`,
  falhando a tarefa que espera ou chamando o `IPushResultHandler` com `Error`. O `AddControlIdPush` registra um serviço de
  fundo que varre a cada `PurgeInterval`. Sem ASP.NET Core, chame você mesmo, por exemplo num timer:
  `int removidos = await push.PurgeExpiredAsync(ct);` (store próprio precisa implementar `IPushCommandStoreMaintenance`).
- O `IPushResultHandler` nunca derruba `/push` nem `/result`: exceção ou prazo vencido vira o log 1101, e o próximo
  comando é entregue normalmente.
- Métrica `controlid.push.commands`, com a tag `controlid.push.outcome` (`enqueued`, `delivered`, `completed`, `orphaned`,
  `expired`, `cancelled`, `rejected`, `timeout`).

### Lotes recusados (`transactions`) (#35)

O `PushServer` entrega comandos em lote como `transactions`. Firmware que não aceita esse formato (resposta como
`Invalid member 'endpoint'` ou `Invalid command`) é lembrado por equipamento: os comandos do lote são reenfileirados como
envios individuais, sem perder resultados nem quem espera por eles, e os próximos lotes daquele equipamento já saem
individuais. `PushServerOptions.UseTransactions` controla isso: `null` (padrão) decide sozinho, `true` sempre usa
`transactions` e `false` nunca usa.

```csharp
builder.Services.AddControlIdPush(o =>
{
    o.UseTransactions = false;   // equipamento conhecido por não aceitar transactions
});
```

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
app.MapControlIdOnline("/api").RequireControlIdToken(token);   // o mesmo caminho do ServerAddress

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
- Em `*_create.fcgi` e `new_rex_log.fcgi`, um `device_id` na query diferente do corpo é recusado com 403 (log 2007).
- `client.AuthorizeRemotelyAsync(...)` libera um acesso em tempo real a partir do servidor (`remote_user_authorization`).

**Fallback e alive (#51, #52).** O equipamento conta uma resposta 500 como falha do servidor. Por isso, se o seu handler
lançar exceção, devolver `null` ou passar do `DecisionTimeout`, o token do handler é cancelado e a rota responde **200 com
a autorização de fallback**; uma requisição que não pôde ser interpretada também cai no fallback. Nunca sai 500. Configure
por `ControlIdOnlineOptions`:

```csharp
builder.Services.Configure<ControlIdOnlineOptions>(o =>
{
    o.FallbackAuthorization = OnlineAuthorization.Deny(message: "Sistema indisponível");   // padrão
    o.DecisionTimeout = TimeSpan.FromSeconds(3);                                           // padrão; Timeout.InfiniteTimeSpan desliga
});
```

Cada fallback gera o log 2014 e soma em `controlid.server.online.fallbacks`, com a tag `controlid.online.fallback.reason`
(`exception`, `null` ou `timeout`). Mantenha o `DecisionTimeout` abaixo do `RequestTimeout` do equipamento (máx. 5 s).
`device_is_alive.fcgi` e `session_is_valid.fcgi` respondem a **GET e POST**, com a mesma regra de token das demais rotas.
- Sem ASP.NET Core: `OnlineEvents.Parse(rota, campos, corpo)` interpreta a requisição, `authorization.ToResponseJson()` gera a resposta e `OnlineEvents.ParseEnroll(rota, json)` lê os resultados de cadastro remoto.

O JSON exato que o equipamento recebe (`event`, `user_id`, `portal_id`, `user_name`, `user_image`/`user_image_hash` e
`actions`), a tabela de códigos de `event` e as regras de `device_id` e do fallback estão em
[Respostas do modo online](modo-online-respostas.md).

### Hora local dos eventos (#50)

O equipamento grava a **hora local dele**, não UTC. As propriedades `*Utc` antigas (as de cada modelo da
tabela abaixo) ficaram `[Obsolete]`: tratavam a hora de parede como UTC e
saíam deslocadas pelo fuso. Use as novas, que devolvem `DateTime` com `Kind = Unspecified` (hora de parede do relógio do
equipamento, sem conversão) ([ADR 0028](adr/0028-hora-local-dos-eventos.md)):

| Modelo | Propriedade nova |
| --- | --- |
| `AccessLog` | `LocalTime` |
| `AccessDeviceEvent` | `LocalTimestamp` |
| `AlarmLog` | `LocalTime` |
| `ChangeLog` | `LocalTimestamp` |
| `ExitButtonLog` | `LocalTime` |
| `OnlineIdentificationEvent` | `LocalTime` (`DateTime?`) |

```csharp
foreach (var log in await device.GetAccessLogsAsync(afterId: 0, limit: 100))
{
    Console.WriteLine($"{log.LocalTime:dd/MM/yyyy HH:mm:ss}");   // 10:00 no relógio do equipamento aparece como 10:00
}

// Para comparar com o relógio do servidor, converta com o fuso do equipamento:
var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
var primeiro = (await device.GetAccessLogsAsync(afterId: 0, limit: 1)).First();
var instante = new DateTimeOffset(primeiro.LocalTime, fuso.GetUtcOffset(primeiro.LocalTime));
```

### Upsert e lotes (#33, #35)

`CreateOrModifyObjectsAsync` usa `create_or_modify_objects.fcgi`. Em firmware sem esse comando (`Invalid command` ou 404),
ele cai sozinho para `modify_objects` filtrando por `id` e `create_objects` só para quem não existia; por isso cada valor
precisa de `id` (sem ele, `ArgumentException`). O fallback **não é atômico**: se parar no meio, o que já foi gravado
fica, e repetir a chamada termina o trabalho sem alterar o estado. `CreateObjectsAsync` e o caminho nativo do upsert
enviam lotes de `MaxObjectsPerRequest` (200): 450 objetos viram 3 requisições, com ids e contagens juntados.

```csharp
int alterados = await device.CreateOrModifyObjectsAsync("users", new[]
{
    new { id = 1L, name = "Maria" },
    new { id = 2L, name = "João" },
});

IReadOnlyList<long> novos = await device.CreateObjectsAsync("groups", grupos);   // em lotes de 200
```

No Push, a mesma ideia vale para `transactions`: veja [Prazos e limites do Push](#prazos-e-limites-do-push).

### Sincronizar pessoas pelo estado desejado (#54, #66)

`SyncUserAsync` e `SyncUsersAsync` levam o equipamento ao estado que você descreve com `DesiredUser`: criam ou atualizam o
usuário, ajustam cartões e grupos e enviam a foto. Rodar de novo com o mesmo estado não escreve nada. O campo `null` não é
gerenciado; uma lista vazia de `Cards` só remove todos os cartões com `RemoveUnlisted = true`.

```csharp
var resultados = await device.SyncUsersAsync(new[]
{
    new DesiredUser
    {
        Id = 1, Name = "Maria", Registration = "1020",
        BeginTime = new DateTime(2026, 1, 1),            // hora local do equipamento
        Password = "1234",                                // cifrada com HashPasswordAsync só quando muda
        Cards = new long[] { CardNumber.Parse("123,45678") },
        GroupIds = new long[] { 1 },
        Photo = File.ReadAllBytes("maria.jpg"),
    },
});

foreach (var r in resultados.Where(r => !r.Success))
{
    foreach (var erro in r.Errors) Console.WriteLine($"Usuário {r.UserId}: {erro}");   // ex.: card:<valor>: ...
}

// Remover do equipamento quem não está na lista exige confirmação:
await device.SyncUsersAsync(desejados, new UserSyncOptions
{
    RemoveMissing = true,
    DataLossConfirmation = DataLossConfirmation.Confirmed,
});
```

- **Nada fora do estado desejado é removido por padrão.** `RemoveUnlisted` (padrão `false`) só remove os cartões e vínculos de grupo que sobram; sem ele, a sincronização cria o que falta e deixa o restante como está (uma lista vazia de `Cards`/`GroupIds` não apaga nada). `RemoveMissing`, que remove pessoas, não mudou e continua exigindo `DataLossConfirmation.Confirmed`.
- **`DryRun = true` devolve o diff sem escrever.** Nada é criado, alterado ou removido, nenhuma senha é cifrada (`HashPasswordAsync`) e nenhuma foto é enviada; as marcas em memória de senha e foto não são gravadas nem apagadas. Os mesmos membros do resultado descrevem o que mudaria (`Created`, `ChangedFields`, `CardsAdded`/`CardsRemoved`, `GroupsAdded`/`GroupsRemoved`, `PhotoUpdated`, `Removed`), e `RemoveMissing` não exige confirmação nesse modo. A execução real seguinte compara o mesmo estado e aplica o diff.
- **`Status` resume cada pessoa.** `UserSyncResult.Status` (enum `UserSyncStatus`) devolve `OutcomeUnknown` se algum erro tem `ControlIdOutcomeUnknownException`; senão `Failed` se há erros; senão `Removed`, `Created`, `Changed` ou `Unchanged`, nessa ordem. `Errors` continua com o detalhe por item.
- **Falha no fallback do upsert deixa estado parcial.** Sem `create_or_modify_objects`, `CreateOrModifyObjectsAsync` cai para `modify_objects` + `create_objects`, que não é atômico. Falha no meio dos `modify_objects`: os itens anteriores já estão gravados e os seguintes nem foram tentados, e nenhum `create_objects` saiu. Falha no meio dos `create_objects`: todos os `modify_objects` terminaram e parte dos novos pode ter sido criada, inclusive os do pedido que falhou. Numa falha de transporte, o pedido sem resposta pode ter sido executado: confira o estado (`GetUsersAsync`) antes de repetir. Repetir com os mesmos valores é idempotente e converge sem duplicar: quem já existe é alterado de novo pelo `id` e quem falta é criado. Em `SyncUsersAsync`, a falha ao gravar o usuário vira `user` em `Errors`, os cartões, grupos e foto daquela pessoa são pulados e `Status` fica `Failed` ou `OutcomeUnknown`, conforme a exceção.
- Uma falha por pessoa não derruba o lote: um cartão recusado (por exemplo, já é de outra pessoa) aparece só em `Errors` daquela pessoa. A sincronização não é transacional: uma execução interrompida deixa o que já foi gravado, e repetir termina o trabalho.
- O equipamento não devolve a senha em claro nem um hash da foto, então a comparação usa **marcas mantidas em memória por instância de `AccessClient`**: o que essa instância enviou por último. Na primeira sincronização de cada instância, a senha de quem já existe é regravada, e a foto é baixada e comparada byte a byte (reenviada se diferir); depois disso, repetir o mesmo estado não faz nenhuma escrita. As marcas se perdem ao reiniciar o processo ou ao trocar de instância, então mantenha uma instância de longa duração (como faz o pacote de DI).
- Para saber o que mudou, use `Created`, `Removed`, `PhotoUpdated`, `ChangedFields`, `CardsAdded`/`CardsRemoved` e `GroupsAdded`/`GroupsRemoved`, ou o `Status`.

```csharp
// Veja o diff sem escrever e depois aplique o mesmo estado, agora removendo cartões e grupos que sobram:
var previa = await device.SyncUsersAsync(desejados, new UserSyncOptions { DryRun = true });
foreach (var r in previa)
{
    Console.WriteLine($"Usuário {r.UserId}: {r.Status}");   // Unchanged, Created, Changed, Removed, Failed ou OutcomeUnknown
}

await device.SyncUsersAsync(desejados, new UserSyncOptions { RemoveUnlisted = true });
```

### Capacidades por firmware (#34)

Alguns comandos não existem em todo firmware (o iDFace 6.9.12, por exemplo, responde `Invalid command` a vários). O
[ADR 0027](adr/0027-capacidades-por-firmware.md) descreve a matriz, que espelha as seções "Observado num iDFace real" de
[COBERTURA.md](COBERTURA.md) (um teste confere que as duas concordam). `GetCapabilitiesAsync` lê o firmware em
`system_information.fcgi` e marca os comandos ausentes: depois disso, chamá-los lança `ControlIdNotSupportedException`
**sem enviar requisição**.

```csharp
DeviceCapabilities caps = await device.GetCapabilitiesAsync();
Console.WriteLine($"{caps.Model} {caps.Firmware}: sem {string.Join(", ", caps.UnsupportedCommands)}");

if (caps.Supports("create_or_modify_objects")) { /* caminho nativo */ }

try
{
    await device.GetDoorStatesAsync();
}
catch (ControlIdNotSupportedException e)
{
    Console.WriteLine($"{e.Command} não existe no firmware {e.Firmware}");
}
```

Firmware fora da matriz não tem restrições conhecidas. Mesmo sem chamar `GetCapabilitiesAsync`, um `Invalid command` ou
um 404 num `.fcgi` marca o comando no cliente, e a segunda chamada já não faz requisição; `Invalid table` lança sem marcar.

## REP iDClass

```csharp
using ControlId.Rep;

// O REP usa HTTPS com certificado autoassinado, e o cliente valida o TLS: informe a impressão digital SHA-256 do
// certificado (veja "Certificado do REP" abaixo). Sem ela, o certificado de fábrica é recusado.
using var rep = new RepClient("https://192.168.0.130", mode: RepMode.Portaria671,
    serverCertificateSha256: "AB:CD:EF:...");

var info = await rep.GetSystemInformationAsync();
string afdTexto = await rep.GetAfdFromNsrAsync(info.LastNsr - 100);   // texto cru

var users = await rep.GetAllUsersAsync();
await rep.SetDateTimeAsync(DateTime.Now);
```

O `RepClient` cobre os 47 comandos da [documentação do iDClass](https://www.controlid.com.br/suporte/api_idclass_latest.html);
a lista completa está em [docs/COBERTURA.md](COBERTURA.md#rep-idclass). No modo `Portaria671`, os usuários são
identificados por CPF; no `Legacy`, por PIS.

### Usuários

```csharp
await rep.AddUsersAsync(new[] { new RepUser { Cpf = 12345678909, Name = "Ana", Registration = "1020" } });

await rep.UpdateUsersAsync(new[] { new RepUserUpdate { Cpf = 12345678909, Name = "Ana Souza", RemoveTemplates = true } });

var alguns = await rep.GetUsersAsync(new[] { 12345678909L, 98765432100L });   // mais de 100 viram várias chamadas
await foreach (var usuario in rep.EnumerateUsersAsync())                        // base grande: páginas de 100 sob demanda
{
}

string csv = await rep.ExportUsersCsvAsync();                                  // backup
RepCsvImportResult importados = await rep.ImportUsersCsvAsync(File.ReadAllBytes("usuarios.csv"));

await rep.RemoveAdminsAsync(DataLossConfirmation.Confirmed);                   // sem volta: exige confirmação
```

### Empregador, rede e configurações

```csharp
await rep.SetCompanyAsync(new RepCompany
{
    Name = "Empresa Ltda",
    DocumentType = RepCompanyDocumentType.Cnpj,
    Document = "12.ABC.345/01DE-35",   // CNPJ alfanumérico (firmware revisão 104); só dígitos funcionam em qualquer revisão
    Address = "Rua Hungria 888",
});

RepNetworkSettings rede = await rep.GetNetworkAsync();
await rep.SetDaylightSavingTimeAsync(new RepDaylightSavingTime(new DateTime(2026, 11, 1), new DateTime(2027, 2, 20)));
await rep.SetDateTimeAsync(DateTimeOffset.Now);              // com fuso horário (Portaria 671)

RepSystemConfiguration config = await rep.GetSystemConfigurationAsync();
await rep.SetEconomicReceiptEnabledAsync(true);              // comprovante curto
await rep.SetCoilPaperAsync(300);                            // bobina nova

RepAbout sobre = await rep.GetAboutAsync();
Console.WriteLine(sobre.ApiRevision);                        // 104: revisão da API, como a documentação a numera
Console.WriteLine(sobre.ApiRevisionText);                    // o mesmo em texto; revisões com A–F (ex.: "10A") dão ApiRevision -1
```

### REP iDClass Facial

```csharp
await rep.SetUserImageAsync(cpf: 12345678909, File.ReadAllBytes("ana.jpg"));
byte[] foto = await rep.GetUserImageAsync(12345678909);

await rep.StartRemoteEnrollAsync(12345678909);               // a tela do REP pede a face
while ((await rep.GetFacialEnrollerStateAsync()).RemoteEnrolling)
{
    await Task.Delay(1000);
}
```

As APIs faciais vão sempre com `mode=671`, que a documentação exige, mesmo num cliente no modo `Legacy`.

### Certificado do REP

O iDClass sai de fábrica com um certificado autoassinado, que a validação padrão do .NET recusa. O `RepClient` valida o
certificado ([ADR 0022](adr/0022-tls-do-rep-validado.md)): fixe a impressão digital SHA-256 do certificado do seu REP, e
só esse certificado passa, mesmo autoassinado.

```csharp
// Uma vez, numa rede confiável (confira o valor no navegador ou na tela do equipamento):
string sha256 = await ControlIdCertificates.GetServerCertificateSha256Async(new Uri("https://192.168.0.130"));
// Guarde o valor na configuração. O sample faz isso com: dotnet run --project samples/ControlId.Net.Sample -- cert https://192.168.0.130

using var rep = new RepClient("https://192.168.0.130", serverCertificateSha256: sha256);   // aceita "AB:CD:...", com espaços ou sem separadores

// Ou, com todas as opções:
using var rep2 = new RepClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("https://192.168.0.130"),
    ServerCertificateSha256 = sha256,
});
```

- Sem impressão digital, a chamada falha com `ControlIdException`, e a mensagem explica como obter o valor.
- Com `ServerCertificateSha256`, `AcceptAnyServerCertificate` é ignorado. Se o REP trocar de certificado, as chamadas
  falham até a configuração ser atualizada.
- No `appsettings.json` (pacote de DI): `"ponto": { "BaseAddress": "https://...", "ServerCertificateSha256": "AB:CD:..." }`.
- Aceitar qualquer certificado (`AcceptAnyServerCertificate = true`) ainda é possível, mas expõe login e senha a quem se
  passar pelo equipamento na rede. O cliente registra o log 1013 (Warning) ao ser criado assim. Até a 0.8.0-alpha.1, o
  atalho `new RepClient(url, ...)` e o `AddControlIdRep` faziam isso sozinhos.
- Vale também para equipamentos de acesso com HTTPS ligado.

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
- Direto do REP, sem carregar o arquivo todo: `await foreach (var registro in rep.EnumerateAfdRecordsAsync(new RepAfdQuery { InitialNsr = n }))`, ou `OpenAfdStreamAsync` para gravar o arquivo em disco. Feche o stream ao terminar: aberto, ele ocupa uma das requisições simultâneas permitidas ao REP.
- `RepAfdQuery.HeaderTop` (firmware revisão 104) pede o cabeçalho no começo e sem a linha com o nome do arquivo, como no arquivo final.
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

builder.Services.AddControlIdRep(o =>
{
    o.BaseAddress = new Uri("https://192.168.0.130");
    o.ServerCertificateSha256 = builder.Configuration["ControlId:RepSha256"];   // impressão digital do certificado autoassinado
});

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
      "rh": { "BaseAddress": "https://192.168.0.130", "Mode": "Portaria671", "ServerCertificateSha256": "AB:CD:..." }
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
- As opções de cada equipamento registrado são validadas na partida: configuração inválida (ex.: sem `BaseAddress`) faz o
  host falhar ao subir, com `OptionsValidationException` citando o equipamento, em vez de falhar no primeiro uso.
- `AddControlIdAccessDevices`/`AddControlIdRepDevices` com uma seção vazia ou inexistente (o erro mais comum é o nome da
  seção) lançam `InvalidOperationException` já no registro.
- Nome desconhecido gera `InvalidOperationException` com o nome do equipamento.
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

`ConnectTimeout`, `AcceptAnyServerCertificate` e o limite de conexões do handler só valem quando o pacote cria o
`HttpClient`. Com um externo, configure o prazo de conexão no handler dele; sem isso, a conexão a um endereço que não
responde espera o `Timeout` inteiro:

```csharp
services.AddHttpClient("portaria-principal")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) });
```

## Bases grandes e vários equipamentos

### Limite por equipamento

Cada cliente limita as requisições simultâneas ao seu equipamento (`MaxConcurrentRequests`, padrão **1**, login incluído).
Dá para disparar quantas chamadas quiser em paralelo: as que passarem do limite esperam a vez, sem sobrecarregar o
equipamento. Cada equipamento tem o próprio limite, então vários equipamentos rodam em paralelo entre si.

O padrão é 1 (era 4 antes da 0.10) porque o equipamento atende uma requisição por vez
([ADR 0025](adr/0025-uma-requisicao-por-vez.md)). Quem dependia de 4 precisa pedir: `MaxConcurrentRequests = 4`.

O limite protege o equipamento, mas não o deixa mais rápido: um iDFace atende uma requisição por vez (cerca de 100 ms
cada), e 32 chamadas levaram ~3,4 s com 1, 4 ou 8 simultâneas. Paralelizar compensa entre equipamentos diferentes.

```csharp
var device = new AccessClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.168.0.129"),
    MaxConcurrentRequests = 2, // equipamento mais modesto ou rede lenta
});

var fotos = await Task.WhenAll(ids.Select(id => device.GetUserImageAsync(id))); // no máximo 2 por vez no equipamento
```

- O limite é o mesmo em qualquer runtime. Sem ele, o .NET Framework seguraria em 2 conexões por servidor e o .NET Core
  não limitaria nada. Quando o pacote cria o `HttpClient` (ou pelo pacote de DI), o limite de conexões do handler acompanha o valor.
- Com um `HttpClient` externo, o limite do cliente continua valendo, mas o limite de conexões do handler fica por sua conta.
- No `appsettings.json`: `"portaria": { "BaseAddress": "...", "MaxConcurrentRequests": 2 }`.

### Leitura paginada

`GetUsersAsync` e `GetObjectsAsync` trazem tudo numa requisição só. Em bases grandes, use os `Enumerate*Async`, que leem
em páginas (500 registros por padrão) e só buscam a próxima quando a anterior foi consumida:

```csharp
await foreach (var usuario in device.EnumerateUsersAsync())
{
    await SincronizarAsync(usuario);
}

// Coleta incremental: guarde o último ID e continue dele na próxima vez.
await foreach (var log in device.EnumerateAccessLogsAsync(afterId: ultimoId, pageSize: 1000))
{
    ultimoId = log.Id;
}

await foreach (var cartao in device.EnumerateObjectsAsync<AccessCard>(Where.Equal("cards", "user_id", 42L))) { }

// Qualquer objeto: Offset é o ponto de partida e Limit, o total máximo.
var request = new LoadObjectsRequest("user_groups") { Order = new List<string> { "user_id" } };
await foreach (var linha in device.EnumerateObjectsAsync<JsonElement>(request, pageSize: 200)) { }
```

- Usuários e logs seguem pelo ID (`id > último`): criar ou remover registros durante a leitura não faz pular nem repetir os demais.
- Os outros objetos usam `limit`/`offset`, ordenados pela chave do modelo. Com `LoadObjectsRequest`, informe `Order` para as páginas ficarem estáveis.
- Sair do `await foreach` (ou cancelar o token) para a leitura sem buscar as páginas restantes.

### Vários equipamentos de uma vez

`ControlIdDevices.RunAsync` executa a mesma operação em vários equipamentos em paralelo (8 por vez, por padrão) e devolve
um resultado por equipamento, na ordem de entrada. Um equipamento fora do ar não interrompe os demais:

```csharp
var equipamentos = new Dictionary<string, IAccessClient> { ["portaria"] = portaria, ["garagem"] = garagem };

var resultados = await ControlIdDevices.RunAsync(equipamentos, (d, ct) => d.CreateUserAsync(usuario, ct), maxParallelism: 4);

foreach (var r in resultados)
{
    if (r.Succeeded) Console.WriteLine($"{r.Device}: criado com ID {r.Value} em {r.Elapsed.TotalMilliseconds} ms");
    else Console.WriteLine($"{r.Device}: {r.Error!.Message}");
}
```

Com o pacote de DI, direto nos equipamentos registrados:

```csharp
// Todos os equipamentos de acesso (ou só alguns: RunOnAccessDevicesAsync(new[] { "portaria", "garagem" }, ...)).
var logs = await equipamentos.RunOnAccessDevicesAsync((d, ct) => d.GetAccessLogsAsync(ultimoId, cancellationToken: ct));
var porta = await equipamentos.RunOnAccessDevicesAsync((d, ct) => d.OpenDoorAsync(1, ct)); // sem valor: DeviceResult
var afds = await equipamentos.RunOnRepDevicesAsync((r, ct) => r.GetAfdFileAsync(cancellationToken: ct));
```

- `r.Value` de um equipamento que falhou lança `InvalidOperationException` com o erro original em `InnerException`; confira `Succeeded` antes.
- Erros ao obter o cliente (ex.: equipamento registrado sem `BaseAddress`) também ficam só no resultado daquele equipamento.
- Cancelar o token lança `OperationCanceledException` em vez de devolver resultados parciais.
- `maxParallelism` limita quantos equipamentos são atendidos ao mesmo tempo; dentro de cada um continua valendo o `MaxConcurrentRequests`.
- O tempo total é o do equipamento mais lento, não a soma: com um iDFace respondendo em 0,2 s e dois endereços fora do ar
  (timeout de 4 s), a chamada levou 4 s em paralelo e 8,5 s com `maxParallelism: 1`.

## Observabilidade (OpenTelemetry)

Os clientes emitem traces e métricas pelas APIs do próprio .NET (`ActivitySource` e `Meter`), sem depender do
OpenTelemetry. Para coletar, registre os nomes de `ControlIdTelemetry`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(ControlIdTelemetry.ActivitySourceName).AddHttpClientInstrumentation())
    .WithMetrics(m => m.AddMeter(ControlIdTelemetry.MeterName))
    .UseOtlpExporter();
```

**Traces**: um span `Client` por comando, com o nome do comando (`load_objects`, `execute_actions`...). Ele cobre a espera
no limite de requisições, o re-login e a requisição HTTP, que aparece como filho (com a instrumentação do `HttpClient`).
O login é um span `login` filho do comando que o disparou, e um re-login vira o evento `controlid.relogin`.

| Atributo | Valor |
| --- | --- |
| `controlid.command` | Comando, sem `.fcgi` |
| `server.address`, `server.port` | Equipamento |
| `error.type` | Status HTTP (`400`), `canceled` ou o tipo da exceção, quando falha |
| `http.response.status_code`, `controlid.device.error_code` | Quando o equipamento devolve erro |

A sessão, o login e a senha nunca vão para os atributos.

**Métricas** (com `server.address` e `server.port`):

| Métrica | Tipo | O que mostra |
| --- | --- | --- |
| `controlid.client.operation.duration` | Histograma (s) | Duração por comando (`controlid.command`, `error.type`) |
| `controlid.client.relogins` | Contador | Sessões expiradas, ex.: o equipamento reiniciou |
| `controlid.client.retries` | Contador | Novas tentativas de leituras (`ReadRetries`), com `controlid.command` e `error.type` |
| `controlid.client.requests.queued` | UpDownCounter | Requisições esperando vaga no `MaxConcurrentRequests` |
| `controlid.client.requests.active` | UpDownCounter | Requisições em andamento no equipamento |
| `controlid.client.request.wait.duration` | Histograma (s) | Espera por uma vaga no `MaxConcurrentRequests` |
| `controlid.client.circuit.transitions` | Contador | Mudanças do circuit breaker (`controlid.circuit.state`: `open`, `half_open`, `closed`) |
| `controlid.client.circuit.rejected` | Contador | Requisições recusadas com o circuito aberto |

No mesmo `Meter`, sem `server.address` (o `deviceId` também não vai para as tags):

| Métrica | Tipo | O que mostra |
| --- | --- | --- |
| `controlid.push.commands` | Contador | Envios do `PushServer` por desfecho (`controlid.push.outcome`) |
| `controlid.server.callback.requests` | Contador | Callbacks recebidos pelas rotas do ASP.NET Core (`controlid.callback.kind`: `push`, `result`, `monitor`, `online`; `controlid.callback.outcome`: `ok`, `unauthorized`, `forbidden`, `too_large`, `bad_request`, `error`) |
| `controlid.server.online.fallbacks` | Contador | Respostas de fallback do modo online (`controlid.online.fallback.reason`: `exception`, `null`, `timeout`) |
| `controlid.guard.corrections` | Contador | Correções feitas pelo guardião de configuração |
| `controlid.server.callback.handler.duration` | Histograma (s) | Tempo do código da aplicação chamado pelo callback (`controlid.callback.kind`, `error.type` em caso de exceção) |

Uma fila que não esvazia indica que o equipamento não está dando conta (um iDFace atende uma requisição por vez); muitos
re-logins indicam um equipamento reiniciando.

Para ver tudo sem hardware, o `samples/docker-compose.yml` sobe o simulador e o dashboard do Aspire, e o
`samples/ControlId.Net.Server` já vem ligado aos dois (instruções no topo do `Program.cs`).

## Logs

Os clientes escrevem logs estruturados pelo `ILogger` (categoria `ControlId.Net`, `ControlIdTelemetry.LoggerCategory`), com `LoggerMessage` gerado em tempo de compilação. Sem configuração, nada é escrito. Com injeção de dependência, o `ILoggerFactory` do container é usado sozinho; sem ela, informe-o nas options:

```csharp
var client = new AccessClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.168.0.129"),
    LoggerFactory = loggerFactory,
});
```

Um `LoggerFactory` explícito nas options vale mais que o do container ([ADR 0016](adr/0016-logs-por-options.md)). Eventos do núcleo (`device` é o endereço do equipamento; sessão, login, senha e corpo nunca são registrados):

| EventId | Nível | Evento |
| --- | --- | --- |
| 1001 | Debug | Login feito |
| 1002 | Warning | Login recusado (com o status) |
| 1003 | Information | Sessão expirada; novo login |
| 1004 | Warning | Nova tentativa (comando, tentativa e motivo) |
| 1005 | Warning | Erro devolvido pelo equipamento (status, código e mensagem) |
| 1006 | Warning | Falha de comunicação |
| 1007 | Warning | Circuito aberto (falhas seguidas e duração) |
| 1008 | Debug | Circuito meio aberto; a próxima requisição testa o equipamento |
| 1009 | Information | Circuito fechado; o equipamento voltou |
| 1010 | Information | Parâmetro de configuração que o modelo não tem (`seção.campo`), retirado das próximas leituras |
| 1011 | Information | Circuito fechado manualmente (`ResetCircuit`) |
| 1012 | Debug | Logout não confirmado (rede ou timeout); a sessão local foi descartada |
| 1013 | Warning | `AcceptAnyServerCertificate` ligado sem impressão digital |

O `PushServer` usa a mesma categoria, com o `LoggerFactory` de `PushServerOptions` (no ASP.NET Core, o do container). O
corpo dos comandos e dos resultados nunca é registrado:

| EventId | Nível | Evento |
| --- | --- | --- |
| 1101 | Error | O `IPushResultHandler` falhou ou passou do prazo; a resposta ao equipamento segue |
| 1102 | Warning | Envio expirado na fila (`CommandTtl`) e descartado |
| 1103 | Warning | Envio sem resultado (órfão) descartado |
| 1104 | Warning | Limite de equipamentos acompanhados atingido (`MaxTrackedDevices`) |
| 1105 | Warning | `/push` ou `/result` sem `uuid` recusado (`RequireUuid`) |
| 1106 | Warning | Não foi possível remover do store um envio cancelado ou vencido |
| 1107 | Information | A varredura removeu envios vencidos (quantidade) |

O pacote `ControlId.Net.AspNetCore` registra os eventos das rotas:

| EventId | Nível | Evento |
| --- | --- | --- |
| 2001 | Warning | Resultado de Push sem comando pendente |
| 2002 | Warning | Notificação do Monitor inválida |
| 2003 | Warning | Resultado de cadastro remoto inválido |
| 2004 | Warning | `new_rex_log` inválido |
| 2005 | Warning | Callback recusado (401) por token ausente ou inválido |
| 2006 | Warning | Callback recusado (401) porque a rota não exige token; uma vez por rota, com a instrução |
| 2007 | Warning | `device_id` da query diferente do corpo no modo online (403) |
| 2008 | Error | Handler do Monitor falhou (os demais seguem) |
| 2009 | Warning | Equipamento desconhecido (`IsKnownDevice`, 403) |
| 2010 | Warning | Corpo acima de `MaxRequestBodySize` (413) |
| 2011 | Error | Um `IPushResultHandler` falhou (os demais seguem) |
| 2012 | Error | A varredura do Push falhou; nova tentativa no próximo intervalo |
| 2013 | Information | Varredura automática do Push desligada (`PurgeInterval = null`) |
| 2014 | Warning | Modo online respondeu com o fallback (exceção, `null` ou `DecisionTimeout`) |
| 2015 | Error | Um `IControlIdCallbackObserver` falhou; a resposta ao equipamento segue |

O guardião de configuração (pacote `ControlId.Net.DependencyInjection`) registra:

| EventId | Nível | Evento |
| --- | --- | --- |
| 3001 | Warning | Campo que divergia do desejado foi corrigido (equipamento e campo) |
| 3002 | Warning | Não foi possível conferir um equipamento; tenta de novo no próximo ciclo |
| 3003 | Error | O ciclo do guardião falhou; tenta de novo no próximo |

## Simulador para desenvolvimento e testes

O [simulador](../tools/ControlId.Net.Simulator/README.md) dispensa o equipamento real:

- **REP iDClass**: `--Simulator:Kind=Rep` (padrão `Access`). A API fala HTTP puro, com os mesmos formatos de erro; `POST /simulator/punch` com `{"cpf":12345678901}` grava uma marcação que aparece no AFD, com NSR crescente e CRC-16/KERMIT válido.
- **Push**: com `push_server.push_remote_address` configurado, o simulador consulta `/push` do seu servidor a cada `push_request_period` segundos, executa o comando (simples ou `transactions`) e devolve o resultado em `/result`.
- **Modo online**: com `general.online=1`, `POST /simulator/identify` consulta o seu servidor (`new_user_identified.fcgi` ou `new_card.fcgi`), aplica o `result.event` devolvido e cai na decisão local se o servidor falhar.

## Novas tentativas em leituras

Equipamentos em rede local falham de forma passageira (Wi-Fi, um reinício). Com `ReadRetries`, os comandos de **leitura**
são repetidos depois de uma falha de rede, de um timeout ou de um HTTP 5xx, 408 ou 429:

```csharp
var device = new AccessClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.168.0.129"),
    ReadRetries = 2,                               // até 2 novas tentativas (padrão 0, máximo 5)
    RetryDelay = TimeSpan.FromMilliseconds(300),   // 300 ms, depois 600 ms, ±20% (padrão 200 ms)
});
```

- Só leituras (`load_objects`, `system_information`, `get_configuration`, `get_afd`, `load_users`...). Comandos que alteram o
  equipamento, como `execute_actions` e `create_objects`, **nunca** são repetidos: um timeout não diz se a porta já abriu.
  Na API da Control iD tudo é POST, então a lista é por comando ([ADR 0013](adr/0013-novas-tentativas-so-em-leituras.md)).
- Um 4xx não é repetido (daria o mesmo erro), e cancelar o token interrompe a espera na hora.
- No `appsettings.json` (pacote de DI): `"portaria": { "BaseAddress": "...", "ReadRetries": 2 }`, também no REP.
- Cada nova tentativa aparece no trace (evento `controlid.retry`) e na métrica `controlid.client.retries`.

## Health checks

Com o pacote de DI, um health check verifica todos os equipamentos registrados em paralelo, com uma leitura que não altera
nada (`system_information` no acesso, `get_about` no REP):

```csharp
builder.Services.AddControlIdAccessDevices(builder.Configuration.GetSection("ControlId:Access"));
builder.Services.AddHealthChecks().AddControlIdDevices(o =>
{
    o.Timeout = TimeSpan.FromSeconds(3);          // prazo de cada equipamento (padrão 5 s)
    o.SlowThreshold = TimeSpan.FromSeconds(1);    // acima disso, conta como lento (padrão 2 s)
    o.CacheDuration = TimeSpan.FromSeconds(10);   // reaproveita a sonda (padrão: sem cache)
}, tags: new[] { "ready" });

// Degraded responde 200 no padrão do ASP.NET Core; 503 tira a instância do balanceador.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResultStatusCodes = { [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable },
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("live") });   // só o processo, sem equipamentos
```

| Situação | Status |
| --- | --- |
| Todos responderam, abaixo do limite de lentidão | `Healthy` |
| Algum falhou, passou do prazo ou está lento | `Degraded` |
| Nenhum respondeu | `failureStatus` do registro (padrão `Unhealthy`) |
| Nenhum equipamento registrado (com `RequireDevices`, o padrão) | `failureStatus` do registro |

O resultado de cada equipamento vai nos dados do health check (`access:portaria` → `ok: 45 ms`, `falhou em 2082 ms: ...`).
Um equipamento travado não segura os outros: o prazo vale para cada um, e o check inteiro tem prazo total de 30 s
(`TotalTimeout`; quem não respondeu conta como falha). O `samples/ControlId.Net.Server` mostra uma resposta em JSON com
esses dados, detalhada só para chamadas da própria máquina.

- `RequireDevices` (padrão `true`): zero equipamentos registrados, quase sempre uma seção de configuração com o nome
  errado, não passa por saudável. Em aplicações sem equipamentos registrados (só Push ou modo online), use `false`.
- `CacheDuration`: o resultado da sonda de cada equipamento é reaproveitado por esse tempo, compartilhado entre o check
  agregado e os por equipamento (com os dois registrados, uma requisição por equipamento). Evita sondar o equipamento a
  cada chamada de um `/health` consultado com frequência.
- Não use a tag do check dos equipamentos na prova de vida (`live`): um equipamento fora do ar não deve reiniciar a aplicação.

### Um check por equipamento e último contato

`AddControlIdDeviceChecks` registra um health check por equipamento (`controlid:access:portaria`), então dá para ver qual
caiu. Com Push, Monitor ou modo online, as rotas do `ControlId.Net.AspNetCore` registram o último contato de cada
equipamento (`DeviceContactTracker`), e `MaxContactAge` degrada o equipamento cujo último contato passou da idade
máxima. Só degrada quem tem contato registrado e vencido; sem contato registrado vale a leitura ativa.

```csharp
builder.Services.AddHealthChecks().AddControlIdDeviceChecks(o => o.MaxContactAge = TimeSpan.FromMinutes(5));
```

Sem equipamentos registrados e com `RequireDevices`, `AddControlIdDeviceChecks` registra um check `controlid:none` (o
prefixo é configurável) com o `failureStatus`, para o erro aparecer em vez de sumir.

## Circuit breaker

Um equipamento fora do ar faz cada chamada esperar o timeout. Com o circuit breaker, depois de N falhas seguidas (rede,
timeout ou HTTP 5xx) as chamadas seguintes falham na hora com `ControlIdCircuitOpenException` (`RetryAfter` diz quando
tentar de novo), sem tocar na rede. Passado o tempo, uma chamada de teste decide se fecha. Desligado por padrão ([ADR 0017](adr/0017-circuit-breaker-proprio.md)).

```csharp
var opcoes = new ControlIdDeviceOptions
{
    CircuitBreakerFailureThreshold = 5,                       // 0 desliga (padrão); máximo 100
    CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30),
};
// client.CircuitState: Closed, Open ou HalfOpen
```

- A chamada de teste (`HalfOpen`) sem resposta por mais de `2 × Timeout` (mínimo 1 s) conta como falha e reabre o
  circuito, então um `HttpClient` com timeout infinito não prende o circuito meio aberto (nesse caso, o limite é 5 min).
- `AccessClient` e `RepClient` implementam `IControlIdClientDiagnostics`, com `Session`, `CircuitState` e `ResetCircuit()`
  (fecha o circuito na hora, log 1011). As interfaces `IAccessClient` e `IRepClient` não mudam; com DI, faça o cast:

```csharp
if (equipamentos.GetAccessClient("portaria") is IControlIdClientDiagnostics diagnostico)
{
    diagnostico.ResetCircuit();   // ex.: depois de trocar o equipamento
}
```

## Callbacks: token, limites e rate limiting

As rotas de Push, Monitor e modo online são **fechadas por padrão** ([ADR 0021](adr/0021-callbacks-fechados-por-padrao.md)):
sem `RequireControlIdToken`, respondem 401 (com `WWW-Authenticate: Bearer`) e registram o log 2006 uma vez por rota.
Exija um token Bearer e grave o mesmo token no equipamento ([ADR 0019](adr/0019-token-bearer-nos-callbacks.md)):

```csharp
app.MapControlIdPush().RequireControlIdToken("segredo-longo");
app.MapControlIdMonitor().RequireControlIdToken(o => o.DeviceToken = id => tokens.Get(id));   // um token por equipamento

await device.ConfigureCallbackSecurityAsync("segredo-longo");   // grava o token (e, se informado, o certificado fixado)
```

- Sem token ou com token errado, a rota responde 401 (log 2005). A comparação é em tempo constante.
- **Rotação**: publique o token novo em `Token` e mantenha o antigo em `PreviousToken` até todos os equipamentos
  receberem o novo; depois, remova-o.
  `RequireControlIdToken(o => { o.Token = novo; o.PreviousToken = antigo; })`.
- **Token por equipamento**: com `DeviceToken`, um equipamento para o qual a função devolve `null` (ou um callback sem
  `deviceId`) é recusado. Para aceitar o `Token` global nesses casos, ligue `AllowTokenFallback = true`; sem isso, um token
  compartilhado vazado não abre o parque inteiro. `DeviceToken` só é chamado quando a requisição traz o cabeçalho `Bearer`.
- **Rota aberta**, só em rede isolada ou para firmware anterior à 6.22 (que não envia o cabeçalho): `.AllowAnonymousControlIdCallbacks()`,
  junto com uma restrição de rede (`RequireHost`, firewall).
- O nome da seção e o formato do token e do certificado no equipamento ainda estão **a validar** em hardware; o
  parâmetro `section` (padrão `general`) permite ajustar.

**Limites.** Configure com `ControlIdCallbackOptions`:

```csharp
builder.Services.Configure<ControlIdCallbackOptions>(o =>
{
    o.MaxRequestBodySize = 8 * 1024 * 1024;              // padrão 4 MiB (DefaultMaxRequestBodySize); acima, 413
    o.IsKnownDevice = id => equipamentosConhecidos.Contains(id);   // fora da lista: 403, sem tocar a fila nem os handlers
});
```

- Com token único (sem `DeviceToken`) ou rota aberta, a autorização vem antes de ler o corpo. Com `DeviceToken`, o corpo
  é lido com o limite e só então autorizado, porque o `deviceId` pode vir nele.
- `/push` e `/result` respondem com `Cache-Control: no-store`.

**Rate limiting.** A biblioteca não escolhe a política; use o rate limiting do ASP.NET Core no grupo devolvido. Um
equipamento consulta o `/push` a cada poucos segundos, então um limite por IP como o do sample sobra para ele e corta quem
tenta adivinhar o token:

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("controlid", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
});

app.UseRateLimiter();
app.MapControlIdPush().RequireControlIdToken(token).RequireRateLimiting("controlid");
```

Atrás de um proxy reverso, o IP é o do proxy: use `UseForwardedHeaders`. As recusas aparecem nos logs 2005–2010 e na
métrica `controlid.server.callback.requests`.

### Observador de callbacks (#65)

Um observador vê o callback **antes** dos handlers — e, quando a rota autoriza sem o corpo (token único ou rota anônima),
antes do `Parse` —, para gravar o evento bruto (auditoria, reprocessamento). Registre um ou vários; cada um é scoped (pode
receber `DbContext`) e todos rodam na ordem de registro. Registrar o mesmo tipo de novo não tem efeito.

```csharp
using ControlId.AspNetCore;

builder.Services.AddControlIdCallbackObserver<AuditoriaDeCallbacks>();

public sealed class AuditoriaDeCallbacks(MeuDbContext db) : IControlIdCallbackObserver
{
    public async Task OnCallbackAsync(ControlIdRawCallback callback, CancellationToken ct)
    {
        db.Callbacks.Add(new CallbackAudit
        {
            Kind = callback.Kind,           // Online, Monitor ou PushResult
            Route = callback.Route,         // ex.: "/result", sem a query
            DeviceId = callback.DeviceId,   // null quando não foi possível saber
            Body = callback.Body.ToArray(), // cópia com os campos sensíveis mascarados
        });
        await db.SaveChangesAsync(ct);
    }
}
```

- O `ControlIdRawCallback` traz `Kind`, `Route` (caminho sem a query), `Headers`, `DeviceId` (quando conhecido) e `Body`
  (bytes). Os cabeçalhos `Authorization`, `Cookie` e `Proxy-Authorization` não são entregues.
- O mascaramento é o mesmo dos cassetes: `session`, `login`, `password`, `salt`, `image` e `template` viram `***` no JSON
  (recursivamente) e no formulário `application/x-www-form-urlencoded`. O corpo que não dá para interpretar (inclusive
  binário) é entregue byte a byte, e o `Parse`/handler continua lendo o original: o mascaramento vale só para o observador.
- Um corpo inválido não impede o observador: depois de a requisição ser autorizada, ele recebe os bytes e a resposta segue
  o contrato da rota (fallback no modo online, 400 no Monitor).
- O observador só é chamado depois do token e de `IsKnownDevice`; requisição recusada (401, 403 ou 413) não é entregue. Com
  **token por equipamento**, o `device_id` pode vir do corpo, então o callback só chega ao observador depois do `Parse` e
  da autorização; um callback sem `device_id` é recusado (salvo `AllowTokenFallback` com token global) e não aparece.
  A exceção do observador vira o log 2015, não muda a resposta e não impede os demais.
- No Push, só o `POST /result` é observado (`Kind = PushResult`); o `GET /push` não passa pelo observador.

## Fila de Push durável

Por padrão a fila fica em memória e some se o servidor reiniciar. O pacote `ControlId.Net.Push.EntityFrameworkCore`
(`net10.0`) guarda a fila no seu `DbContext` ([ADR 0020](adr/0020-fila-de-push-duravel.md)):

```csharp
protected override void OnModelCreating(ModelBuilder mb) => mb.ApplyControlIdPush();   // tabela controlid_push_commands

builder.Services.AddDbContextFactory<MeuDbContext>(o => o.UseSqlite("Data Source=push.db;Default Timeout=30"));
builder.Services.AddControlIdPushEntityFrameworkStore<MeuDbContext>();
builder.Services.AddControlIdPushResultHandler<MeuHandler>();   // IPushResultHandler: resultados sem tarefa esperando
builder.Services.AddControlIdPushResultHandler<Auditoria>();    // vários handlers: todos rodam
```

O comando é serializado ao enfileirar: alterar o `Body` depois de enfileirar não tem mais efeito. Um comando órfão
(resultado que não voltou) sem tarefa esperando é reportado ao `IPushResultHandler` com `Error` e nunca reenviado.
Dá para implementar o seu `IPushCommandStore` para outro banco (e `IPushCommandStoreMaintenance`, para a varredura).

- O store precisa de `AddDbContextFactory<TContext>`; sem ela, a resolução falha com uma mensagem que diz isso.
- **Várias instâncias** sobre o mesmo banco não entregam o mesmo envio: o próximo é tomado com um `UPDATE` condicional
  ([ADR 0023](adr/0023-limites-da-fila-de-push.md)), sem mudança de esquema. `EnqueueAsync` com `IPushResultHandler`
  funciona em qualquer instância. `SendAsync` ainda exige que o `/result` chegue à instância que enfileirou: em outra, o
  resultado vai ao handler dela e a tarefa original termina pelo `CommandTimeout`.
- Com SQLite e várias conexões, ponha `Default Timeout` na connection string para as escritas esperarem o bloqueio em
  vez de falhar. Os testes de concorrência cobrem só o SQLite.
- **Handlers**: `AddControlIdPushResultHandler<T>` registra cada handler como scoped (pode receber `DbContext`). A cada
  resultado, o pacote cria um escopo e chama todos, em sequência; a falha de um vira o log 2011 e não impede os outros.

## Erros

| Exceção | Quando |
| --- | --- |
| `ControlIdAuthenticationException` | Usuário ou senha recusados no `login.fcgi` |
| `ControlIdConnectionException` | Não conectou (conexão recusada, endereço sem resposta, prazo de conexão vencido): o comando **não saiu**, é seguro repetir |
| `ControlIdTimeoutException` | Leitura sem resposta no `Timeout` |
| `ControlIdOutcomeUnknownException` | Escrita enviada sem resposta (timeout ou conexão caída): o equipamento pode ter aplicado ou não (`Command`, `TimedOut`) |
| `ControlIdDeviceErrorException` | O equipamento respondeu com erro (`StatusCode`, `DeviceErrorCode`, `DeviceMessage`) |
| `ControlIdNotSupportedException` | `Invalid command`, `Invalid table` ou 404 num `.fcgi` (`Command`, `Firmware`); deriva da anterior |
| `ControlIdImageRejectedException` | Foto recusada antes do envio (`Reason`, `Width`, `Height`, `ByteCount`, `UserId`) |
| `ControlIdException` | Base de todas as anteriores; falhas de rede restantes, inclusive certificado TLS recusado |
| `ControlIdCircuitOpenException` | Circuit breaker aberto (`RetryAfter`) |
| `PushQueueFullException` | Fila de Push do equipamento cheia (`DeviceId`, `Limit`) |
| `TimeoutException` | `SendAsync`/`SendBatchAsync` do Push sem resposta no `CommandTimeout` |

As exceções do cliente derivam de `ControlIdException`, então `catch (ControlIdException)` continua pegando tudo
([ADR 0026](adr/0026-excecoes-por-desfecho.md)); capture os tipos específicos antes dele para decidir por desfecho.

### Escrita sem resposta: o exemplo da porta (#32)

Em uma escrita, "não sei se aconteceu" é diferente de "não aconteceu". Um `OpenDoorAsync` que dá timeout pode ter aberto a
porta. Em vez de repetir às cegas, confira o estado (`doors_state`):

```csharp
try
{
    await device.OpenDoorAsync(door: 1);
}
catch (ControlIdConnectionException)
{
    await device.OpenDoorAsync(door: 1);        // nada saiu: repetir é seguro
}
catch (ControlIdOutcomeUnknownException e)
{
    var estado = await device.GetDoorStatesAsync();
    var aberta = estado.Doors.Any(d => d.Id == 1 && d.Open);
    Console.WriteLine($"{e.Command} ({(e.TimedOut ? "timeout" : "conexão caída")}): porta {(aberta ? "abriu" : "não abriu")}");
}
catch (ControlIdTimeoutException)
{
    // só em leitura: ler de novo é seguro
}
```

### Prazo de conexão (#53)

`ConnectTimeout` (padrão 5 s) limita só o estabelecimento da conexão, para um equipamento fora do ar falhar em segundos e
não nos 30 s do `Timeout`. O prazo vencido vira `ControlIdConnectionException`. Se o `ConnectTimeout` passar do
`Timeout`, vale o `Timeout`; `Timeout.InfiniteTimeSpan` desliga o prazo. No .NET Framework (alvo `netstandard2.0`) o
`HttpClientHandler` não tem prazo de conexão e o `ConnectTimeout` é **ignorado**: só o `Timeout` vale. Com um `HttpClient`
externo, veja [HttpClient externo](#httpclient-externo).

### O que o pacote registra e o que não registra (LGPD) (#39)

Nome, cartão, foto, PIN e senha são dados pessoais. O pacote **não** os leva para logs, traces, métricas nem mensagens de exceção:

| Saída | O que contém | O que nunca contém |
| --- | --- | --- |
| Logs (`ILogger`) | Comando, endereço do equipamento, status HTTP, código e mensagem de erro do equipamento, contagens | Sessão, login, senha, corpo de requisição ou resposta, nomes, cartões, fotos |
| Traces | `controlid.command`, `server.address`, `server.port`, `error.type`, status | Sessão, login, senha, conteúdo |
| Métricas | Contadores e durações, com `server.address` e `server.port` | Identificador de equipamento do Push, dados de pessoas |
| Exceções | `Message` com comando e motivo; `ControlIdImageRejectedException` com `UserId` e dimensões | Os bytes da foto, senha, cartão |
| Cassetes do `ControlId.Net.Testing` | Conversa gravada | `session`, `login`, `password`, `salt`, `image` e `template` (mascarados como `***`) |

`ControlIdDeviceOptions.ExceptionResponseBody` decide o que fica em `ControlIdException.ResponseBody`: `ErrorOnly` (padrão)
só guarda o corpo quando ele é o JSON de erro do equipamento (`{"error":..,"code":..}`) e deixa `null` nos demais casos;
`Full` guarda o corpo recebido (pode conter dados de pessoas: use só para diagnóstico e não o leve para logs); `None` nunca
guarda. O `PersonalDataLeakTests` do repositório exercita isso. O que a **sua** aplicação grava (logs próprios, banco, o
corpo que você recebe nos callbacks) continua sendo responsabilidade dela.

## Guardião de configuração (#55)

Alguém muda `general.online` pela tela do equipamento, ou o relógio dele deriva, e a integração para de funcionar sem
aviso. `AddControlIdConfigurationGuard` (pacote `ControlId.Net.DependencyInjection`) roda um serviço de segundo plano que,
a cada ciclo, lê a configuração de cada equipamento listado e **só escreve o que difere** do desejado. Um equipamento
fora do ar não trava os demais.

```csharp
builder.Services.AddControlIdConfigurationGuard(o =>
{
    o.Interval = TimeSpan.FromMinutes(5);          // padrão
    o.ClockTolerance = TimeSpan.FromSeconds(60);   // padrão; null desliga o acerto do relógio
    o.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");   // padrão: Local

    o.Devices["portaria"] = new DesiredDeviceConfiguration
    {
        Online = true,
        OnlineServerId = 1,
        Monitor = new MonitorTarget { Hostname = "192.168.0.20", Port = 5000, Path = "/monitor" },
    };
    o.Devices["portaria"].Settings["push_server"] = new Dictionary<string, string> { ["push_request_period"] = "5" };
});

builder.Services.AddHealthChecks().AddControlIdConfigurationGuard();   // último ciclo e falhas
```

Quando `system_information.time` diverge além da `ClockTolerance`, o guardião acerta o relógio com `SetSystemTimeAsync`.
Cada correção gera o log 3001 e soma em `controlid.guard.corrections`; as falhas são os logs 3002 e 3003. O guardião usa o
`IControlIdClientFactory`, então os equipamentos precisam estar registrados (`AddControlIdAccess` ou `AddControlIdAccessDevices`).

## Descoberta de equipamentos (#58)

`ControlIdDiscovery` varre endereços e diz o que há em cada um. A varredura é paralela (`MaxParallelism`, 32) e, num
endereço sem resposta, termina perto do `ConnectTimeout` (1 s).

```csharp
var enderecos = ControlIdDiscovery.ExpandCidr("192.168.0.0/24", port: 80);

var achados = await ControlIdDiscovery.ScanAsync(
    enderecos,
    new[] { new DiscoveryCredential("admin", "minha-senha") },
    new DiscoveryOptions { TryFactoryDefaults = false });   // true também tenta as credenciais de fábrica; padrão false

foreach (var d in achados)
{
    Console.WriteLine($"{d.Address} {d.Status} {d.Model} {d.Firmware} {d.Serial} {d.Mac} (login {d.CredentialLogin})");
}
```

`DiscoveryStatus`: `Found`, `PortClosed`, `NotControlId`, `CredentialRejected` e `Error`. `CredentialLogin` mostra qual
login funcionou, nunca a senha; a senha também não vai para log nem para exceção.

## Testes sem equipamento (`ControlId.Net.Testing`) (#31)

O pacote `ControlId.Net.Testing` grava a conversa com um equipamento real num cassete JSON (`RecordingHandler`, que mascara
sessão, login, senha, `salt`, imagem e template) e a reproduz nos testes (`ReplayHandler`), que falha dizendo o que divergiu
(ordem, endpoint ou corpo). Passo a passo e a troca de um cliente próprio pelo `ControlId.Net`:
[Migrando de um cliente próprio e testando sem equipamento](migrando-de-cliente-proprio.md).

```csharp
var cassette = Cassette.Load("cassetes/usuarios.json");
using var http = new HttpClient(new ReplayHandler(cassette));
using var client = new AccessClient(opcoes, http);
var usuarios = await client.GetUsersAsync();
```

## Desenvolvimento

```bash
dotnet build
dotnet test
dotnet run --project samples/ControlId.Net.Sample -- access http://192.168.0.129 admin admin
dotnet run --project samples/ControlId.Net.Sample -- cert https://192.168.0.130          # impressão digital do REP
dotnet run --project samples/ControlId.Net.Sample -- rep https://192.168.0.130 admin admin AB:CD:...

# O servidor de exemplo exige o token dos callbacks e a chave da API (veja o topo do Program.cs):
dotnet user-secrets set --id controlid-net-server-sample ControlId:CallbackToken "<token longo e aleatório>"
dotnet user-secrets set --id controlid-net-server-sample ApiKey "<chave da API de operação>"
dotnet run --project samples/ControlId.Net.Server -- --environment Development --urls http://localhost:5000
```

Os testes rodam em `net8.0`, `net10.0` e `net48`. Os dois primeiros exercitam o build `net8.0` das bibliotecas (o `net10.0` garante a compatibilidade com o .NET 10, inclusive o pacote ASP.NET Core sobre o ASP.NET Core 10); o `net48` exercita o build `netstandard2.0`, usado no .NET Framework, e só executa no Windows. Para compilar o alvo `net10.0` é preciso o SDK do .NET 10. Em Linux ou macOS, use `dotnet test -f net8.0` ou `-f net10.0`.

O `ci.yml` roda a cada push na `main` e em todo pull request: build sem avisos, testes nos três alvos (Linux e Windows), validação da API pública e resumo de cobertura. O `publish.yml` repete os testes ao lançar versão e só publica se todos passarem.

Para publicar, crie uma tag `vX.Y.Z` e faça push. O workflow `publish.yml` empacota `ControlId.Net`, `ControlId.Net.DependencyInjection`, `ControlId.Net.AspNetCore` e `ControlId.Net.Push.EntityFrameworkCore` com a versão da tag e envia ao NuGet.org por Trusted Publishing (OIDC), sem chave guardada no repositório. Configuração única: uma política de Trusted Publishing no nuget.org para este repositório e o arquivo `publish.yml`, e o secret `NUGET_USER` com o nome de usuário do nuget.org.
