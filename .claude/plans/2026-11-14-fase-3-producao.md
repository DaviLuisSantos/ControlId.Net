# PLAN — Fase 3 (produção) do ControlId.Net

## Contexto
Primeira fase aberta do `docs/PLANO.md` (§5.3: 4.1, 4.2, 3.5, 1.7+2.2, 1.8). A exploração mostrou que parte já existe:
retry só em leituras (ADR 0013), ActivitySource/Meter sem OpenTelemetry (ADR 0012), `[LoggerMessage]`, health check
agregado (0.6.0). Falta: circuit breaker por equipamento, histograma de espera na fila, health check por equipamento +
último contato, token Bearer nos callbacks, fila de Push durável + pacote EF Core.
Decisões do usuário: breaker próprio (sem Polly); token = validação no servidor + configuração genérica no cliente
(seção "a validar"); fila durável com store no núcleo + pacote EF Core.
Regras do repo (CONTRIBUTING.md): teste para comportamento novo, XML docs em português, API nova em `PublicAPI.Unshipped.txt`,
CHANGELOG `[Unreleased]`, zero avisos (CI usa `-warnaserror`), validação de pacote com base 0.7.0-alpha.1 (nada de membro
novo em interface pública já publicada), ADRs em `docs/adr/` (última: 0016). Commits em português, sem coautoria de IA.

## Contratos

**C1 — Circuit breaker (núcleo)**
- `ControlIdDeviceOptions.CircuitBreakerFailureThreshold : int` (padrão 0 = desligado; `const MaxCircuitBreakerFailureThreshold = 100`),
  `CircuitBreakerBreakDuration : TimeSpan` (padrão 30 s); `Validate()` cobre ambos. Espelhar em `ControlIdRepOptions` (DI) e `ToDeviceOptions()`.
- `public enum ControlIdCircuitState { Closed, Open, HalfOpen }`; `public sealed class ControlIdCircuitOpenException : ControlIdException { TimeSpan RetryAfter }`.
- `AccessClient.CircuitState` / `RepClient.CircuitState` (só nas classes concretas, não nas interfaces).
- Falha = `HttpRequestException`, timeout do cliente, HTTP 5xx (por tentativa). Não conta: 4xx, 401/relogin, `{"error"}`,
  `ControlIdAuthenticationException`, cancelamento do chamador. Resposta < 500 zera o contador.
- Aberto: falha antes do login e antes de `RequestSlots`. HalfOpen após `BreakDuration`: uma sonda; demais falham já;
  sonda ok fecha, falha reabre, cancelada libera a vez. Porta em `SendAsync` privado (comandos + login) e em
  `PostForStreamCoreAsync`; `LogoutAsync` passa direto. `ReadRetry.IsTransient` = false para a exceção de circuito.
- Relógio: `Stopwatch.GetTimestamp` atrás de gancho interno (sem `TimeProvider`).
- Métricas: `controlid.client.circuit.transitions` (tag `controlid.circuit.state`), `controlid.client.circuit.rejected`,
  `controlid.client.request.wait.duration` (Histogram s, todo `RequestSlots.WaitAsync`). Span: `error.type=circuit_open`.
  Logs: 1007 CircuitOpened (Warning), 1008 CircuitHalfOpen (Debug), 1009 CircuitClosed (Information).

**C2 — Último contato (núcleo, `ControlId.Access`)**
- `public sealed class DeviceContactTracker { void Record(long deviceId, DeviceContactSource source); DeviceContact? GetLastContact(long deviceId); IReadOnlyDictionary<long, DeviceContact> GetAll(); }`
- `public enum DeviceContactSource { Push, Monitor, Online }`; `public sealed class DeviceContact { DateTimeOffset At; DeviceContactSource Source; }`. Thread-safe, último vence.

**C3 — Health check por equipamento (DI)**
- `ControlIdHealthCheckOptions.MaxContactAge : TimeSpan?` (padrão null).
- `IHealthChecksBuilder AddControlIdDeviceChecks(this IHealthChecksBuilder, Action<ControlIdHealthCheckOptions>? configure = null, string namePrefix = "controlid", HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)`.
- Um check por equipamento: `{prefix}:access:{nome}` / `{prefix}:rep:{nome}` (nome vazio = `default`), registrado de forma
  adiada via `IConfigureOptions<HealthCheckServiceOptions>` lendo o `ControlIdRegistry`.
- Circuito Open → `failureStatus` sem requisição. Senão sonda atual (`system_information`/`get_about`): falha → failureStatus;
  lento → Degraded; `MaxContactAge` vencido → Degraded; senão Healthy. Data: `serial`, `firmware`, `deviceId`, `latencyMs`,
  `circuit`, `lastContact` (ISO 8601), `lastContactSource`. Tracker opcional (`GetService`); vínculo via
  `AccessSystemInformation.DeviceId` convertido para long. REP sem último contato. Check agregado atual inalterado.

**C4 — Token Bearer nos callbacks**
- AspNetCore: `public sealed class ControlIdTokenOptions { string? Token; Func<long, string?>? DeviceToken; }`;
  `public static class ControlIdEndpointConventionBuilderExtensions { TBuilder RequireControlIdToken<TBuilder>(this TBuilder, string token); TBuilder RequireControlIdToken<TBuilder>(this TBuilder, Action<ControlIdTokenOptions>); } where TBuilder : IEndpointConventionBuilder`.
- A extensão só adiciona metadado; os handlers do pacote o leem (sem endpoint filter). Validador interno
  `Internal/ControlIdTokenValidator.cs`: sem header/esquema errado/token errado → 401 + `WWW-Authenticate: Bearer`;
  `FixedTimeEquals` sobre SHA-256 dos dois lados; `DeviceToken(id)` não nulo vence `Token`; nenhum configurado → 401.
  deviceId: Push = query `deviceId`; Monitor = `DeviceId` da notificação (parse antes, handlers só depois); Online =
  `device_id` da query/form. Log 2005 (Warning: rota, deviceId; nunca o token).
- Núcleo: `public static class AccessClientCallbackSecurityExtensions { Task ConfigureCallbackSecurityAsync(this IAccessClient client, string? bearerToken, string? pinnedCertificate = null, string section = "general", CancellationToken cancellationToken = default); }`
  → `SetConfigurationAsync({section:{curl_bearer_token, curl_pinned_certificate}})`; null omite, `""` limpa, ambos null → `ArgumentException`.
  XML doc: "seção e formato a validar no equipamento (firmware 6.22.3+)".
- Simulador: com `general.curl_bearer_token` não vazio, Push, Online e Monitor mandam `Authorization: Bearer`.

**C5 — Fila de Push durável (núcleo, `ControlId.Access.Push`, netstandard2.0)**
- `public interface IPushCommandStore { Task AddAsync(PushEnvelope, CancellationToken); Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken); Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken); Task<bool> RemoveAsync(Guid id, CancellationToken); Task<int> CountAsync(long deviceId, CancellationToken); }`
  (chamado em série por equipamento; paralelo entre equipamentos). `TakeNextAsync` remove o InFlight atual (→ `Orphan`) e marca o
  Pending mais antigo como InFlight com o uuid (→ `Next`). `CompleteAsync`: uuid null de qualquer lado = coincide (como hoje).
- `public sealed class PushEnvelope { Guid Id; long DeviceId; bool Batch; IReadOnlyList<string> Endpoints; string Payload; PushEnvelopeStatus Status; string? Uuid; DateTimeOffset CreatedAt; DateTimeOffset? DeliveredAt; }` (ctor público).
  `public enum PushEnvelopeStatus { Pending, InFlight }`; `public readonly struct PushTake { PushEnvelope? Orphan; PushEnvelope? Next; }`.
- `public sealed class InMemoryPushCommandStore : IPushCommandStore`.
- `public interface IPushResultHandler { Task HandleAsync(PushCompletion completion, CancellationToken ct); }`;
  `public sealed class PushCompletion { Guid CommandId; long DeviceId; IReadOnlyList<PushResult> Results; string? Error; }`.
- `PushServer`: novo ctor `(IPushCommandStore store, IPushResultHandler? unclaimedResultHandler = null)` (o sem parâmetros usa
  memória); `Task<Guid> EnqueueAsync(long, PushCommand, ct)`, `Task<Guid> EnqueueBatchAsync(long, IEnumerable<PushCommand>, ct)`,
  `Task<string> HandlePushAsync(long, string?, ct)`, `Task<bool> HandleResultAsync(long, string?, string body, ct)`,
  `Task<int> GetPendingCountAsync(long, ct)`. `SendAsync`/`SendBatchAsync` iguais por fora (TCS em `ConcurrentDictionary<Guid,…>`;
  cancelar → `RemoveAsync` e depois cancela). `SemaphoreSlim` por equipamento no lugar do `lock`. Payload serializado no enqueue.
  Resultado sem TCS → `IPushResultHandler` (no máximo uma vez, depois do `CompleteAsync`). Órfão = falha, **nunca reenviado**.
  Métodos síncronos mantidos (bloqueiam sobre o async; XML doc recomenda os `*Async`). Uma instância por store (documentado).
- AspNetCore: rotas usam os `*Async`; `AddControlIdPush()` usa `IPushCommandStore` registrado ou memória + `IPushResultHandler`
  opcional; novo `AddControlIdPushResultHandler<THandler>()` (handler scoped via `IServiceScopeFactory`).
- Pacote novo `src/ControlId.Net.Push.EntityFrameworkCore` (**net10.0**: EF Core 10 exige net10; 8/9 fora de suporte),
  `Microsoft.EntityFrameworkCore.Relational` 10.0.x, `PackageValidationBaselineVersion` vazio no csproj, PublicAPI próprio.
  `ModelBuilder ApplyControlIdPush(this ModelBuilder, string table = "controlid_push_commands", string? schema = null)` (entidade interna
  `PushCommandRecord`, PK Id, índice (DeviceId, Status, CreatedAt)); `IServiceCollection AddControlIdPushEntityFrameworkStore<TContext>(this IServiceCollection) where TContext : DbContext`
  (store singleton interno sobre `IDbContextFactory<TContext>`).

**C6 — Arquivos compartilhados**
`PublicAPI.Unshipped.txt` (núcleo, DI, AspNetCore), `CHANGELOG.md`, `docs/guia.md`, ADRs, READMEs e `docs/PLANO.md` só na T10.
Cada tarefa que cria API grava as linhas em `docs/plano-fase3/api-T<n>.txt` (`<projeto>: <linha>`); o RS0016 fica como aviso
até a T10 (builds das ondas 1–2 sem `-warnaserror`).

## Tarefas

## T1 — Circuit breaker e espera na fila (4.1, 4.2)
- Arquivos: `src/ControlId.Net/Internal/{ControlIdTransport,CircuitBreaker(novo),RequestSlots,Telemetry,Log,ReadRetry}.cs`,
  `src/ControlId.Net/{ControlIdDeviceOptions,ControlIdCircuitState(novo),ControlIdCircuitOpenException(novo),ControlIdTelemetry}.cs`,
  `src/ControlId.Net/Access/AccessClient.cs`, `src/ControlId.Net/Rep/RepClient.cs`,
  `src/ControlId.Net.DependencyInjection/ControlIdRepOptions.cs`, `tests/ControlId.Net.Tests/{CircuitBreakerTests(novo),TelemetryTests}.cs`,
  `docs/plano-fase3/api-T1.txt`.
- Dependências: nenhuma.
- Pronto: C1; testes: desligado por padrão, abre no limite sem tocar o handler, `RetryAfter`, 4xx/`{"error"}`/401/cancelamento não contam,
  5xx/timeout contam, sucesso zera, HalfOpen com sonda única concorrente, sonda que falha reabre, sonda cancelada libera,
  retry de leitura não repete com circuito aberto, AFD em stream e login passam pela porta, métricas (transições, rejeições,
  espera), `Validate()`, binding de `ControlIdRepOptions`.
- modelo: sonnet · esforço: high

## T2 — Store de Push no núcleo (1.8)
- Arquivos: `src/ControlId.Net/Access/Push/{PushServer,IPushCommandStore(novo),PushEnvelope(novo),InMemoryPushCommandStore(novo),IPushResultHandler(novo),PushCompletion(novo)}.cs`,
  `tests/ControlId.Net.Tests/{PushStoreContractTests(novo, abstrata),PushServerStoreTests(novo)}.cs`, `docs/plano-fase3/api-T2.txt`.
- Dependências: nenhuma.
- Pronto: C5 (parte do núcleo); `PushServerTests` atual passa sem mudanças; contrato do store em memória (FIFO, órfão, uuid
  diferente, remove, count); `EnqueueAsync` → handler; reinício simulado (novo `PushServer`, mesmo store) entrega o pendente e o
  resultado vai ao handler; cancelar remove; órfão não reenviado; equipamentos em paralelo; sync ≡ async; testes nos 3 alvos.
- modelo: sonnet · esforço: high

## T3 — Token: extensão do cliente e simulador (1.7, 2.2)
- Arquivos: `src/ControlId.Net/Access/AccessClientCallbackSecurityExtensions.cs` (novo),
  `tools/ControlId.Net.Simulator/{PushClient,OnlineClient,MonitorDispatcher,CallbackAuth(novo)}.cs`,
  `tests/ControlId.Net.Tests/CallbackSecurityTests.cs` (novo), `docs/plano-fase3/api-T3.txt`.
- Dependências: nenhuma.
- Pronto: C4 (núcleo + simulador); corpo do `set_configuration` com seção padrão e personalizada, null omitido, `""` limpa,
  ambos null lança; simulador manda o header quando configurado (confirmar o arquivo que envia o Monitor; se não for
  `MonitorDispatcher.cs`, parar e reportar).
- modelo: sonnet · esforço: medium

## T4 — Rastreio do último contato (3.5, núcleo)
- Arquivos: `src/ControlId.Net/Access/DeviceContactTracker.cs` (novo, 3 tipos), `tests/ControlId.Net.Tests/DeviceContactTrackerTests.cs` (novo),
  `docs/plano-fase3/api-T4.txt`.
- Dependências: nenhuma.
- Pronto: C2; testes de concorrência e "último vence".
- modelo: sonnet · esforço: low

## T5 — Token: tipos e validador no ASP.NET Core
- Arquivos: `src/ControlId.Net.AspNetCore/{ControlIdTokenOptions(novo),ControlIdEndpointConventionBuilderExtensions(novo),Internal/ControlIdTokenValidator(novo)}.cs`,
  `tests/ControlId.Net.Tests/ControlIdTokenValidatorTests.cs` (novo), `docs/plano-fase3/api-T5.txt`.
- Dependências: nenhuma.
- Pronto: C4 (tipos + validador, ainda sem ligar nas rotas); testes unitários: sem header, `Basic`, token errado, token de
  outro equipamento, `DeviceToken` vence `Token`, nada configurado → falha; metadado legível a partir do endpoint.
- modelo: sonnet · esforço: medium

## T6 — ASP.NET Core: liga token, último contato e rotas async
- Arquivos: `src/ControlId.Net.AspNetCore/{ControlIdEndpointRouteBuilderExtensions,ControlIdOnlineEndpoints,ControlIdServiceCollectionExtensions,Log}.cs`,
  `tests/ControlId.Net.Tests/AspNetCoreTokenTests.cs` (novo), `docs/plano-fase3/api-T6.txt`.
- Dependências: T2, T4, T5.
- Pronto: handlers aplicam o validador quando o grupo tem o metadado (Push, Monitor, Online), log 2005; `TryAddSingleton<DeviceContactTracker>()`
  nos `AddControlId*` e registro em `/push`, `/result`, Monitor (qualquer notificação com DeviceId) e Online; rotas usam
  `HandlePushAsync`/`HandleResultAsync`; `AddControlIdPush()` com store/handler do container; `AddControlIdPushResultHandler<THandler>()`;
  testes TestServer: 401 sem/errado, 200 com token, grupo sem token inalterado, tracker alimentado, handler scoped por escopo;
  `AspNetCoreTests`/`AspNetCoreOnlineTests` passam.
- modelo: sonnet · esforço: high

## T7 — Health check por equipamento (3.5, DI)
- Arquivos: `src/ControlId.Net.DependencyInjection/{ControlIdHealthChecksBuilderExtensions,ControlIdHealthCheck,ControlIdDeviceHealthCheck(novo)}.cs`,
  `tests/ControlId.Net.Tests/DeviceHealthCheckTests.cs` (novo), `docs/plano-fase3/api-T7.txt`.
- Dependências: T1, T4.
- Pronto: C3; testes: um registro por equipamento, ordem de registro indiferente, nomes com o tipo, data completa,
  lento → Degraded, falha → failureStatus, circuito aberto sem requisição, `MaxContactAge` → Degraded, tracker ausente tolerado,
  check agregado inalterado.
- modelo: sonnet · esforço: medium

## T8 — Pacote ControlId.Net.Push.EntityFrameworkCore (1.8)
- Arquivos: `src/ControlId.Net.Push.EntityFrameworkCore/` (novo: csproj net10.0, `ModelBuilderExtensions.cs`,
  `ServiceCollectionExtensions.cs`, `EntityFrameworkPushCommandStore.cs`, `PushCommandRecord.cs`, `PublicAPI.Shipped.txt` vazio,
  `PublicAPI.Unshipped.txt`), `ControlId.Net.sln`, `tests/ControlId.Net.Tests/ControlId.Net.Tests.csproj`
  (ref. ao projeto + `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x só em net10.0; `Compile Remove` dos testes EF nos outros alvos),
  `tests/ControlId.Net.Tests/EfPushStoreTests.cs` (novo, herda `PushStoreContractTests`, SQLite in-memory com conexão aberta).
- Dependências: T2.
- Pronto: C5 (EF); contrato do store passa em SQLite; reinício simulado com o mesmo banco entrega o pendente; `dotnet pack` do
  projeto sem erro de validação.
- modelo: sonnet · esforço: high

## T9 — Integração de ponta a ponta com o simulador
- Arquivos: `tests/ControlId.Net.IntegrationTests/{CallbackTokenEndToEndTests,DeviceHealthEndToEndTests,CircuitBreakerEndToEndTests,PushStoreEndToEndTests}.cs` (novos).
- Dependências: T1–T8.
- Pronto: token via `ConfigureCallbackSecurityAsync` aceito e recusado (Push e Monitor); health por equipamento com `lastContact`
  após um Push; circuito abre com o simulador parado e fecha ao voltar; comando pendente sobrevive a um novo `PushServer` com o
  mesmo store.
- modelo: sonnet · esforço: medium

## T10 — API pública, ADRs e documentação
- Arquivos: `src/ControlId.Net/PublicAPI.Unshipped.txt`, `src/ControlId.Net.DependencyInjection/PublicAPI.Unshipped.txt`,
  `src/ControlId.Net.AspNetCore/PublicAPI.Unshipped.txt`, `docs/plano-fase3/` (apagar após juntar),
  `docs/adr/{0017-circuit-breaker-proprio,0018-health-check-por-equipamento,0019-token-bearer-nos-callbacks,0020-fila-de-push-duravel}.md` (novos),
  `docs/adr/README.md`, `CHANGELOG.md`, `docs/guia.md`, `README.md`, `README.pt-BR.md`, `docs/PLANO.md` (Fase 3 concluída, nota do ci.yml corrigida),
  `SECURITY.md`, `.github/workflows/publish.yml` (incluir o pacote EF, se a lista de pacotes for explícita).
- Dependências: T9.
- Pronto: build `-warnaserror` sem avisos (inclui RS0016/RS0017); ADRs 0017–0020 (0020 registra a exceção net10.0 ao ADR 0014);
  CHANGELOG `[Unreleased]` com tudo, incluindo a mudança sutil (alterar `Body` após enfileirar não tem mais efeito); guia com
  breaker, health por equipamento, token e store EF; `dotnet pack` ok.
- modelo: sonnet · esforço: medium

## Ondas
- **Onda 1:** T1, T2, T3, T4, T5 — ✅ concluída (verificação passou: 565/565/543 + 32 integração)
- **Onda 2:** T6, T7, T8 — ✅ concluída (verificação passou: 587/603/553 + 32 integração)
- **Onda 3:** T9 — ✅ concluída (verificação passou: 587/603/553 + 37 integração)
- **Onda 4:** T10 — ✅ concluída (verificação passou: build -warnaserror 0 avisos; 587/603/553 + 37 integração; pack com 4 pacotes)

## Verificação (fim de cada onda)
Ondas 1–3:
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
Executores da mesma onda compartilham o projeto de testes: em erro de arquivo travado no build, repetir; a verificação da onda é a que vale.

## Entrega
Branch `fase-3-producao`, um commit por onda (português, sem coautoria de IA), PR ao final com a CI verde.

## Riscos
- Token/certificado do lado do equipamento não documentados (seção e se valem para `/push`): fica "a validar" no XML doc e no guia.
- Métodos síncronos do `PushServer` bloqueiam sobre async (net48 com SynchronizationContext): documentar.
- Health check vincula ao tracker por `system_information.device_id` (string): sem conversão, sem `lastContact`.
- Fila durável não suporta várias instâncias no mesmo banco (documentado, fora do escopo).
