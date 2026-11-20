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
