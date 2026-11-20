# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e [SemVer](https://semver.org/lang/pt-BR/).

## [Unreleased]

Correções da [auditoria de 2026-11-19](docs/auditoria-2026-11-19.md): callbacks fechados por padrão, TLS do REP validado, fila de Push com prazos e limites, store EF com claim atômico, validação das opções na partida e núcleo endurecido. Tem mudanças que quebram: leia a seção abaixo antes de atualizar.

### Mudanças que quebram

Cada item traz o caminho de migração.

- **Callbacks fechados por padrão** ([ADR 0021](docs/adr/0021-callbacks-fechados-por-padrao.md)): `MapControlIdPush`, `MapControlIdMonitor` e `MapControlIdOnline` sem `RequireControlIdToken` respondem 401 (com `WWW-Authenticate: Bearer`) e registram o log 2006 uma vez por rota. **Migração:** encadeie `.RequireControlIdToken(...)` e grave o mesmo token no equipamento com `ConfigureCallbackSecurityAsync`. Para manter a rota aberta, só em rede isolada, encadeie `.AllowAnonymousControlIdCallbacks()` (namespace `ControlId.AspNetCore`). Firmware anterior à 6.22 não envia o cabeçalho `Authorization`: use `AllowAnonymousControlIdCallbacks()` com restrição de rede (`RequireHost`, firewall).
- **`DeviceToken` sem queda no token global**: com `ControlIdTokenOptions.DeviceToken` definido, um equipamento cuja função devolve `null` (ou um callback sem `deviceId`) é recusado em vez de cair no `Token`. **Migração:** devolva um token para cada equipamento ou ligue `AllowTokenFallback = true`.
- **`Map*` sem `Add*` lança**: `MapControlIdPush` sem `AddControlIdPush()` e `MapControlIdOnline` sem `IOnlineAccessHandler` registrado lançam `InvalidOperationException` no mapeamento, em vez de falhar na primeira requisição. **Migração:** registre o serviço antes de mapear.
- **Corpo dos callbacks limitado a 4 MiB**: acima disso a resposta é 413. **Migração:** `services.Configure<ControlIdCallbackOptions>(o => o.MaxRequestBodySize = ...)`.
- **Modo online**: em `*_create.fcgi` e `new_rex_log.fcgi`, `device_id` da query diferente do corpo responde 403; sem `device_id` na query, o token é conferido contra o equipamento do corpo. **Migração:** nenhuma para equipamentos reais, que mandam os dois iguais.
- **Monitor**: um `IMonitorNotificationHandler` que lança não impede mais os seguintes; a resposta é 500 só depois de todos rodarem. Com token único, o 401 sai antes de ler o corpo. **Migração:** handlers que dependiam de um anterior ter lançado para não rodar precisam checar isso por conta própria.
- **TLS do REP validado** ([ADR 0022](docs/adr/0022-tls-do-rep-validado.md)): o atalho `new RepClient(string, login, password, mode)` foi trocado por um com `serverCertificateSha256` opcional que valida o certificado, e `ControlIdRepOptions.AcceptAnyServerCertificate` passa a ter padrão `false`. Sem impressão digital, o certificado autoassinado de fábrica é recusado. O código-fonte continua compilando, mas binários compilados contra a 0.8.0-alpha.1 precisam ser recompilados (quebra binária registrada em `CompatibilitySuppressions.xml`). **Migração:** leia a impressão digital uma vez com `ControlIdCertificates.GetServerCertificateSha256Async`, confira no equipamento e passe em `serverCertificateSha256` (ou `"ServerCertificateSha256"` no `appsettings.json`). Aceitar qualquer certificado exige `AcceptAnyServerCertificate = true` explícito e gera o log 1013.
- **Prazos e limites do Push** ([ADR 0023](docs/adr/0023-limites-da-fila-de-push.md)): `SendAsync`/`SendBatchAsync` falham com `TimeoutException` depois de 5 minutos (`CommandTimeout`, mesmo com token do chamador sem prazo) e o envio sai da fila; pendente com mais de 24 horas (`CommandTtl`) não é entregue; cada equipamento tem no máximo 1.000 envios na fila (`PushQueueFullException`); `GetLastSeen` e o `DeviceContactTracker` acompanham no máximo 10.000 equipamentos. **Migração:** ajuste com `AddControlIdPush(o => ...)` ou `new PushServer(store, new PushServerOptions { ... })`; `null` desliga os prazos e `MaxPendingPerDevice = 0` desliga o limite.
- **`new PushServer(store, null)` ambíguo**: com o novo construtor `PushServer(store, PushServerOptions, handler)`, o `null` literal no segundo argumento não compila. **Migração:** `new PushServer(store, (IPushResultHandler?)null)` ou só `new PushServer(store)`.
- **Vários `IPushResultHandler`**: `AddControlIdPushResultHandler<T>` passa de `TryAddSingleton` (só o primeiro valia) para `TryAddEnumerable` com tempo de vida scoped: handlers diferentes rodam todos, em sequência, num escopo por resultado, e `THandler` não é mais registrado como serviço concreto. **Migração:** quem resolvia `THandler` direto do container registra-o à parte; handlers singleton com estado passam a ser criados a cada resultado.
- **`AddControlIdPush` cria o `PushServer` na partida**: o serviço de varredura é registrado junto, então opções inválidas impedem o host de subir. **Migração:** corrija as opções; `PurgeInterval = null` desliga a varredura.
- **Opções dos equipamentos validadas na partida**: configuração inválida de um equipamento registrado (ex.: sem `BaseAddress`) faz o `StartAsync` do host lançar `OptionsValidationException` citando o equipamento, em vez de falhar no primeiro uso. `AddControlIdAccessDevices`/`AddControlIdRepDevices` com seção vazia ou inexistente lançam `InvalidOperationException` no registro. **Migração:** corrija a configuração (o nome da seção costuma ser o erro).
- **Health checks**: `ControlIdHealthCheckOptions.RequireDevices` tem padrão `true`, então zero equipamentos registrados devolve o `failureStatus` (e `AddControlIdDeviceChecks` registra o check `{prefixo}:none`); o check agregado tem prazo total de 30 s (`TotalTimeout`); `SlowThreshold` e `CacheDuration` negativos lançam `ArgumentOutOfRangeException`. **Migração:** `RequireDevices = false` em aplicações sem equipamentos registrados (só Push ou modo online).
- **`SetAdministratorAsync(id, true)`** passa a alterar o papel com `modify_objects` e só cria quando nada foi alterado, sem apagar antes (a confirmar em equipamento real). **Migração:** nenhuma.
- **`LogoutAsync`** não lança mais erro de rede nem timeout (log 1012, Debug); cancelamento do chamador continua lançando. **Migração:** remova `catch` que só existia para isso.
- **Códigos desconhecidos do firmware**: `AccessLog.EventType` e `UserIdentifiedEvent.Event` (`AccessEventType`) e `FaceImageError.CodeType` (`FaceImageErrorCode`) devolvem `Unknown` (0), e `CauseType` (`AlarmCause`, nos alarmes) e `GetVpnStatusAsync` (`VpnStatus`) devolvem `Unknown` (-1) para um código que o enum não tem, em vez de um valor fora do enum. **Migração:** trate `Unknown` onde houver `switch` sobre esses enums.
- **Cache de parâmetros de configuração ausentes zerado a cada login**: um parâmetro que o firmware passou a ter volta a ser lido depois de atualizar ou reiniciar o equipamento. **Migração:** nenhuma.

### Added
- **`PushServerOptions`**: `CommandTimeout`, `CommandTtl`, `InFlightTimeout`, `MaxPendingPerDevice`, `MaxTrackedDevices`, `RequireUuid`, `ResultHandlerTimeout`, `PurgeInterval` e `LoggerFactory`, com o construtor `PushServer(store, options, handler)` e a sobrecarga `AddControlIdPush(Action<PushServerOptions>)` ([guia](docs/guia.md#prazos-e-limites-do-push)).
- **Varredura da fila**: `PushServer.PurgeExpiredAsync` e o contrato `IPushCommandStoreMaintenance` (implementado pelo store em memória e pelo EF). No ASP.NET Core, um serviço de fundo varre a cada `PurgeInterval` (padrão 1 minuto; `null` desliga).
- `PushQueueFullException`, `DeviceContactTracker(int maxDevices)`, logs 1101–1107 do Push e a métrica `controlid.push.commands` (tag `controlid.push.outcome`).
- **Store EF**: claim atômico (várias instâncias sobre o mesmo banco não entregam o mesmo envio), varredura de vencidos e erro claro sem `AddDbContextFactory`, sem mudança de esquema. Com SQLite e várias conexões, use `Default Timeout` na connection string.
- **ASP.NET Core**: `AllowAnonymousControlIdCallbacks()`, `ControlIdTokenOptions.PreviousToken` (rotação) e `AllowTokenFallback`, `ControlIdCallbackOptions` (`MaxRequestBodySize`, `DefaultMaxRequestBodySize`, `IsKnownDevice`), `Cache-Control: no-store` em `/push` e `/result`, logs 2006–2013 e as métricas `controlid.server.callback.requests` e `controlid.server.callback.handler.duration`.
- **Health checks**: `ControlIdHealthCheckOptions.RequireDevices`, `CacheDuration` (o check agregado e os por equipamento fazem uma requisição por equipamento) e `TotalTimeout`.
- **Núcleo**: `IControlIdClientDiagnostics` (`Session`, `CircuitState`, `ResetCircuit`), implementada por `AccessClient` e `RepClient` (as interfaces publicadas não mudam); `AccessClient.ClearConfigurationFieldCache()`; `RepAbout.ApiRevisionText`; `Unknown` em `AccessEventType`, `FaceImageErrorCode`, `AlarmCause` e `VpnStatus`; logs 1010–1013.
- A sonda do circuit breaker sem resposta por mais de `2 × Timeout` (mínimo 1 s; 5 min com timeout infinito) conta como falha e reabre o circuito.
- Erro de TLS recusado explica como fixar a impressão digital.
- **Samples**: o Server exige token nas três rotas, protege a API de operação com chave (`X-Api-Key`), tem rate limiting nos callbacks, `/health` detalhado só para chamadas locais e segredos por user-secrets (`--id controlid-net-server-sample`) ou variáveis de ambiente; o Dashboard tem health check real, token nos callbacks (vazio gera um por execução) e senha obrigatória (`Dashboard__DevicePassword`); o compose usa `CONTROLID_DEVICE_PASSWORD` e `CONTROLID_CALLBACK_TOKEN`; o sample do REP ganha o comando `cert` e usa a impressão digital.
- Build: base da API na 0.8.0-alpha.1, gerenciamento central de pacotes (`Directory.Packages.props`), `global.json`, `nuget.config` só com o nuget.org, cobertura mínima de 89% e nota mínima de mutação de 60 na CI.
- Testes de ponta a ponta dos callbacks fechados e da robustez do Push contra o simulador.

### Changed
- `RepAbout.ApiRevision` não lança mais com revisão hexadecimal com `A`–`F` (ex.: `0x10A`): devolve `-1`, e o texto fica em `ApiRevisionText`.
- `IPushResultHandler` recebe um token próprio que vence em `ResultHandlerTimeout`, sem ligação com a requisição do equipamento, e também é chamado para envios expirados.
- O circuit breaker emite log e métrica de transição fora do lock.
- O transporte usa um caminho único de envio (novas tentativas, novo login e circuit breaker) para comandos e para streams de AFD, sem mudança de comportamento.
- `ConfigureOnlineModeAsync` documenta a aplicação parcial em caso de falha.

### Fixed
- Um `IPushResultHandler` que lançava derrubava `/push` ou `/result` e fazia o equipamento perder o próximo comando (agora vira log 1101).
- `deviceId` arbitrário na query fazia a memória do `PushServer` crescer sem limite.
- Cancelar um `SendAsync` não removia o envio do store; o cancelamento agora libera a tarefa na hora e remove em segundo plano.
- Handler de resultado scoped registrado direto no container era capturado pelo `PushServer` singleton.
- Sample Dashboard: o `Urls` do `appsettings.json` tinha precedência sobre `ASPNETCORE_URLS`, e o compose não usava a porta 8080.
- `dotnet build -warnaserror` sem rede falhava pelo NU1900.

## [0.8.0-alpha.1] - 2026-11-14

Fase 3, produção: circuit breaker, health check por equipamento, token nos callbacks e fila de Push durável.

### Added
- **Circuit breaker por equipamento**: `CircuitBreakerFailureThreshold` e `CircuitBreakerBreakDuration` em `ControlIdDeviceOptions`, `CircuitState` nos clientes e `ControlIdCircuitOpenException`. Desligado por padrão ([ADR 0017](docs/adr/0017-circuit-breaker-proprio.md), [guia](docs/guia.md#circuit-breaker)).
- **Health check por equipamento**: `AddControlIdDeviceChecks` e `ControlIdHealthCheckOptions.MaxContactAge`, com o último contato (`DeviceContactTracker`, origem Push, Monitor ou Online) registrado pelas rotas ([ADR 0018](docs/adr/0018-health-check-por-equipamento.md)). `MaxContactAge` só degrada com contato registrado e vencido.
- **Token Bearer nas rotas**: `RequireControlIdToken` (`ControlIdTokenOptions`, token único ou por equipamento) e `ConfigureCallbackSecurityAsync` para gravar o token e o certificado no equipamento ([ADR 0019](docs/adr/0019-token-bearer-nos-callbacks.md)). A seção e o formato do token e do certificado no equipamento estão a validar em hardware.
- **Fila de Push durável**: `IPushCommandStore` (padrão `InMemoryPushCommandStore`), `IPushResultHandler` e `AddControlIdPushResultHandler`. Novo pacote `ControlId.Net.Push.EntityFrameworkCore` (`ApplyControlIdPush`, `AddControlIdPushEntityFrameworkStore`) ([ADR 0020](docs/adr/0020-fila-de-push-duravel.md)).
- Testes de ponta a ponta das novas funções contra o simulador.

### Changed
- O pacote `ControlId.Net.Push.EntityFrameworkCore` tem só o alvo `net10.0`, por exigência do EF Core 10 (exceção ao [ADR 0014](docs/adr/0014-manter-alvo-net8.md)).
- **Mudança sutil no Push**: o comando é serializado ao enfileirar, então alterar o `Body` do `PushCommand` depois de enfileirar não tem mais efeito.
- `PushServer` ganha o construtor com `IPushCommandStore` e as versões assíncronas `EnqueueAsync`, `EnqueueBatchAsync`, `GetPendingCountAsync`, `HandlePushAsync` e `HandleResultAsync`, que usam o store.
- Um comando órfão sem tarefa esperando é reportado ao `IPushResultHandler` com `Error` e nunca reenviado.
- O teste do store EF usa um arquivo SQLite temporário.

## [0.7.0-alpha.1] - 2026-11-13

Logs estruturados, simulador do REP iDClass com Push e modo online, dashboard em tempo real, site de documentação, teste de mutação e avaliação de Native AOT.

### Added
- **Logs estruturados**: `ControlIdDeviceOptions.LoggerFactory` (e no REP) e o `ILoggerFactory` do container de DI alimentam o `ILogger` com categoria `ControlId.Net` e `LoggerMessage` gerado. EventIds 1001-1006 no núcleo e 2001-2004 no ASP.NET Core; sessão, login, senha e corpo nunca são registrados ([ADR 0016](docs/adr/0016-logs-por-options.md), [guia](docs/guia.md#logs)).
- **Simulador**: modo REP iDClass (`--Simulator:Kind=Rep`, `POST /simulator/punch`), Push (o simulador consulta `/push` e responde em `/result`) e modo online (consulta `new_user_identified.fcgi` e `new_card.fcgi`).
- **Dashboard em tempo real** ([samples/ControlId.Net.Dashboard](samples/ControlId.Net.Dashboard)): Blazor Server com o Monitor ao vivo; sobe em `http://localhost:8081` no `samples/docker-compose.yml`.
- **Avaliação de Native AOT**: sonda `tools/ControlId.Net.AotProbe` (3 IL2026, 3 IL3050, 1 IL2070) e [ADR 0015](docs/adr/0015-native-aot.md). A biblioteca não declara `IsAotCompatible`.
- **Teste de mutação** com Stryker.NET: nota de 68,08% em `src/ControlId.Net`.
- **Site de documentação** (DocFX), publicado pelo workflow `docs.yml` em https://daviluissantos.github.io/ControlId.Net/.
- Dashboard na solution e teste de fumaça do simulador REP na CI.

### Fixed
- Simulador: a espera dos clientes de Push e do modo online podia calcular um atraso negativo quando o prazo vencia entre duas leituras do relógio. O `Task.Delay` lançava `ArgumentOutOfRangeException`, e a exceção derrubava o simulador inteiro (e explicava o teste de Push em lote que estourava o prazo na CI).

## [0.6.0-alpha.1] - 2026-11-12

Simulador de equipamento com testes de integração, traces e métricas (OpenTelemetry), health checks, novas tentativas em leituras e parser de AFD até 6,5× mais rápido. Mantém o alvo `net8.0` ([ADR 0014](docs/adr/0014-manter-alvo-net8.md)).

### Added
- Benchmarks com BenchmarkDotNet ([benchmarks/](benchmarks/README.md)): o parser de AFD com 10 mil e 100 mil marcações, lendo o arquivo inteiro e registro a registro.
- **Novas tentativas em leituras**: `ReadRetries` e `RetryDelay` em `ControlIdDeviceOptions` (e `ControlIdRepOptions`) repetem comandos de leitura após falha de rede, timeout ou HTTP 5xx/408/429, com espera exponencial e variação aleatória. Comandos que alteram o equipamento nunca são repetidos. Desligado por padrão ([ADR 0013](docs/adr/0013-novas-tentativas-so-em-leituras.md), [guia](docs/guia.md#novas-tentativas-em-leituras)). Nova métrica `controlid.client.retries` e evento `controlid.retry` no span.
- **Health checks**: `AddHealthChecks().AddControlIdDevices()` (pacote de DI) verifica todos os equipamentos registrados em paralelo, com prazo por equipamento e limite de lentidão; saudável, degradado ou `failureStatus`, com o resultado de cada um nos dados ([guia](docs/guia.md#health-checks)). O pacote de DI passa a depender de `Microsoft.Extensions.Diagnostics.HealthChecks`.
- **Traces e métricas (OpenTelemetry)**: um span por comando ao equipamento (com fila, re-login e HTTP) e as métricas `controlid.client.operation.duration`, `controlid.client.relogins`, `controlid.client.requests.queued` e `controlid.client.requests.active`. Pelas APIs do .NET, sem dependência do OpenTelemetry; os nomes ficam em `ControlIdTelemetry` ([ADR 0012](docs/adr/0012-telemetria-sem-dependencia-do-opentelemetry.md), [guia](docs/guia.md#observabilidade-opentelemetry)). O `netstandard2.0` passa a depender de `System.Diagnostics.DiagnosticSource`.
- `samples/docker-compose.yml` com o simulador e o dashboard do Aspire; o `samples/ControlId.Net.Server` exporta traces e métricas por OTLP e ganha rotas que acessam equipamentos direto por HTTP (`/direto/{nome}/...`).
- **Simulador de equipamento** ([tools/ControlId.Net.Simulator](tools/ControlId.Net.Simulator/README.md)): a API `*.fcgi` da linha de acesso com estado em memória (sessão, objetos com filtro e paginação, fotos, configuração, portas) e notificações do Monitor, mais rotas `/simulator/*` para provocar identificações e reinícios. Reproduz as divergências do firmware de que a biblioteca depende. Imagem Docker publicada no GitHub Container Registry a cada versão ([ADR 0011](docs/adr/0011-simulador-de-equipamento.md)).
- Testes de integração (`tests/ControlId.Net.IntegrationTests`): a biblioteca contra o simulador com HTTP de verdade, inclusive re-login, paginação, exclusão em lotes, configuração tolerante e o Monitor de ponta a ponta até as rotas do `ControlId.Net.AspNetCore`. Rodam na CI, junto com um teste de fumaça da imagem.

### Changed
- Parser de AFD até 6,5× mais rápido e com 59% menos alocação (100 mil marcações em streaming: de 133 ms para 21 ms). Os campos são lidos por índice, sem substrings; data e hora são validadas dígito a dígito com as regras do `DateTime.TryParseExact`; o CRC-16/KERMIT usa tabela. O comportamento, inclusive as mensagens de erro, não muda: 30 testes de casos de borda e testes diferenciais passam no parser antigo e no novo ([resultados](benchmarks/README.md)).
- Dependências de runtime na 10.x (10.0.15: `System.Text.Json`, `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options.ConfigurationExtensions`): o .NET 8 saiu de suporte em 2026-11-10, e a 10.x continua com alvos `net8.0` e `netstandard2.0` ([ADR 0010](docs/adr/0010-dependencias-no-lts-suportado.md)). Quem usa o .NET Framework passa a receber as correções de segurança da `System.Text.Json`.
- README reorganizado: página de apresentação em inglês (`README.md`) e em português (`README.pt-BR.md`), com diagrama de arquitetura e roadmap; o guia de uso completo foi para [docs/guia.md](docs/guia.md). A página do NuGet usa uma versão curta, com links absolutos (`src/PACKAGE.md`).
- A CI roda em todo push na `main` e em cada pull request, com build sem avisos, os três alvos de teste, validação da API pública e resumo de cobertura ([ADR 0009](docs/adr/0009-ci-em-push-e-pull-request.md)).

## [0.5.0-alpha.1] - 2026-10-30

REP iDClass completo, proteção contra operações destrutivas, certificado fixado e governança da publicação.

### Added
- **REP iDClass completo**: os 47 comandos da [documentação da API](https://www.controlid.com.br/suporte/api_idclass_latest.html) (v4.0.1), com `RepDocumentationCoverageTests` garantindo cada um. Novos no `RepClient`:
  - Sessão: `IsSessionValidAsync`, `ChangeLoginAsync`.
  - Usuários: `UpdateUsersAsync` (`RepUserUpdate`), `GetUsersAsync` por PIS/CPF, `EnumerateUsersAsync`, `ImportUsersCsvAsync`, `ExportUsersCsvAsync`, `RemoveAdminsAsync` (exige `DataLossConfirmation.Confirmed`), `AddUsersAsync` com `doMatch`, e foto no cadastro (`RepUser.Image`, `ImageTimestamp`, `HasFace`).
  - Empregador: `GetCompanyAsync`, `SetCompanyAsync` (`RepCompany`, com CNPJ alfanumérico).
  - Sistema: `GetInfoAsync`, `GetSystemConfigurationAsync`, beep, animação, comprovante curto, identificação 1:1, iDCloud, chave pública, bobina, rede (`RepNetworkSettings`), horário de verão (`RepDaylightSavingTime`) e data e hora com fuso (`GetDateTimeOffsetAsync`, `SetDateTimeAsync(DateTimeOffset)`). `RepAbout.ApiRevision` dá a revisão da API (260 → 104); `RepSystemInformation` ganha bobina e cortes.
  - AFD: `GetAfdAsync(RepAfdQuery)` com `headerTop`, e leitura sob demanda com `OpenAfdStreamAsync` e `EnumerateAfdRecordsAsync`.
  - Biometria: `ExtractTemplateAsync`, `MergeTemplatesAsync`.
  - REP Facial: configuração do módulo, licença de 10 mil faces, informações do módulo, estado do cadastro, cadastro remoto, e fotos (`SetUserImageAsync`, `GetUserImageAsync`, `DeleteUserImageAsync`, `ImportUserImageAsync`).
- Confirmação explícita para o que apaga dados sem volta: `ResetToFactoryDefaultAsync`, `DeleteAllAdministratorsAsync`, `DeleteAllUserImagesAsync` e `DeleteAllFaceTemplatesAsync` ganham sobrecargas que exigem `DataLossConfirmation.Confirmed`. Sem ela (inclusive com o valor padrão do enum), a chamada é recusada antes de ir ao equipamento.
- Certificado fixado: `ControlIdDeviceOptions.ServerCertificateSha256` (e em `ControlIdRepOptions`) aceita só o certificado com aquela impressão digital SHA-256, mesmo autoassinado, no lugar de aceitar qualquer um. `ControlIdCertificates.GetServerCertificateSha256Async` lê a impressão digital do equipamento para gravar na configuração. O padrão não muda: sem impressão digital, o REP continua aceitando qualquer certificado.
- Publicação com governança: cada `dotnet pack` valida a API contra a versão publicada e entre os alvos (quebras viram erro), a API pública fica registrada em `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`, e as regras de qualidade recomendadas estão ligadas sem nenhum aviso. Cada versão publicada leva resumo de cobertura dos testes, SBOM (CycloneDX) e atestação de proveniência dos pacotes (`gh attestation verify <arquivo.nupkg> --repo DaviLuisSantos/ControlId.Net`).
- `SECURITY.md` (relato privado de vulnerabilidades e uso seguro), `CONTRIBUTING.md`, modelos de issue e de PR, e as decisões de arquitetura em [docs/adr](docs/adr/README.md).
- `GetEnrollerStateAsync` (`enroller_state.fcgi`, da coleção oficial do Postman): estado do cadastro remoto e resultado do último, com `IsEnrolling`. O firmware manda `biometry_sate` (erro de digitação); `EnrollerState.BiometryState` aceita as duas grafias. Serve para acompanhar o cadastro assíncrono no iDFace, que não avisa pelo Monitor.

### Deprecated
- As sobrecargas antigas desses quatro métodos, sem confirmação, ficam `[Obsolete]`. Continuam funcionando e saem numa versão futura.

### Changed
- **Quebra de compatibilidade:** `IAccessClient` e `IRepClient` ganham membros novos. Quem só usa `AccessClient` e `RepClient` não é afetado; quem implementa as interfaces por conta própria (ex.: um mock) precisa implementar os membros novos.
- `DestroyObjectsAsync` remove em lotes de 200 (`limit`, novo `AccessClient.DestroyPageSize`) até acabar, como o SDK oficial da Control iD passou a fazer em 2026-09 para exclusões grandes. Exclusões com menos de 200 registros continuam numa requisição só.
- `MapControlIdMonitor` também aceita o caminho no singular (`api/notification/...`) quando o configurado termina em `notifications`: o SDK oficial registra que o firmware posta `device_is_alive` assim. As duas rotas ficam no grupo devolvido.

## [0.4.3-alpha.1] - 2026-10-30

Três nomes errados da API de acesso, encontrados na coleção oficial do Postman e confirmados num iDFace 6.13.1.

### Fixed
- `GetDoorStatesAsync` chamava `door_state.fcgi`, que o equipamento recusa ("Invalid command"); agora chama `doors_state.fcgi`, como o exemplo da documentação, a versão em inglês e a coleção oficial do Postman.
- `HidSettings` gravava e lia a seção `hid`, nome inferido do título da documentação; a seção é `hid_reader`.
- `out_mode` e `wiegand_format_size` da SecBox ficam na seção `sec_box`, não em `sec_boxs`: passam a existir em `SecBoxSettings` (`OutMode`, `WiegandFormatSize`). `SecBoxsSettings` fica obsoleta e agora grava em `sec_box`.

### Documentation
- Os três nomes corrigidos (`doors_state`, `hid_reader`, `sec_box`) confirmados num iDFace 6.13.1, com o que mais existe e não existe nesse firmware, em [docs/COBERTURA.md](docs/COBERTURA.md).
- Paralelismo testado num iDFace real: `MaxConcurrentRequests` é respeitado, mas o equipamento atende uma requisição por vez, então o ganho está entre equipamentos (`ControlIdDevices.RunAsync`), não dentro de um. Anotado em `MaxConcurrentRequests`, no README e em [docs/COBERTURA.md](docs/COBERTURA.md).

## [0.4.2-alpha.1] - 2026-10-13

Documentação do cadastro remoto assíncrono testado num iDFace real. Sem mudanças de código.

### Documentation
- `StartRemoteEnrollAsync` no iDFace 6.9.12: com `Save = false` a tela de cadastro não abre; com `Save = true` a face é gravada, mas o resultado não chega pelo Monitor (`RemoteEnrollNotification`). Anotado no método, no README e em [docs/COBERTURA.md](docs/COBERTURA.md).

## [0.4.1-alpha.1] - 2026-10-13

Correção do Monitor encontrada com uma pessoa no iDFace (cadastro e identificação reais).

### Fixed
- `DaoNotification.GetInsertedAccessLogs()` e `ObjectChange.ValuesAs<T>()` lançavam exceção com os eventos mais comuns: pelo Monitor, o iDFace manda `user_id: ""` em rosto não identificado e `confidence: ""` em cartão negado. A leitura do Monitor passa a tratar `""` em campo numérico como vazio (`null`); antes, um servidor como o de exemplo respondia 500 e a notificação se perdia.

### Documentation
- Cadastro remoto de PIN não funciona no iDFace 6.9.12 (responde `UNKNOWN`), anotado em [docs/COBERTURA.md](docs/COBERTURA.md).

## [0.4.0-alpha.1] - 2026-10-13

Correções encontradas testando num iDFace real (firmware 6.9.12).

### Fixed
- `GetConfigurationSectionAsync<T>` e `GetConfigurationAsync` falhavam na seção inteira quando o modelo não tinha um dos parâmetros (o firmware recusa a leitura toda: no iDFace, 25 das 34 seções, entre elas `general` e `monitor`). Agora o parâmetro apontado no erro sai da lista, a leitura é repetida e ele fica `null`/fora do resultado; o cliente lembra dos parâmetros inexistentes e não os pede de novo.
- `UpdateSecBoxAsync` era recusado ("integer expected"): `enabled`, `door_sensor_enabled` e `door_sensor_idle` passam a ser enviados como 0/1.
- `ExtractFaceTemplateAsync` devolvia `Success = false` mesmo extraindo o template, porque o iDFace não manda `success` nessa resposta. Sem o campo, `FaceImageResult.Success` agora vale a ausência de erros.
- `AuthorizeRemotelyAsync` era recusado sem usuário (ex.: `OnlineAuthorization.NotIdentified()`): o firmware exige `user_id`, `portal_id`, `user_name`, `user_image` e `actions`. Os que faltarem vão como 0, portal 1, vazio, `false` e lista vazia, sem alterar o objeto recebido.
- `ConfigureOnlineModeAsync` falhava fora das IHMs (o iDFace não tem `general.ihm_enterprise_mode`, e o `set_configuration` era recusado depois de aplicar parte dos valores). `ihm_enterprise_mode` só é enviado com `OnlineModeConfiguration.IsIhm`.
- `CreateHolidayAsync` lança `ArgumentOutOfRangeException` para datas depois de 19/01/2038, que o equipamento recusa (guarda os segundos em 32 bits).
- `AccessUser.UserTypeId` lê 0 como `null`: o equipamento devolve 0 quando o campo está vazio, mas recusa gravar 0 (chave estrangeira para `user_types`), então regravar um usuário lido falhava.

### Changed
- `PushServerSettings.PushRemoteAddress` passa de `int?` para `string?` (o valor é `ip:porta`; antes a leitura sempre dava `null`).

### Documentation
- Comportamentos observados no iDFace 6.9.12 em [docs/COBERTURA.md](docs/COBERTURA.md): `set_configuration` não atômico, áudio de acesso só em WAV, Push em lote não aceito, reinícios depois de exportar o log de auditoria e a lista do que o modelo não tem.

## [0.3.0-alpha.1] - 2026-10-12

Bases grandes e vários equipamentos: limite de requisições por equipamento, leitura paginada e operações em lote.

### Added
- `ControlIdDeviceOptions.MaxConcurrentRequests` (e em `ControlIdRepOptions`): limite de requisições simultâneas por equipamento, padrão 4, login incluído. As chamadas além do limite esperam a vez. O limite de conexões do `HttpClientHandler` criado pelo pacote (e pelo pacote de DI) acompanha o valor, então o comportamento é o mesmo no .NET Framework e no .NET Core.
- Leitura paginada com `IAsyncEnumerable`: `EnumerateUsersAsync` e `EnumerateAccessLogsAsync` (páginas por ID, estáveis com inserções e remoções durante a leitura), `EnumerateObjectsAsync<T>` (modelos tipados, ordenados pela chave) e `EnumerateObjectsAsync<T>(LoadObjectsRequest)` (qualquer objeto, por `limit`/`offset`). Página padrão de 500 registros (`AccessClient.DefaultPageSize`); a próxima página só é buscada quando a anterior é consumida.
- `ControlIdDevices.RunAsync`: a mesma operação em vários equipamentos em paralelo, com limite de equipamentos simultâneos (`maxParallelism`, padrão 8) e um `DeviceResult`/`DeviceResult<T>` por equipamento (nome, sucesso, valor, erro e tempo). A falha de um equipamento não interrompe os demais.
- `RunOnAccessDevicesAsync` e `RunOnRepDevicesAsync` em `IControlIdClientFactory` (pacote de DI), para todos os equipamentos registrados ou só os informados.
- Dependência de `Microsoft.Bcl.AsyncInterfaces` no alvo netstandard2.0 (para `IAsyncEnumerable`).

### Changed
- Até aqui as requisições a um equipamento não tinham limite no .NET Core (e ficavam em 2 conexões no .NET Framework). Agora o padrão é 4 simultâneas por equipamento; use `MaxConcurrentRequests` para ajustar.

## [0.2.0-alpha.1] - 2026-10-12

Cobertura completa da documentação pública da API de acesso (veja [docs/COBERTURA.md](docs/COBERTURA.md)).

### Changed
- `SetUserImageAsync` e `SetUserImagesAsync` passam a enviar `timestamp` (obrigatório na API) e `match`, e devolvem o resultado do cadastro facial (`FaceImageResult`). `UserImage` ganha `Timestamp` e `PushCommand.SetUserImage`, os mesmos parâmetros.

### Added
- Modelos para todos os objetos documentados que faltavam: `qrcodes`, `uhf_tags`, `pins`, `contingency_cards`, `alarm_zones`, `timed_alarms`, `user_roles`, `scheduled_unlocks`, `actions` (`ActionScript`), `devices`, `catra_infos`, `log_types`, `contacts`, `custom_thresholds`, `network_interlocking_rules`, `change_logs` e `access_log_access_rules`.
- CRUD tipado genérico: `[ControlIdObject]` nos modelos e `GetObjectsAsync<T>`/`CreateObjectAsync<T>`/`UpdateObjectAsync<T>`/`DeleteObjectAsync<T>`.
- QR Codes, tags UHF e PINs (`Get/Create...Async`), `SetAdministratorAsync`, `GetChangeLogsAsync` e `GetAccessLogRuleIdsAsync`.
- Vínculos `AccessLink.PortalAction`, `ScheduledUnlockAccessRule`, `AlarmZoneTimeZone` e `ContingencyCardAccessRule`.
- `AccessUser.ImageTimestamp` e `AccessUser.LastAccess`.
- Estado e comandos do equipamento: `GetDoorStatesAsync` (`door_state`), `GetAlarmStatusAsync`/`StopAlarmAsync` (`alarm_status`, com `AlarmCause` também em `AlarmLog.CauseType`), `RebootWithoutSessionAsync` (`GET /reboot`), `SetTurnstileLedAsync` (`remote_led_control`, iDBlock Next) e `ExecuteActionsWithStatusAsync` (resultado do intertravamento).
- `DeviceAction.OpenTurnstileCollector` (`open_collector`) e `DeviceAction.ActivateTurnstileRelay` (`relay=1|2`).
- Modo online: `ComponentId`/`ComponentName` (iDBlock Next).
- Reconhecimento facial: `TestUserImageAsync`, `GetUserImageWithTimestampAsync`, `GetUserImagesAsync` (`user_get_image_list`), `GetUserImageInfoAsync`, `DeleteUserImagesAsync`, `DeleteDanglingUserImagesAsync`, templates faciais (`ExtractFaceTemplate(s)Async`, `EnrollFaceTemplatesAsync`, `DeleteFaceTemplatesAsync`, `DeleteAllFaceTemplatesAsync`), `CaptureCameraAsync` (`save_screenshot`), `FaceImageResult` com notas e `FaceImageErrorCode`.
- `StartTemplateSyncAsync`/`EndTemplateSyncAsync` (carga de bases grandes) e `ExportObjectsAsync` (`export_objects`).
- Mídia do iDFace: modo propaganda (`UploadAdvertisingVideoAsync`, `SetAdvertisingModeAsync`, `RemoveAdvertisingVideoAsync`), mensagens sonoras (`UploadAccessAudioAsync`, `GetAccessAudioAsync`, `GetAccessAudioStatusAsync`), interfonia SIP (`MakeSipCallAsync`, `FinalizeSipCallAsync`, `GetSipStatusAsync`, som de chamada) e `GetRtspStreamUri`. Vídeo e áudio são enviados em partes de até 2 MB automaticamente.
- Rede e sistema: diagnóstico (`TestInternetConnectionAsync`, `PingAsync`, `NsLookupAsync`), `SetNetworkInterlockAsync`, `IsValidBiometryAsync`, OpenVPN (`SetVpnAsync`, `GetVpnInformationAsync`, `GetVpnStatusAsync`, `UploadVpnFileAsync`, `HasVpnFileAsync`, `GetVpnExampleFilesAsync`), `UploadSslCertificateAsync`, `Configure8021XAsync`, `SetOsdpInstallationModeAsync`, `ChangeIdCloudCodeAsync` e `PushConfiguration.IdCloud()`.
- `docs/COBERTURA.md` com a matriz de cobertura da documentação e `DocumentationCoverageTests`, que chama o método de cada um dos 85 endpoints documentados e confere endpoint e método HTTP.
- Configurações tipadas: 34 seções (`GeneralSettings`, `CatraSettings`, `AlarmSettings`, `IdentifierSettings`, `PjsipSettings`, `OnvifSettings`, `SecBoxSettings`...) com cerca de 245 parâmetros documentados, geradas a partir da documentação, e `Get/SetConfigurationSectionAsync`/`SetConfigurationSectionsAsync`. Os nomes dos módulos de Energia, RS485, RFID, HID e OSDP não constam da documentação e foram inferidos dos títulos.
- Licenças: `ApplyFaceLicenseAsync` (iDFace Pro 10k/15k/30k, iDFace Max 50k/100k) e `UpgradeToEnterpriseAsync` (iDFlex/iDAccess Nano).

## [0.1.0-alpha.2] - 2026-10-10

### Changed
- Autor e copyright dos pacotes e da licença passam a ser "Davi Luis".

## [0.1.0-alpha.1] - 2026-10-10

### Fixed
- `AccessLog` tinha `AccessRuleId` (`access_rule_id`), campo que não existe em `access_logs` e nunca vinha preenchido. Trocado pelos campos documentados: `IdentificationRuleId`, `QrCodeValue`, `UhfTag`, `PinValue`, `Confidence`, `Mask` e `LogTypeId`.
- `RepClient.GetAfd*` decodificava o AFD como UTF-8 quando o REP não informava o charset, corrompendo nomes acentuados; agora usa ISO-8859-1 (codificação das portarias) quando os bytes não são UTF-8 válido.

### Changed
- `FingerprintImage` renomeado para `CapturedImage` (também usado na face do modo online).

### Added
- Transporte HTTP com login automático, cache de sessão e re-login em 401.
- `AccessClient` (iDAccess / iDFlex / iDFace): objetos genéricos (`load/create/modify/destroy_objects`), usuários, logs de acesso, abertura de porta/SecBox/catraca, informações do sistema e data/hora.
- `AccessClient`: cartões, digitais (`templates`), grupos, regras de acesso, horários (`time_zones`/`time_spans`) e portais tipados; associações via `LinkAsync`/`UnlinkAsync` com `AccessLink`.
- `AccessClient`: fotos de usuário (`user_set_image`, `user_set_image_list`, `user_get_image`, `user_list_images`, `user_destroy_image`).
- Monitor: `MonitorNotifications.Parse` com notificações tipadas (`dao`, `door`, `secbox`, `catra_event`, `operation_mode`, `device_is_alive`, `access_photo`, `usb_drive`) e `ConfigureMonitorAsync`.
- Push: `PushServer` (fila por equipamento, `HandlePush`/`HandleResult`), `PushCommand`, `PushDevice` com operações tipadas e `ConfigurePushAsync`.
- Push em lote: `PushServer.SendBatchAsync`/`PushDevice.SendBatchAsync` (objeto `transactions`), com um `PushResult` por comando.
- Pacote `ControlId.Net.DependencyInjection`: `AddControlIdAccess`/`AddControlIdRep` (padrão ou por nome, por delegate ou `IConfiguration`), `AddControlIdAccessDevices`/`AddControlIdRepDevices` para vários equipamentos no `appsettings.json` e `IControlIdClientFactory`.
- Pacote `ControlId.Net.AspNetCore`: `AddControlIdPush`, `AddControlIdMonitor` (handler tipado ou delegate), `MapControlIdPush` e `MapControlIdMonitor`.
- Sample `ControlId.Net.Server` (ASP.NET Core) com Push e Monitor.
- Objetos tipados: áreas (`AccessArea`, `AccessLink.AreaAccessRule`), feriados (`AccessHoliday`), eventos de porta/SecBox/catraca (`AccessDeviceEvent`), logs de alarme (`AlarmLog`) e SecBox (`GetSecBoxAsync`/`UpdateSecBoxAsync`).
- Exportações: `GenerateReportAsync` (`report_generate`), `ExportAfdAsync`/`ExportAfdFileAsync` (`export_afd` do equipamento de acesso) e `ExportAuditLogsAsync` (`export_audit_logs`).
- Modo online: resultados do cadastro remoto (`card_create`, `face_create`, `fingerprint_create`, `template_create`, `pin_create`, `password_create`) via `OnlineEvents.ParseEnroll` e `IOnlineAccessHandler.OnRemoteEnrollAsync` no `MapControlIdOnline`; `RemoteEnrollResult.Face`.
- Comandos tipados no `AccessClient`: `ShowMessageAsync`/`ClearMessageAsync`, `BuzzAsync`, `ChangeLoginAsync` (atualiza as credenciais do próprio cliente), `HashPasswordAsync`, `RebootAsync`, `RebootToUpdateModeAsync`, `ResetToFactoryDefaultAsync`, `DeleteAllAdministratorsAsync`, `SetNetworkAsync`, logotipo (`Get`/`Set`/`Delete`/`ShowLogoAsync`), `GetGpioStateAsync`, `RefreshLedAsync`, `SetUsbPortEnabledAsync` e `CreateOrModifyObjectsAsync`; `PushCommand.ShowMessage`/`Buzz`/`Reboot`.
- Modo online (Pro/Enterprise): `ConfigureOnlineModeAsync`/`DisableOnlineModeAsync`, eventos tipados (`OnlineEvents.Parse`), `OnlineAuthorization` (Grant/Deny/NotIdentified), `AuthorizeRemotelyAsync` e, no ASP.NET Core, `AddControlIdOnline` + `MapControlIdOnline` (identificação, botoeira, foto do usuário e `device_is_alive`).
- `AccessEventType` (códigos de evento) e `AccessLog.EventType`; `DeviceAction.OpenDoor`/`OpenSecBox`/`ReleaseTurnstile`.
- Cadastro remoto: `RemoteEnrollAsync` (síncrono, cancela no equipamento em timeout/cancelamento), `StartRemoteEnrollAsync` (resultado via Monitor como `RemoteEnrollNotification`), `CancelRemoteEnrollAsync` e `PushCommand.RemoteEnroll`.
- `IControlIdClient` (base de `IAccessClient` e `IRepClient`): `SendAsync<T>`, `SendRawAsync`, `SendBytesAsync` e `DownloadAsync` para chamar qualquer endpoint `.fcgi` com sessão e re-login automáticos.
- `AccessClient.GetConfigurationAsync` (`get_configuration.fcgi`), por seção ou várias seções de uma vez.
- `AccessClient.SetConfigurationAsync` para `set_configuration.fcgi`.
- Parser do AFD (`ControlId.Rep.Afd`): leiautes das Portarias 671/2021 e 1510/2009, todos os tipos de registro (1 a 7, trailer e assinatura), CRC-16/KERMIT, validação do trailer e do NSR, leitura em streaming e detecção de encoding.
- `RepClient.GetAfdFileFromNsrAsync`/`GetAfdFileFromDateAsync`/`GetAfdFileAsync` devolvem o AFD interpretado.
- `RepClient` (REP iDClass): about, informações do sistema, data/hora, usuários (PIS/CPF) e download de AFD (incluindo modo Portaria 671).

[Unreleased]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.8.0-alpha.1...HEAD
[0.8.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.7.0-alpha.1...v0.8.0-alpha.1
[0.7.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.6.0-alpha.1...v0.7.0-alpha.1
[0.6.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.5.0-alpha.1...v0.6.0-alpha.1
[0.5.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.4.3-alpha.1...v0.5.0-alpha.1
[0.4.3-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.4.2-alpha.1...v0.4.3-alpha.1
[0.4.2-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.4.1-alpha.1...v0.4.2-alpha.1
[0.4.1-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.4.0-alpha.1...v0.4.1-alpha.1
[0.4.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.3.0-alpha.1...v0.4.0-alpha.1
[0.3.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.2.0-alpha.1...v0.3.0-alpha.1
[0.2.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.1.0-alpha.2...v0.2.0-alpha.1
[0.1.0-alpha.2]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/releases/tag/v0.1.0-alpha.1
