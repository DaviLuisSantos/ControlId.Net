# PLAN — Correções da auditoria de 2026-11-19

## Contexto
Implementa os achados de `docs/auditoria-2026-11-19.md` (P1, P2 e os P3 baratos). Base: `main` em `30da687`
(0.8.0-alpha.1, já publicada no NuGet com a tag `v0.8.0-alpha.1`).

Decisões do usuário:
- Escopo: P1 + P2 + P3 baratos. O fatiamento do `IAccessClient` fica para um plano rumo à 1.0.
- Callbacks **fechados por padrão**: rota sem `RequireControlIdToken` responde 401; liberar exige `AllowAnonymousControlIdCallbacks()`.
- TLS do REP **validado por padrão**: sai o `AcceptAnyServerCertificate = true` do atalho `new RepClient(string)` e do DI; o atalho ganha `serverCertificateSha256`.
- Store EF com **claim atômico**: várias instâncias não entregam o mesmo comando; `EnqueueAsync` + `IPushResultHandler` funcionam em
  qualquer instância, `SendAsync` continua exigindo que `/result` chegue à instância que enfileirou (documentado).

Decisões deste plano (vetáveis antes da execução):
- `PushServerOptions.CommandTimeout` padrão 5 min e `CommandTtl` padrão 24 h (comando mais velho não é entregue).
- `ControlIdHealthCheckOptions.RequireDevices` padrão `true` (zero equipamentos registrados deixa de ser `Healthy`).
- Seções vazias em `AddControlIdAccessDevices`/`AddControlIdRepDevices` passam a lançar na hora do registro.
- Unificação de flags `0/1`, leitura de volta do token no equipamento e CPF numérico no REP **ficam fora**: dependem de teste em hardware.

Regras do repo (CONTRIBUTING.md): teste para comportamento novo, XML docs em português, API nova em `PublicAPI.Unshipped.txt`,
quebra intencional em `CompatibilitySuppressions.xml` com o motivo, CHANGELOG `[Unreleased]`, zero avisos (`-warnaserror` na CI),
ADRs em `docs/adr/` (última: 0020), commits em português e sem coautoria de IA. Nada de membro novo em interface já publicada
(`IAccessClient`, `IRepClient`, `IPushCommandStore`, `IPushResultHandler`): capacidades novas entram em interfaces novas.

## Rastreabilidade (achado → tarefa)
| Achado (auditoria) | Tarefa |
| --- | --- |
| P1-1 callbacks abertos | T3 (+ T7 samples, T9 ponta a ponta, T10 docs/ADR) |
| P1-2 handler do Push perde o próximo comando | T1 |
| P1-3 memória ilimitada, fila sem limite/validade | T1, T2, T3 |
| P1-4 `SendAsync` sem prazo, `InFlight` eterno | T1, T2, T4 (hosted service) |
| P1-5 TLS do REP | T5, T6 |
| P2-6 Online autentica A e entrega B | T3 |
| P2-7/8 corpo antes do token, sem limite, handlers sem isolamento | T3 |
| P2-9 ordem do token, fallback global, rotação | T3 |
| P2-10 cancelamento não atômico | T1 |
| P2-11 EF sem claim atômico | T2 |
| P2-12/13/14 validação tardia, health check | T6 |
| P2-15/16 `ApiRevision`, enums | T5 |
| P2-17/18/20 cache de configuração, `LogoutAsync`, `SetAdministratorAsync` | T5 |
| P2-19 retry/relogin duplicados no transporte | T8 |
| P2-22 `Session`/`CircuitState` fora das interfaces | T5 (parte barata; fatiamento fora) |
| P3 circuito, `uuid`, `no-store`, captive dependency, telemetria de entrada | T1, T3, T4, T5 |
| P3 samples | T7 |
| Build/CI | T0 |

## Contratos

**C0 — Base de API e build**
- Mover o conteúdo de `PublicAPI.Unshipped.txt` para `PublicAPI.Shipped.txt` nos 4 projetos de `src/` (é o que saiu na 0.8.0-alpha.1)
  e `PackageValidationBaselineVersion` = `0.8.0-alpha.1` em `src/Directory.Build.props` e no csproj do pacote EF (hoje vazio).
- `Directory.Build.props`: `<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1900</WarningsNotAsErrors>` (sem rede, o `-warnaserror` derruba o build).
- `global.json`: SDK `10.0.100`, `rollForward: latestFeature`.
- `Directory.Packages.props` (gerenciamento central): todas as versões saem dos csproj; mesmas versões de hoje, condicionais por alvo mantidas.
- `stryker-config.json`: `"break": 60`. `ci.yml`: limite mínimo de cobertura de linha no ReportGenerator (valor = cobertura atual arredondada para baixo; confirmar a opção na versão 5.5.12).

**C1 — Push no núcleo (`ControlId.Access.Push`, netstandard2.0)**
- `public sealed class PushServerOptions`:
  `TimeSpan? CommandTimeout = 5 min` (prazo de `SendAsync`/`SendBatchAsync` sem prazo próprio; `null` = sem prazo; ao vencer, remove do
  store e lança `TimeoutException`), `TimeSpan? CommandTtl = 24 h` (pendente mais velho não é entregue), `TimeSpan? InFlightTimeout = 10 min`
  (usado na varredura), `int MaxPendingPerDevice = 1000` (0 = sem limite), `int MaxTrackedDevices = 10_000`, `bool RequireUuid = false`,
  `TimeSpan ResultHandlerTimeout = 30 s`, `TimeSpan? PurgeInterval = 1 min` (lido pelo hosted service do ASP.NET Core; `null` desliga),
  `ILoggerFactory? LoggerFactory`; `internal Func<DateTimeOffset>? Clock` (testes). `Validate()` interno.
- Novo ctor `PushServer(IPushCommandStore store, PushServerOptions options, IPushResultHandler? unclaimedResultHandler = null)`; os atuais
  continuam e usam `new PushServerOptions()`.
- `public sealed class PushQueueFullException : ControlIdException { long DeviceId; int Limit; }` — `Enqueue*`/`Send*` com a fila cheia
  (contagem feita sob o gate do equipamento, antes do `AddAsync`).
- `public interface IPushCommandStoreMaintenance { Task<IReadOnlyList<PushEnvelope>> RemoveExpiredAsync(DateTimeOffset pendingCreatedBefore, DateTimeOffset inFlightDeliveredBefore, CancellationToken cancellationToken); }`
  implementada pelo `InMemoryPushCommandStore` (e pelo EF na T2). `PushServer.PurgeExpiredAsync(CancellationToken)` → `Task<int>`: usa
  `CommandTtl`/`InFlightTimeout`, falha cada envio removido (waiter com `ControlIdException` "expirado" ou handler com `Error`); store sem a
  interface → 0.
- Comportamento:
  - O handler (`IPushResultHandler`) **nunca** derruba `/push` nem `/result`: chamado com `CancellationTokenSource(ResultHandlerTimeout)`
    sem vínculo com o token do equipamento; exceção vira log 1101 (Error, com `CommandId` e `DeviceId`). No `/push`, o payload do
    próximo envio é devolvido mesmo se o handler do órfão falhar.
  - `TakeNext` em laço: `Next` com `CreatedAt + CommandTtl < agora` → `RemoveAsync`, falha como expirado (log 1102), pega o seguinte.
  - `RequireUuid`: `/push` sem `uuid` devolve vazio e `/result` sem `uuid` devolve `false` (log 1105).
  - Cancelamento: `CancelAsync` cancela o waiter na hora e remove do store em segundo plano (prazo 10 s; falha → log 1106); o `catch`
    de `AddCoreAsync` faz `RemoveAsync` best-effort antes de relançar.
  - `_gates` vira um conjunto fixo de 256 `SemaphoreSlim` (índice pelo hash do `deviceId`): memória limitada, ainda em série por equipamento.
  - `_lastSeen` não registra equipamento novo acima de `MaxTrackedDevices` (log 1104 uma vez).
  - `InMemoryPushCommandStore`: `TakeNextAsync`/`CountAsync` sem `GetOrAdd` (equipamento sem fila = vazio); fila vazia e sem `InFlight` é removida.
- `DeviceContactTracker(int maxDevices)` novo ctor (o sem parâmetros usa 10.000); acima do limite, equipamento novo é ignorado.
- Logs em `src/ControlId.Net/Access/Push/PushLog.cs` (novo, categoria `ControlId.Net`): 1101 ResultHandlerFailed (Error), 1102 CommandExpired
  (Warning), 1103 CommandOrphaned (Warning), 1104 TrackingLimitReached (Warning), 1105 UuidMissing (Warning), 1106 CancelRemoveFailed (Warning),
  1107 Purged (Information, quantidade). Nunca o payload.
- Métrica `controlid.push.commands` (Counter, tag `controlid.push.outcome`: `enqueued|delivered|completed|orphaned|expired|cancelled|rejected|timeout`)
  no `Meter` `ControlId.Net`.

**C2 — Store EF**
- `TakeNextAsync`: órfão removido com `ExecuteDeleteAsync` condicional (`Id` e `Status == InFlight`), reportado só se a exclusão apagou;
  vários `InFlight` (concorrência) → todos apagados, o mais antigo reportado. Próximo: candidato ordenado por `(CreatedAtTicks, Id)` e
  claim com `ExecuteUpdateAsync` condicional (`Id` e `Status == Pending`) → `InFlight`, `Uuid`, `DeliveredAtTicks`; 0 linhas = outra
  instância levou → tenta o seguinte (até 5 vezes).
- `AddAsync` mantém o `Max+1`; o desempate por `Id` garante ordem determinística. Sem mudança de esquema (sem migração nova).
- Implementa `IPushCommandStoreMaintenance` com dois `ExecuteDelete` precedidos da leitura dos removidos.
- `AddControlIdPushEntityFrameworkStore` valida na resolução que existe `IDbContextFactory<TContext>` (mensagem clara).

**C3 — ASP.NET Core: callbacks**
- `ControlIdEndpointConventionBuilderExtensions.AllowAnonymousControlIdCallbacks<TBuilder>(this TBuilder) where TBuilder : IEndpointConventionBuilder`
  (metadado interno). Sem token nem esse metadado → 401 + `WWW-Authenticate: Bearer` + log 2006 (Warning, uma vez por rota, dizendo como
  resolver). Com os dois, vale o token.
- `ControlIdTokenOptions`: `string? PreviousToken` (aceito junto com `Token`, para rotação) e `bool AllowTokenFallback = false`
  (com `DeviceToken` definido e devolvendo `null`, só cai no `Token` se ligado). O validador confere o cabeçalho `Bearer` **antes** de chamar `DeviceToken`.
- `public sealed class ControlIdCallbackOptions` (via `services.Configure<ControlIdCallbackOptions>`): `long MaxRequestBodySize = 4 MiB`,
  `Func<long, bool>? IsKnownDevice`. Corpo acima do limite → 413 (log 2010); `deviceId` desconhecido → 403 (log 2009), sem tocar
  `PushServer`, tracker ou handlers.
- Ordem: com token só global (sem `DeviceToken`) ou anônimo, autoriza **antes** de ler o corpo; com `DeviceToken`, lê o corpo com limite e então autoriza.
- Online: em `*_create.fcgi` e `new_rex_log.fcgi`, `device_id` da query diferente do do corpo → 403 (log 2007).
- Monitor: cada `IMonitorNotificationHandler` em `try/catch`; todos rodam; falha → log 2008 (Error, tipo do handler); se algum falhou, 500 ao final.
- `/push` e `/result`: `Cache-Control: no-store`.
- `MapControlIdPush` lança `InvalidOperationException` se `PushServer` não estiver registrado; `MapControlIdOnline` idem para `IOnlineAccessHandler`
  (via `IServiceProviderIsService`).
- Métricas no `Meter` `ControlId.Net`: `controlid.server.callback.requests` (tags `controlid.callback.kind`: `push|result|monitor|online`;
  `controlid.callback.outcome`: `ok|unauthorized|forbidden|too_large|bad_request|error`) e `controlid.server.callback.handler.duration` (s).
- Logs 2006–2010 em `src/ControlId.Net.AspNetCore/Log.cs`.

**C4 — ASP.NET Core: Push**
- `AddControlIdPush(this IServiceCollection, Action<PushServerOptions> configure)` (sobrecarga nova); o `LoggerFactory` vem do container se não informado.
- O `PushServer` recebe um despachante interno que, a cada resultado, cria um escopo e chama **todos** os `IPushResultHandler` registrados
  (falha de um não impede os outros). `AddControlIdPushResultHandler<T>` passa a `TryAddEnumerable(Scoped<IPushResultHandler, T>)`:
  dois handlers diferentes rodam os dois; handler registrado direto como scoped deixa de ser capturado pelo singleton.
- `ControlIdPushMaintenanceService` (hosted service interno, registrado pelo `AddControlIdPush`): chama `PurgeExpiredAsync` a cada `PurgeInterval`;
  exceção logada, nunca derruba o host.

**C5 — Núcleo: transporte, clientes e modelos**
- `public interface IControlIdClientDiagnostics { string? Session { get; } ControlIdCircuitState CircuitState { get; } void ResetCircuit(); }`
  implementada por `AccessClient` e `RepClient` (as interfaces publicadas não mudam). O health check por equipamento usa a interface.
- Circuit breaker: `Reset()` (→ `Closed`, log 1011); sonda sem veredito por mais de `2 × Timeout` (mínimo 1 s) conta como falha;
  log e métrica de transição emitidos fora do lock.
- `LogoutAsync`: falha de rede ou timeout vira log 1012 (Debug) e retorno normal; cancelamento do chamador continua lançando.
- Configuração: o transporte expõe uma geração de sessão interna; o `AccessClient` limpa `_missingConfigFields` quando ela muda (equipamento
  reiniciou/relogin) e ganha `public void ClearConfigurationFieldCache()`; campos retirados geram log 1010 (Information: equipamento, `seção.campo`).
- `SetAdministratorAsync(id, true)`: `modify_objects` em `user_roles`; só cria se não alterou nada (não apaga antes). `false` continua apagando.
- REP: atalho `RepClient(string baseAddress, string login = "admin", string password = "admin", RepMode mode = RepMode.Portaria671, string? serverCertificateSha256 = null)`
  substitui o atual, sem `AcceptAnyServerCertificate`. `ControlIdRepOptions.AcceptAnyServerCertificate` padrão `false`. O transporte loga
  1013 (Warning) ao ser criado com `AcceptAnyServerCertificate = true` e sem impressão digital.
- `RepAbout.ApiRevision` nunca lança: hexadecimal só com dígitos → como hoje; com `A–F` → `-1`. Novo `string ApiRevisionText` (ex.: `"10A"`).
- Enums lidos de inteiros do equipamento ganham `Unknown = 0` quando 0 não é um valor válido hoje (no mínimo `AccessEventType`, `AlarmCause`,
  `FaceImageErrorCode`, `VpnStatus`; levantar os demais casts); as propriedades derivadas usam `Internal/EnumValues.ToDefined<T>(int)` → `Unknown`.
- `ConfigureOnlineModeAsync`: XML doc sobre aplicação parcial em caso de falha (sem mudar o fluxo).

**C6 — DI e health checks**
- `IValidateOptions<ControlIdDeviceOptions>` e `IValidateOptions<ControlIdRepOptions>` internos chamando o `Validate()` do núcleo para os nomes
  registrados, com `ValidateOnStart()` por nome (confirmar que o `Microsoft.Extensions.Options` 10.x expõe `ValidateOnStart` no netstandard2.0;
  se não, registrar só o validador e documentar).
- `AddControlIdAccessDevices`/`AddControlIdRepDevices`: seção sem filhas → `InvalidOperationException` citando o caminho da seção.
- `ControlIdHealthCheckOptions`: `bool RequireDevices = true`, `TimeSpan CacheDuration = TimeSpan.Zero` (cache por equipamento compartilhado
  entre o check agregado e os por equipamento), `TimeSpan TotalTimeout = 30 s` (agregado). `SlowThreshold` negativo → `ArgumentOutOfRangeException`;
  comparação em `TimeSpan`.

## Tarefas

## T0 — Base da API 0.8.0-alpha.1 e higiene de build
- Arquivos: `src/*/PublicAPI.Shipped.txt`, `src/*/PublicAPI.Unshipped.txt` (4 projetos), `src/Directory.Build.props`,
  `src/ControlId.Net.Push.EntityFrameworkCore/ControlId.Net.Push.EntityFrameworkCore.csproj`, `Directory.Build.props`, `global.json` (novo),
  `Directory.Packages.props` (novo), todos os `*.csproj` (só `Version` dos `PackageReference`), `stryker-config.json`, `.github/workflows/ci.yml`.
- Dependências: nenhuma.
- Pronto: C0; `dotnet build -c Release -warnaserror` e `dotnet pack` passam com a base nova; `dotnet restore` sem rede não falha por NU1900;
  nenhuma versão de pacote muda.
- esforço: medium

## T1 — PushServer robusto (núcleo)
- Arquivos: `src/ControlId.Net/Access/Push/{PushServer,PushServerOptions(novo),PushQueueFullException(novo),IPushCommandStoreMaintenance(novo),InMemoryPushCommandStore,PushLog(novo)}.cs`,
  `src/ControlId.Net/Access/DeviceContactTracker.cs`, `src/ControlId.Net/Internal/Telemetry.cs`,
  `tests/ControlId.Net.Tests/{PushServerRobustnessTests(novo),PushStoreContractTests,DeviceContactTrackerTests}.cs`, `docs/plano-auditoria/api-T1.txt`.
- Dependências: nenhuma.
- Pronto: C1; testes com relógio falso (sem `Task.Delay`): handler do órfão que lança não impede a entrega do próximo; handler do resultado que lança
  devolve `true` e loga 1101; handler lento corta em `ResultHandlerTimeout`; `CommandTimeout` lança `TimeoutException` e tira da fila; TTL descarta
  e entrega o seguinte; fila cheia lança `PushQueueFullException`; `RequireUuid`; cancelamento libera o waiter mesmo com store travado;
  `PurgeExpiredAsync` falha waiters e chama o handler; 10.000+ `deviceId` distintos não crescem os dicionários; contrato de manutenção
  (abstrato, reaproveitado pelo EF); `PushServerTests` e `PushServerStoreTests` atuais passam; testes nos 3 alvos.
- esforço: high

## T2 — Store EF com claim atômico
- Arquivos: `src/ControlId.Net.Push.EntityFrameworkCore/{EntityFrameworkPushCommandStore,ServiceCollectionExtensions}.cs`,
  `tests/ControlId.Net.Tests/{EfPushStoreTests,EfPushStoreConcurrencyTests(novo)}.cs`, `docs/plano-auditoria/api-T2.txt`.
- Dependências: T1.
- Pronto: C2; contrato do store e de manutenção passam no SQLite; dois `PushServer` (dois stores sobre o mesmo arquivo SQLite) disputando o mesmo
  `deviceId` em paralelo nunca entregam o mesmo envio duas vezes e mantêm a ordem; órfão de outra instância é reportado uma vez; sem migração nova.
- esforço: high

## T3 — Callbacks fechados e endurecidos (ASP.NET Core)
- Arquivos: `src/ControlId.Net.AspNetCore/{ControlIdEndpointRouteBuilderExtensions,ControlIdOnlineEndpoints,ControlIdEndpointConventionBuilderExtensions,ControlIdTokenOptions,ControlIdCallbackOptions(novo),Log}.cs`,
  `src/ControlId.Net.AspNetCore/Internal/{ControlIdTokenValidator,CallbackBody(novo),CallbackTelemetry(novo)}.cs`,
  `tests/ControlId.Net.Tests/{AspNetCoreTests,AspNetCoreOnlineTests,AspNetCoreTokenTests,ControlIdTokenValidatorTests,AspNetCoreCallbackSecurityTests(novo)}.cs`,
  `tests/ControlId.Net.IntegrationTests/{PushOnlineEndToEndTests,PushStoreEndToEndTests,MonitorEndToEndTests,DeviceHealthEndToEndTests}.cs` (só o mapeamento),
  `docs/plano-auditoria/api-T3.txt`.
- Dependências: nenhuma (não mexe em `ControlIdServiceCollectionExtensions.cs`).
- Pronto: C3; rota sem token → 401 e log 2006 uma vez; `AllowAnonymous` → 200; `PreviousToken` aceito; `DeviceToken` não é chamado sem cabeçalho;
  sem fallback por padrão; corpo acima do limite → 413 sem ler tudo; `IsKnownDevice` → 403; Online com `device_id` divergente → 403;
  handler de Monitor que lança não impede os outros (500 no fim); `Cache-Control: no-store`; `Map*` sem `Add*` lança; métricas medidas com
  `MeterListener`; testes existentes ajustados para `AllowAnonymousControlIdCallbacks()` ou token, sem perder cobertura; integração verde.
- esforço: high

## T4 — Push no ASP.NET Core: opções, handlers por escopo e varredura
- Arquivos: `src/ControlId.Net.AspNetCore/{ControlIdServiceCollectionExtensions,ControlIdPushMaintenanceService(novo)}.cs`,
  `tests/ControlId.Net.Tests/AspNetCorePushOptionsTests.cs` (novo), `docs/plano-auditoria/api-T4.txt`.
- Dependências: T1, T3.
- Pronto: C4; `AddControlIdPush(o => ...)` chega ao `PushServer`; dois handlers rodam os dois; handler scoped registrado direto é resolvido por escopo
  (teste com `ValidateScopes = true`); hosted service varre no intervalo e sobrevive a exceção do store; `PurgeInterval = null` não registra varredura.
- esforço: medium

## T5 — Núcleo: diagnóstico, circuito, configuração, REP e modelos
- Arquivos: `src/ControlId.Net/{IControlIdClientDiagnostics(novo)}.cs`, `src/ControlId.Net/Internal/{ControlIdTransport,CircuitBreaker,Log,EnumValues(novo)}.cs`,
  `src/ControlId.Net/Access/AccessClient.cs`, `src/ControlId.Net/Rep/RepClient.cs`, `src/ControlId.Net/Rep/Models/RepModels.cs`,
  `src/ControlId.Net/Access/Models/*.cs` e `Access/Monitoring/*.cs` (só enums e propriedades derivadas),
  `src/ControlId.Net.DependencyInjection/ControlIdDeviceHealthCheck.cs` (troca do cast pela interface),
  `tests/ControlId.Net.Tests/{CircuitBreakerTests,LoggingTests,RepClientPhase2Tests,EnumFallbackTests(novo),ConfigurationCacheTests(novo),ClientDiagnosticsTests(novo)}.cs`,
  `docs/plano-auditoria/api-T5.txt`.
- Dependências: T6 (o arquivo do health check).
- Pronto: C5 (exceto o `ControlIdRepOptions`, que é da T6); `ResetCircuit` fecha; sonda abandonada reabre; `LogoutAsync` com rede caída não lança;
  cache de campos limpo após relogin e por `ClearConfigurationFieldCache`; `SetAdministratorAsync(true)` não apaga antes; atalho do REP valida o
  certificado e aceita a impressão digital; log 1013; `ApiRevision` com `0x10A` devolve `-1` e `ApiRevisionText = "10A"`; código desconhecido → `Unknown`.
- esforço: high

## T6 — DI e health checks
- Arquivos: `src/ControlId.Net.DependencyInjection/{ControlIdDeviceServiceCollectionExtensions,ControlIdRepOptions,ControlIdHealthCheck,ControlIdHealthChecksBuilderExtensions,ControlIdOptionsValidation(novo),ControlIdProbeCache(novo)}.cs`,
  `tests/ControlId.Net.Tests/{DependencyInjectionTests,DeviceHealthCheckTests,HealthCheckOptionsTests(novo)}.cs`, `docs/plano-auditoria/api-T6.txt`.
- Dependências: nenhuma.
- Pronto: C6 e `ControlIdRepOptions.AcceptAnyServerCertificate = false`; host com `BaseAddress` faltando falha no `StartAsync`, citando o equipamento;
  seção vazia lança no registro; zero equipamentos → `failureStatus`; `CacheDuration` evita a segunda sonda dentro da janela (agregado + por
  equipamento = uma requisição); `TotalTimeout` corta o agregado; `SlowThreshold` negativo lança.
- esforço: medium

## T7 — Samples seguros
- Arquivos: `samples/ControlId.Net.Server/{Program.cs,appsettings.json}`, `samples/ControlId.Net.Dashboard/{Program.cs,DashboardState.cs,DashboardOptions.cs,appsettings.json}`,
  `samples/docker-compose.yml`, `samples/ControlId.Net.Sample/Program.cs` (se usar o REP).
- Dependências: T3, T4, T6.
- Pronto: Server com `RequireControlIdToken` nas três rotas (token por configuração/user-secrets), API de operação atrás de chave de API (filtro do
  sample), `AddRateLimiter` nos callbacks e `/health` detalhado só local; Dashboard com health check real (`AddControlIdDeviceChecks`), senha só por
  variável de ambiente, `ConfigureCallbackSecurityAsync` + `RequireControlIdToken` no Monitor e `CloseLaterAsync` com o token de parada do app;
  `docker compose -f samples/docker-compose.yml up` continua funcionando sem passo manual; build dos samples sem avisos.
- esforço: medium

## T8 — Transporte: um único caminho de envio
- Arquivos: `src/ControlId.Net/Internal/ControlIdTransport.cs`, `tests/ControlId.Net.Tests/TransportPathParityTests.cs` (novo).
- Dependências: T5.
- Pronto: `SendCommandAsync` e `PostForStreamCoreAsync` usam o mesmo núcleo de retry, relogin e circuito (parâmetro `HttpCompletionOption`);
  sem mudança de comportamento: todos os testes de retry, circuito, stream de AFD e telemetria passam sem edição; teste novo prova que os dois
  caminhos reagem igual a 401, 5xx, timeout e circuito aberto.
- esforço: high

## T9 — Ponta a ponta
- Arquivos: `tests/ControlId.Net.IntegrationTests/{CallbackDefaultsEndToEndTests,PushRobustnessEndToEndTests}.cs` (novos).
- Dependências: T1–T4.
- Pronto: contra o simulador: callback sem token 401 e com `ConfigureCallbackSecurityAsync` 200; handler de resultado que lança não perde o
  comando seguinte; comando expirado não chega ao simulador; varredura do hosted service falha um `InFlight` abandonado.
- esforço: medium

## T10 — API pública, ADRs e documentação
- Arquivos: `src/*/PublicAPI.Unshipped.txt`, `src/*/CompatibilitySuppressions.xml` (atalho do `RepClient` e o que o pack acusar, com o motivo),
  `docs/plano-auditoria/` (apagar após juntar), `docs/adr/{0021-callbacks-fechados-por-padrao,0022-tls-do-rep-validado,0023-limites-da-fila-de-push}.md` (novos),
  `docs/adr/README.md`, `docs/adr/0019-*.md` e `0020-*.md` (status "complementada por"), `CHANGELOG.md`, `docs/guia.md`, `README.md`,
  `README.pt-BR.md`, `src/PACKAGE.md`, `SECURITY.md`, `docs/auditoria-2026-11-19.md` (situação de cada achado).
- Dependências: T0–T9.
- Pronto: build `-warnaserror` sem avisos (RS0016/RS0017 incluídos); `dotnet pack` passa contra a base 0.8.0-alpha.1 só com as supressões
  justificadas; CHANGELOG `[Unreleased]` com seção de mudanças que quebram (callbacks 401, TLS do REP, `AllowTokenFallback`, `RequireDevices`,
  seções vazias, prazos e TTL do Push) e o caminho de migração de cada uma; guia com callbacks e rate limiting, `PushServerOptions`, REP com
  impressão digital e `ResultStatusCodes[Degraded] = 503`; ADR 0023 registra o claim atômico e o limite do `SendAsync` com várias instâncias.
- esforço: medium

## Ondas
- **Onda 1:** T0, T1, T3, T6 — escopos de escrita disjuntos. ✅ Concluída (`a623d2f`).
- **Onda 2:** T2, T4, T5 — T2/T4 dependem de T1 (e T4 de T3); T5 depende de T6. ✅ Concluída (`8eb80fa`).
- **Onda 3:** T7, T8, T9. ✅ Concluída (`66c4a7a`): build Release só com RS0016 (226) e RS0017 (4), esperados até a T10;
  testes 799 (net8.0), 832 (net10.0), 721 (net48) e 42 de integração, todos passando; build do Dashboard sem avisos.
- **Onda 4:** T10. ✅ Concluída (commit pendente do líder): `dotnet build -c Release -warnaserror --no-incremental` com
  0 avisos e 0 erros; `dotnet test -c Release --no-build` com 799 (net8.0), 832 (net10.0), 721 (net48) e 42 de integração,
  0 falhas; `dotnet pack -c Release --no-build` passa contra a 0.8.0-alpha.1 só com a supressão do atalho antigo do
  `RepClient` (CP0002 em `net8.0` e `netstandard2.0`, ADR 0022). Nenhuma quebra não intencional acusada.

## Pendências
- Lançamento: provavelmente 0.9.0-alpha.1, por causa das mudanças que quebram (a versão e a base da validação de pacote
  não mudaram neste plano).
- Confirmar em hardware: `SetAdministratorAsync(id, true)` via `modify_objects` com `changes` (cria só se `changes == 0`) e
  a gravação/formato do `curl_bearer_token` pelo `ConfigureCallbackSecurityAsync`.
- `docker compose -f samples/docker-compose.yml up` não foi testado nesta máquina (sem Docker).

## Verificação (fim de cada onda)
Ondas 1–3 (RS0016 fica como aviso até a T10):
```
dotnet build -c Release --no-incremental
dotnet test -c Release --no-build
```
Onda 4:
```
dotnet build -c Release -warnaserror --no-incremental
dotnet test -c Release --no-build
dotnet pack -c Release --no-build -o artifacts
```
Onda 3 também: `dotnet build samples/ControlId.Net.Dashboard -c Release -warnaserror` e `docker compose -f samples/docker-compose.yml up` com o
dashboard recebendo eventos do simulador. Executores da mesma onda compartilham o projeto de testes: em erro de arquivo travado no build, repetir.

## Entrega
Branch `auditoria-2026-11`, um commit por onda (português, sem coautoria de IA), PR com a CI verde. A versão não muda neste plano; o lançamento
(provavelmente 0.9.0-alpha.1, por causa das quebras) fica como pendência.

## Fora do escopo
Fatiar `IAccessClient` e base comum `AccessClient`/`RepClient` (plano 1.0); unificar flags `0/1`, ler de volta o token gravado e CPF/CNPJ
numérico (exigem equipamento real); waiters do `SendAsync` entre instâncias; metadados de OpenAPI; serialização com source generator (AOT);
reescrita dos testes antigos que usam `Task.Delay`.

## Riscos
- **Callbacks fechados quebram toda instalação sem token** (inclusive firmware anterior à 6.22, que não envia o cabeçalho): log 2006 com a
  instrução, CHANGELOG com `AllowAnonymousControlIdCallbacks()` + restrição de rede como saída.
- **Atalho do REP passa a falhar no TLS** em quem dependia do certificado autoassinado: mensagem da exceção cita
  `ControlIdCertificates.GetServerCertificateSha256Async`.
- `CommandTtl` de 24 h descarta comandos de equipamentos offline por mais tempo; documentado e configurável.
- `ValidateOnStart` no netstandard2.0 a confirmar (C6).
- Gates em faixas serializam equipamentos que colidem no hash (custo pequeno; documentado no código).
- `ExecuteUpdate`/`ExecuteDelete` condicionais dependem do provedor; os testes cobrem só SQLite.
