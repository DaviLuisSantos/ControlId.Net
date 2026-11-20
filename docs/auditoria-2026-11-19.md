# Auditoria em camadas — ControlId.Net 0.8.0-alpha.1 (2026-11-19)

Revisão somente leitura feita sobre `main` em `30da687`. Camadas: transporte/núcleo, API pública e modelos,
resiliência/Push/segurança, ASP.NET Core/DI/health checks/samples e engenharia (build/CI/testes). Os achados centrais
foram conferidos no código; as linhas citadas são desse commit.

Prioridade: **P1** corrigir antes da 0.8.0 estável · **P2** próxima iteração · **P3** melhoria/higiene.

## O que já está bom (verificado)

- Token Bearer comparado em tempo constante (`SHA256` + `FixedTimeEquals`); nenhum token, sessão, senha ou corpo em log/telemetria.
- `CancellationToken` em todos os métodos assíncronos públicos; `IAsyncEnumerable` nas leituras grandes.
- Store EF usa `IDbContextFactory` (sem `DbContext` singleton); `ExecuteDeleteAsync` faz do `CompleteAsync` um vencedor único.
- Governança de API (`PublicApiAnalyzers` + `EnablePackageValidation`), SourceLink, snupkg, SBOM e atestação de proveniência na publicação.
- Retry só em leituras (ADR 0013), circuit breaker com sonda única e testes de concorrência.

---

## P1

### 1. Callbacks de Push, Monitor e Online abertos por padrão
- `ControlIdTokenValidator.Authorize` libera quando o endpoint não tem metadado (`options is null || ...`):
  `src/ControlId.Net.AspNetCore/Internal/ControlIdTokenValidator.cs:57-68`.
- O sample `samples/ControlId.Net.Server/Program.cs` mapeia as três rotas sem `RequireControlIdToken` e expõe
  `/equipamentos/{id}/abrir-porta` sem autenticação, com instrução de subir em `0.0.0.0`. O `docs/guia.md` repete o padrão.
- Impacto: qualquer host da rede drena a fila (`GET /push?deviceId=N`), forja resultados (`POST /result`), lê fotos via
  `user_get_image.fcgi`, injeta notificações de Monitor que os handlers transformam em escrita. Um `set_configuration`
  enfileirado com `curl_bearer_token` vaza o próprio token.
- Fazer: fail-closed com opt-in explícito (`AllowAnonymousCallbacks`), ou no mínimo log de Warning na inicialização para
  grupo `MapControlId*` sem token; samples e guia sempre com `.RequireControlIdToken(...)`. É mudança de comportamento:
  registrar no CHANGELOG e revisar o ADR 0019.

### 2. Falha do `IPushResultHandler` perde o próximo comando (e o resultado)
- `HandlePushAsync` (`src/ControlId.Net/Access/Push/PushServer.cs:118-152`): `TakeNextAsync` já marcou o próximo envelope
  como `InFlight` quando o handler do órfão é aguardado (linhas 143-148). Se o handler lançar, o payload não é devolvido,
  o equipamento recebe 500 e, no próximo `/push`, esse comando nunca entregue vira órfão e falha.
- `HandleResultAsync` (linhas 186-221): `CompleteAsync` remove o envelope e só depois chama o handler; exceção ou
  `RequestAborted` (o equipamento fecha a conexão logo após enviar) perdem o resultado de vez — inclusive no store "durável".
- Fazer: nunca propagar exceção do handler para a resposta do equipamento; entregar via `Channel<PushCompletion>` +
  `BackgroundService` com escopo próprio e o token do host, não `context.RequestAborted`; logar com EventId.

### 3. Memória ilimitada por `deviceId` arbitrário; fila sem limite nem validade
- `_gates`, `_lastSeen` (`PushServer.cs:36-38`, `120`, `453`), `InMemoryPushCommandStore._devices` (`GetOrAdd` em
  `TakeNextAsync`, linha 42) e `DeviceContactTracker` criam entrada para qualquer `deviceId` da query e nunca removem.
  Com o item 1, `GET /push?deviceId=<aleatório>` é um vazamento remoto de memória.
- Não há `MaxPendingPerDevice` nem TTL: equipamento offline por dias acumula comandos (até ~1 MB cada, fotos) e, ao
  voltar, executa ordens obsoletas (abrir porta, reboot).
- Fazer: `PushServerOptions { MaxPendingPerDevice, CommandTtl, MaxTrackedDevices }`, limpeza de filas vazias,
  `PurgeExpiredAsync` no store e allowlist opcional de `deviceId` (`Func<long,bool> IsKnownDevice` → 400).

### 4. `SendAsync` do Push sem prazo; `InFlight` eterno
- `SendAsync`/`SendBatchAsync` só terminam com `/result` ou cancelamento do chamador (`PushServer.cs:55-79`, `361-372`).
  O órfão só é recolhido quando o equipamento volta a chamar `/push`; se ele morrer, waiter e linha EF ficam para sempre.
- Fazer: `DefaultCommandTimeout` (via `CancelAfter`) e `InFlightTimeout` com varredura que falha o envio para o waiter/handler.

### 5. `new RepClient(string)` desliga a validação TLS
- `src/ControlId.Net/Rep/RepClient.cs:35-44` grava `AcceptAnyServerCertificate = true`; o `ControlIdRepOptions` do DI tem
  o mesmo padrão. Login e senha de admin vão para qualquer certificado (MITM), embora a fixação por SHA-256 já exista.
- Fazer: parâmetro `pinnedSha256` no atalho, padrão `false` no DI e Warning no log quando `AcceptAny` estiver ligado.
  Mudança de comportamento: documentar a migração com `ControlIdCertificates.GetServerCertificateSha256Async`.

---

## P2

### Segurança e callbacks
6. **Online: autentica um equipamento e entrega outro.** `HandleEnrollAsync` autoriza com o `device_id` da query e repassa o
   `DeviceId` do corpo; `HandleExitButtonAsync` idem (`ControlIdOnlineEndpoints.cs:139-171`, `192-200`). Com token por
   equipamento, o token de A grava cadastro como B. Responder 403 quando query e corpo divergirem.
7. **Corpo lido antes da autorização, sem limite e sem rate limiting.** Monitor lê o corpo inteiro antes do token
   (`ControlIdEndpointRouteBuilderExtensions.cs:124` vs `143`); Online copia para `MemoryStream` sem teto
   (`ControlIdOnlineEndpoints.cs:111-122`). Aplicar `MaxRequestBodySize` por endpoint, `RequireRateLimiting` documentado e
   validar o token global antes de ler o corpo quando não houver `DeviceToken`.
8. **Handlers do Monitor sem isolamento.** O `foreach` (`ControlIdEndpointRouteBuilderExtensions.cs:153-156`) não tem
   `try/catch`: um handler que falha devolve 500 e impede os seguintes. Isolar por handler e logar o tipo.
9. **Token.** `DeviceToken(id)` (síncrono) roda antes de checar se há cabeçalho; `null` cai no token global (um vazamento
   autentica o parque todo); sem rotação (`PreviousToken`) nem `IOptionsMonitor`; `ConfigureCallbackSecurityAsync` não relê a
   configuração para confirmar que o firmware aceitou.
10. **Cancelamento não atômico.** `AddCoreAsync` cancelado não compensa o que entrou no store; `CancelAsync` só cancela o
    waiter depois do `RemoveAsync` sem prazo (`PushServer.cs:399-451`). Cancelar o waiter já e remover em segundo plano
    com timeout; no `catch` do enqueue, `RemoveAsync` best-effort.

### Push durável (EF)
11. **Sem claim atômico** (`EntityFrameworkPushCommandStore.cs:55-87`): duas instâncias entregam o mesmo comando;
    `RemoveRange(inFlight)` apaga o envio que a outra espera; `Max(CreatedAtTicks)+1` empata sob concorrência e o índice
    não é único. O ADR 0020 proíbe várias instâncias, mas nada impede em runtime. Fazer: `RowVersion` + `ExecuteUpdate`
    condicional, índice único `(DeviceId, CreatedAtTicks)`, `ThenBy(Id)`; ou lease no banco que falha na partida.

### DI, opções e health checks
12. **Configuração inválida só aparece no primeiro uso.** `Validate()` é interno e roda no construtor do transporte;
    `AddControlIdAccessDevices` com seção errada não registra nada e não avisa; `MapControlIdPush/Online` sem o `Add*`
    correspondente estoura 500 no primeiro callback. Fazer `IValidateOptions` + `ValidateOnStart` e checar serviços no `Map*`.
13. **Health check agregado `Healthy` com zero equipamentos** (`ControlIdHealthCheck.cs:66-69`): justamente o erro de
    configuração que deveria pegar. Opção `RequireDevices`.
14. **Health check sem cache e sem prazo agregado**; `Degraded` vira HTTP 200 no `MapHealthChecks` padrão; registrar os dois
    checks sonda cada equipamento duas vezes; `SlowThreshold` negativo não é validado. Cache curto por equipamento, tags
    `ready`/`live` e `ResultStatusCodes[Degraded] = 503` no guia.

### Núcleo e modelos
15. **`RepAbout.ApiRevision` lança** em getter público quando a versão em hexadecimal tem `A-F`
    (`int.Parse(FirmwareVersion.ToString("X"))`, `src/ControlId.Net/Rep/Models/RepModels.cs:29`; ex.: `0x10A`).
16. **Enums sem `Unknown = 0`** e casts cegos (`AccessEventType`, `AlarmCause`, `FaceImageErrorCode`, `VpnStatus`): código de
    firmware novo vira valor indefinido. Converter com `Enum.IsDefined` → `Unknown`.
17. **Leitura de configuração tolerante demais.** O cache `_missingConfigFields` (`AccessClient.cs:41`) nunca é invalidado
    num cliente singleton (após atualizar o firmware, os campos continuam omitidos); valor fora do formato vira `null`
    igual a "ausente"; nada informa o que faltou, e o padrão ler-alterar-gravar pode gravar incompleto. Devolver os
    campos ausentes, limpar o cache em login/reboot.
18. **`LogoutAsync` lança `HttpRequestException` crua**, fora do contrato `ControlIdException` e contra o comentário
    "best-effort" (`ControlIdTransport.cs:92-112`).
19. **Retry, relogin e circuito duplicados** entre `SendCommandAsync` e `PostForStreamCoreAsync`
    (`ControlIdTransport.cs:165-285` e `316-385`, ~100 linhas paralelas): uma correção feita num caminho e esquecida no
    outro é questão de tempo. Unificar com `HttpCompletionOption` como parâmetro.
20. **Operações compostas não atômicas.** `SetAdministratorAsync` apaga o papel antes de criar (`AccessClient.cs:479-487`);
    `ConfigureOnlineModeAsync` faz três passos sem compensação.
21. **Flags com três codificações** (`true`, `1`, `"1"`) para o mesmo conceito, conforme o método. Centralizar.
22. **Fachada grande.** `IAccessClient` com ~164 membros e `AccessClient.cs` com 1.695 linhas em 12 domínios, 92 literais de
    rota; `Session`/`CircuitState` fora das interfaces (o health check faz cast para o concreto). Para a próxima major:
    interfaces por domínio compostas em `IAccessClient`, `partial class`, catálogo de rotas e `ControlIdObjects.NameOf<T>()`;
    já agora, uma `IControlIdClientDiagnostics` com `Session`, `CircuitState` e `ResetCircuit()`.

---

## P3

- **Circuit breaker:** sem `Reset()` público; sonda sem prazo pode prender o `HalfOpen` se o `HttpClient` tiver
  timeout infinito; log e métrica emitidos sob o lock (`CircuitBreaker.cs:213-236`).
- **Pareamento por `uuid` tolerante:** sem `uuid`, qualquer `/result` completa o envio em voo (replay/atraso). Opção `RequireUuid`.
- **`GET /push` sem `Cache-Control: no-store`**, embora retire da fila e carregue dados sensíveis.
- **Handler de resultado scoped registrado direto** (fora de `AddControlIdPushResultHandler`) é resolvido no provider raiz
  pelo singleton `PushServer` (`ControlIdServiceCollectionExtensions.cs:30-32`): captive dependency. Resolver por escopo sempre.
- **Sample Dashboard:** `/health` fixo em `Healthy`, `admin/admin` versionado, Monitor sem token, `_ = CloseLaterAsync(...)` fire-and-forget.
- **Telemetria só de saída:** callbacks (Push/Monitor/Online) sem `Meter`/`ActivitySource` (recebidos, 401, latência do handler).
- **OpenAPI:** rotas sem metadado (`WithTags`, `Produces`, `ExcludeFromDescription`).
- **CPF/CNPJ gravados como número** no REP: confirmar em equipamento real que zeros à esquerda são preservados.

## Build, CI e testes

- `-warnaserror` transforma o **NU1900** (auditoria de vulnerabilidade do NuGet) em erro: sem rede o build falha
  (reproduzido localmente). Adicionar `<WarningsNotAsErrors>NU1900</WarningsNotAsErrors>` ou `NuGetAudit` só na CI.
- Sem `global.json` (versão do SDK livre entre máquinas e CI) e sem `Directory.Packages.props` (versões `10.0.15`
  repetidas em vários csproj); sem `packages.lock.json`.
- Avisos como erro só na CI: localmente RS0016/RS0017 passam. Considerar `TreatWarningsAsErrors` em Release.
- Cobertura só reportada, sem limite mínimo; Stryker com `"break": 0` (68% de mutation score sem trava).
- ~30 esperas por tempo (`Task.Delay`) em 15 arquivos de teste, a maioria de integração: fonte provável de instabilidade.
  Preferir relógio falso (`TimeProvider`/gancho existente) e espera por condição.
- Faltam testes para: handler de resultado que lança, flood de `deviceId`, TTL, duas instâncias EF no mesmo banco,
  sonda abandonada do circuito, zero equipamentos no health check, divergência de `device_id` no modo online.

## Adições sugeridas (roadmap)

1. `PushServerOptions` (timeout, TTL, limites, `RequireUuid`) + serviço de varredura e métricas de fila (profundidade, idade do mais antigo, órfãos, expirados).
2. Entrega de resultados por `Channel` + `BackgroundService` (outbox simples).
3. Callbacks fail-closed, rate limiting e limite de corpo prontos no `Map*`.
4. Validação de opções na partida (`ValidateOnStart`) e checagem de serviços no `Map*`.
5. `IControlIdClientDiagnostics` (sessão, circuito, reset) e telemetria de entrada no ASP.NET Core.
6. Serialização com source generator (fase 2 do ADR 0015) para desbloquear Native AOT.
7. Fatiamento de `IAccessClient` e base comum `AccessClient`/`RepClient` na próxima major.
8. `global.json`, gerenciamento central de pacotes e limites de cobertura/mutação na CI.

---

## Situação (2026-11-19)

Correções feitas na branch `auditoria-2026-11`, pelo plano `.claude/plans/2026-11-19-auditoria.md`, em quatro ondas:
`a623d2f` (onda 1: T0, T1, T3, T6), `8eb80fa` (onda 2: T2, T4, T5), `66c4a7a` (onda 3: T7, T8, T9) e a onda 4 (T10: API
pública, ADRs 0021–0023 e documentação). As mudanças que quebram estão no [CHANGELOG](../CHANGELOG.md), com a migração.

### P1

| Achado | Situação |
| --- | --- |
| 1. Callbacks abertos por padrão | Resolvido: T3 (`a623d2f`), fechados por padrão com `AllowAnonymousControlIdCallbacks()` explícito; samples na T7 e ponta a ponta na T9 (`66c4a7a`); [ADR 0021](adr/0021-callbacks-fechados-por-padrao.md) |
| 2. Falha do `IPushResultHandler` perde o próximo comando | Resolvido: T1 (`a623d2f`), handler isolado com prazo próprio e log 1101; ponta a ponta na T9 |
| 3. Memória ilimitada por `deviceId`; fila sem limite nem validade | Resolvido: T1 (`a623d2f`: 256 gates fixos, `MaxTrackedDevices`, `MaxPendingPerDevice`, `CommandTtl`), T3 (`a623d2f`: limite de corpo e `IsKnownDevice`), T2 (`8eb80fa`: varredura no EF) |
| 4. `SendAsync` sem prazo; `InFlight` eterno | Resolvido: T1 (`a623d2f`: `CommandTimeout`, `InFlightTimeout`, `PurgeExpiredAsync`), T2 e T4 (`8eb80fa`: varredura no EF e serviço de fundo) |
| 5. `new RepClient(string)` desliga a validação TLS | Resolvido: T6 (`a623d2f`: `ControlIdRepOptions.AcceptAnyServerCertificate = false`) e T5 (`8eb80fa`: atalho com `serverCertificateSha256`, log 1013); [ADR 0022](adr/0022-tls-do-rep-validado.md) |

### P2

| Achado | Situação |
| --- | --- |
| 6. Online autentica um equipamento e entrega outro | Resolvido: T3 (`a623d2f`), `device_id` divergente → 403 |
| 7. Corpo antes da autorização, sem limite e sem rate limiting | Resolvido: T3 (`a623d2f`), autorização antes do corpo com token único e limite de 4 MiB. O rate limiting fica com o ASP.NET Core (`RequireRateLimiting`), documentado no guia e aplicado no sample (T7, `66c4a7a`) |
| 8. Handlers do Monitor sem isolamento | Resolvido: T3 (`a623d2f`) |
| 9. Token (ordem, queda no global, rotação) | Resolvido: T3 (`a623d2f`): `Bearer` conferido antes de `DeviceToken`, `AllowTokenFallback = false`, `PreviousToken` |
| 10. Cancelamento não atômico | Resolvido: T1 (`a623d2f`) |
| 11. EF sem claim atômico | Resolvido: T2 (`8eb80fa`), sem mudança de esquema; [ADR 0023](adr/0023-limites-da-fila-de-push.md). `SendAsync` entre instâncias continua fora do escopo (ver abaixo) |
| 12. Configuração inválida só no primeiro uso | Resolvido: T6 (`a623d2f`), validação na partida e seção vazia lança no registro; `Map*` sem `Add*` lança (T3) |
| 13. Health check `Healthy` com zero equipamentos | Resolvido: T6 (`a623d2f`), `RequireDevices = true` e check `:none` |
| 14. Health check sem cache e sem prazo agregado; `Degraded` → 200; sonda dobrada | Resolvido: T6 (`a623d2f`), `CacheDuration` compartilhado e `TotalTimeout`. `Degraded` → 503 é configuração da aplicação (`ResultStatusCodes`), mostrada no guia e no sample (T7) |
| 15. `RepAbout.ApiRevision` lança | Resolvido: T5 (`8eb80fa`), `-1` e `ApiRevisionText` |
| 16. Enums sem `Unknown` | Resolvido: T5 (`8eb80fa`) |
| 17. Leitura de configuração tolerante demais | Em parte: T5 (`8eb80fa`), cache limpo a cada login e por `ClearConfigurationFieldCache()`, campo retirado informado pelo log 1010. Devolver os campos ausentes no resultado e distinguir valor fora do formato de ausente não entraram no plano (mudariam o tipo de retorno) |
| 18. `LogoutAsync` lança `HttpRequestException` | Resolvido: T5 (`8eb80fa`), log 1012 |
| 19. Retry, relogin e circuito duplicados no transporte | Resolvido: T8 (`66c4a7a`), caminho único com teste de paridade |
| 20. Operações compostas não atômicas | Em parte: T5 (`8eb80fa`), `SetAdministratorAsync` sem apagar antes (a confirmar em hardware); `ConfigureOnlineModeAsync` só ganhou a documentação da aplicação parcial |
| 21. Flags com três codificações | Fora do escopo: exige teste em equipamento real |
| 22. Fachada grande | Em parte: T5 (`8eb80fa`), `IControlIdClientDiagnostics` com `Session`, `CircuitState` e `ResetCircuit()`. O fatiamento de `IAccessClient` e a base comum dos clientes ficam para o plano rumo à 1.0 |

### P3

| Achado | Situação |
| --- | --- |
| Circuit breaker sem `Reset()`, sonda sem prazo, log sob o lock | Resolvido: T5 (`8eb80fa`) |
| Pareamento por `uuid` tolerante | Resolvido: T1 (`a623d2f`), `RequireUuid` (desligado por padrão) |
| `GET /push` sem `Cache-Control: no-store` | Resolvido: T3 (`a623d2f`), também em `/result` |
| Handler de resultado scoped capturado pelo singleton | Resolvido: T4 (`8eb80fa`), um escopo por resultado |
| Sample Dashboard | Resolvido: T7 (`66c4a7a`) |
| Telemetria só de saída | Resolvido: T3 (`a623d2f`), métricas `controlid.server.callback.requests` e `controlid.server.callback.handler.duration` (os spans de entrada vêm da instrumentação do ASP.NET Core) |
| OpenAPI sem metadado | Fora do escopo |
| CPF/CNPJ como número no REP | Fora do escopo: exige equipamento real |

### Build, CI e testes

| Achado | Situação |
| --- | --- |
| NU1900 vira erro sem rede | Resolvido: T0 (`a623d2f`) |
| Sem `global.json` e sem `Directory.Packages.props` | Resolvido: T0 (`a623d2f`), também `nuget.config` só com o nuget.org. `packages.lock.json` não entrou no plano |
| Avisos como erro só na CI | Mantido: a CI continua com `-warnaserror`, e o CONTRIBUTING pede o mesmo comando localmente; `TreatWarningsAsErrors` em Release não entrou no plano |
| Cobertura sem mínimo; Stryker com `break: 0` | Resolvido: T0 (`a623d2f`), mínimo de 89% de linhas e `break: 60` |
| Esperas por `Task.Delay` nos testes | Fora do escopo (reescrita dos testes antigos); os testes novos usam relógio falso ou ganchos internos |
| Testes que faltavam | Resolvidos: handler que lança, flood de `deviceId` e TTL (T1), duas instâncias EF no mesmo banco (T2), divergência de `device_id` (T3), sonda abandonada (T5), zero equipamentos (T6), ponta a ponta (T9) |

### Adições sugeridas

| Sugestão | Situação |
| --- | --- |
| 1. `PushServerOptions`, varredura e métricas de fila | Feito (T1, T4), com a métrica `controlid.push.commands` por desfecho; medidores de profundidade e idade da fila não entraram |
| 2. Entrega de resultados por `Channel` (outbox) | Não entrou no plano |
| 3. Callbacks fail-closed, rate limiting e limite de corpo | Feito (T3); rate limiting pelo ASP.NET Core, documentado |
| 4. `ValidateOnStart` e checagem de serviços no `Map*` | Feito (T6, T3) |
| 5. `IControlIdClientDiagnostics` e telemetria de entrada | Feito (T5, T3) |
| 6. Serialização com source generator (AOT) | Fora do escopo |
| 7. Fatiamento de `IAccessClient` | Fora do escopo (plano 1.0) |
| 8. `global.json`, gerenciamento central de pacotes e limites na CI | Feito (T0) |

Fora do escopo, pela seção do plano: fatiar `IAccessClient` e criar a base comum `AccessClient`/`RepClient` (plano 1.0);
unificar flags `0/1`, ler de volta o token gravado e CPF/CNPJ numérico (exigem equipamento real); tarefas do `SendAsync`
esperando entre instâncias; metadados de OpenAPI; serialização com source generator; reescrita dos testes antigos que
usam `Task.Delay`.