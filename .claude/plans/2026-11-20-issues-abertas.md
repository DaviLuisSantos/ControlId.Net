# Plano: resolver as issues abertas do repositório
Slug: issues-abertas | Criado: 2026-11-20
Verificação final: `dotnet build -c Release -warnaserror --no-incremental && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net8.0 && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0 && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net48 && dotnet test tests/ControlId.Net.IntegrationTests -c Release --no-build && dotnet pack -c Release --no-build -o artifacts`
Sujos antes: nenhum

## Contexto
Há 20 issues abertas. O escopo cobre tudo que dá para fazer sem equipamento. Validações em iDFace real, cassetes gravados em hardware e a investigação do PIN UNKNOWN (#36) ficam registrados como pendência. A #47 já está superada, porque a base está na 0.10.0-alpha.1. Ela não gera tarefa, só entra no relatório final.

Decisões tomadas com o usuário:
- #50: criar propriedades `Local*` (`DateTime`, `Unspecified`) e marcar os `*Utc` como `[Obsolete]`.
- #37: o padrão de `MaxConcurrentRequests` passa para 1, com ADR e entrada no CHANGELOG.
- Sem commit, push ou mudança nas issues do GitHub. No fim, só relatório.

Regras para todas as tarefas:
- Durante as ondas, o build roda sem `-warnaserror`. Avisos RS0016/RS0017 de API pública nova são esperados, e quem preenche os `PublicAPI.Unshipped.txt` é a T20. Nenhuma outra tarefa edita esses arquivos, exceto a T15, nos arquivos do projeto novo.
- CS0618 (uso de `[Obsolete]`) não pode sobrar em `src/`, `tests/`, `samples/` nem `tools/`.
- Documentação (guia, COBERTURA, CHANGELOG, índice de ADR) é feita só pelas T21, T22 e T23. Os executores não editam esses arquivos.
- Mensagens, XML doc e testes seguem o padrão do repositório: português do Brasil, xUnit e `FakeDeviceHandler` (`tests/ControlId.Net.Tests/FakeDeviceHandler.cs`).

## Contratos

### Exceções (namespace `ControlId`, arquivo novo `src/ControlId.Net/ControlIdOutcomeExceptions.cs`, criado na T1)
Todas usam o construtor da base `ControlIdException(string message, HttpStatusCode? statusCode = null, int? deviceErrorCode = null, string? responseBody = null, Exception? innerException = null)`.
```csharp
public sealed class ControlIdTimeoutException : ControlIdException          // leitura sem resposta no prazo
{ public ControlIdTimeoutException(string message, Exception? innerException = null); }

public sealed class ControlIdConnectionException : ControlIdException       // não conectou; o comando não saiu (seguro repetir)
{ public ControlIdConnectionException(string message, Exception? innerException = null); }

public sealed class ControlIdOutcomeUnknownException : ControlIdException   // escrita enviada sem resposta (timeout ou conexão caída)
{ public ControlIdOutcomeUnknownException(string message, string command, bool timedOut, Exception? innerException = null);
  public string Command { get; } public bool TimedOut { get; } }

public class ControlIdDeviceErrorException : ControlIdException             // o equipamento respondeu com erro
{ public ControlIdDeviceErrorException(string message, HttpStatusCode? statusCode, int? deviceErrorCode, string? deviceMessage, string? responseBody);
  public string? DeviceMessage { get; } }

public sealed class ControlIdNotSupportedException : ControlIdDeviceErrorException // "Invalid command", "Invalid table" ou 404 no .fcgi
{ public ControlIdNotSupportedException(string command, string? firmware, HttpStatusCode? statusCode, string? deviceMessage, string? responseBody);
  public string Command { get; } public string? Firmware { get; } }

public enum ImageRejectionReason { Format = 0, Dimensions = 1, Size = 2 }
public sealed class ControlIdImageRejectedException : ControlIdException
{ public ControlIdImageRejectedException(ImageRejectionReason reason, long? userId, int? width, int? height, long byteCount, string message);
  public ImageRejectionReason Reason { get; } public long? UserId { get; } public int? Width { get; } public int? Height { get; } public long ByteCount { get; } }
```

### `ControlIdDeviceOptions` (`src/ControlId.Net/ControlIdDeviceOptions.cs`, alterado na T1)
- `const int DefaultMaxConcurrentRequests = 1;` (antes era 4, ver #37)
- `TimeSpan ConnectTimeout = 5 s` (> 0; acima de `Timeout` vale o `Timeout`; `Timeout.InfiniteTimeSpan` desliga)
- `bool ValidateImages = true`
- `int MaxImageLongSide = 1920`, `int MaxImageShortSide = 1080`, `long MaxImageBytes = 2 MiB`
- `long MaxImageBatchBytes = 1 MiB` (base64 somado por `user_set_image_list`), `int MaxImagesPerBatch = 100`
- `int MaxObjectsPerRequest = 200` (lotes de `create_objects` e `create_or_modify_objects`)
- `ExceptionResponseBody ExceptionResponseBody = ExceptionResponseBody.ErrorOnly`
- O enum novo fica em `src/ControlId.Net/ExceptionResponseBody.cs`: `public enum ExceptionResponseBody { ErrorOnly = 0, Full = 1, None = 2 }`. Com `ErrorOnly`, `ResponseBody` só guarda o corpo quando ele é o JSON de erro do equipamento (`{"error":..,"code":..}`). Nos outros casos fica `null`.
- `Validate()` cobre os campos novos.

### Handler HTTP (`src/ControlId.Net/Internal/HttpHandlers.cs`)
`internal static HttpMessageHandler CreatePrimaryHandler(int maxConnections, TimeSpan? connectTimeout, string? certificateSha256, bool acceptAnyCertificate)`
- A T1 cria esse método com o comportamento atual (`HttpClientHandler`) e troca as chamadas em `ControlIdTransport` e em `ControlIdDeviceServiceCollectionExtensions.AddHttpClient`. O REP passa `null`.
- A T3 implementa: no `net8.0`, `SocketsHttpHandler` com `ConnectTimeout`; no `netstandard2.0`, continua `HttpClientHandler` e o prazo é ignorado (documentado).

### Cache de capacidades no transporte (T2, usado pelas T9 e T12)
Em `ControlIdTransport`:
- `internal string? Firmware { get; set; }`
- `internal bool IsKnownUnsupported(string command)`
- `internal void MarkUnsupported(string command)`

Comando marcado lança `ControlIdNotSupportedException` antes de ir ao equipamento. Só "Invalid command" e 404 marcam o comando. "Invalid table" lança sem marcar.

### Hora local (T4, arquivo novo `src/ControlId.Net/Internal/DeviceLocalTime.cs`)
`internal static DateTime FromEpoch(long seconds)` devolve `DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(s).DateTime, DateTimeKind.Unspecified)`.

Propriedades novas:
- `AccessLog.LocalTime`
- `AccessDeviceEvent.LocalTimestamp`
- `AlarmLog.LocalTime`
- `ChangeLog.LocalTimestamp`
- `ExitButtonLog.LocalTime`
- `OnlineIdentificationEvent.LocalTime` (`DateTime?`)

Os `*Utc` correspondentes recebem `[Obsolete("O equipamento grava a hora local dele, não UTC. Use Local...")]`.

### Números de ADR
- 0025: `MaxConcurrentRequests` padrão 1
- 0026: exceções por desfecho
- 0027: capacidades por firmware e degradação
- 0028: hora local dos eventos
- 0029: política de compatibilidade e critérios para a 1.0 (#40)
- 0030: versões mínimas das dependências (#38)

## Ondas
- [x] Onda 0: T1 ✓ | verificação: `dotnet build -c Release && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0`
- [x] Onda 1: T2 ✓, T3 ✓, T4 ✓, T5 ✓, T6 ✓ | verificação: `dotnet build -c Release && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0`
- [x] Onda 2: T7 ✓, T8 ✓, T9 ✓, T10 ✓, T11 ✓ | verificação: `dotnet build -c Release && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0`
- [x] Onda 3: T12 ✓, T13 ✓, T14 ✓, T15 ✓ | verificação: `dotnet build -c Release && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0`
- [x] Onda 4: T16 ✓, T17 ✓, T18 ✓, T19 ✓ | verificação: `dotnet build -c Release && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0 && dotnet test tests/ControlId.Net.IntegrationTests -c Release --no-build`
- [x] Onda 5: T20 ✓, T21 ✓, T22 ✓, T23 ✓ | verificação: a verificação final

## T1
- O que: criar os contratos (exceções, enum `ExceptionResponseBody`, opções novas com validação, padrão de `MaxConcurrentRequests` = 1, `HttpHandlers.CreatePrimaryHandler` com o comportamento atual) e trocar as duas chamadas de criação de handler para usá-lo. Ajustar testes que assumem 4 como padrão.
- Arquivos: `src/ControlId.Net/ControlIdOutcomeExceptions.cs` (novo), `src/ControlId.Net/ExceptionResponseBody.cs` (novo), `src/ControlId.Net/ControlIdDeviceOptions.cs`, `src/ControlId.Net/Internal/HttpHandlers.cs`, `src/ControlId.Net/Internal/ControlIdTransport.cs` (só o construtor), `src/ControlId.Net.DependencyInjection/ControlIdDeviceServiceCollectionExtensions.cs`, `tests/ControlId.Net.Tests/ConcurrencyTests.cs`, `tests/ControlId.Net.Tests/CircuitBreakerTests.cs` (ajuste do padrão 1), `tests/ControlId.Net.Tests/OptionsContractTests.cs` (novo)
- Depende de: nada
- Pronto quando: compila nos dois TFMs, a validação recusa valores fora da faixa (teste) e a suíte unitária passa.
- modelo: sonnet | esforço: medium

## T2
- O que: classificar as falhas no transporte (#32, #34, #39, #53).
  - Conexão recusada ou prazo de conexão vencido: `ControlIdConnectionException`.
  - Timeout em leitura (`ReadRetry.IsReadOnly`): `ControlIdTimeoutException`.
  - Timeout ou conexão caída depois do envio, em comando que não é leitura: `ControlIdOutcomeUnknownException`.
  - Resposta de erro do equipamento: `ControlIdDeviceErrorException`. "Invalid command", "Invalid table" ou 404: `ControlIdNotSupportedException`.
  - Cache de capacidades conforme o contrato e `ExceptionResponseBody` aplicado em toda exceção.
  - As novas tentativas em leitura continuam iguais.
- Arquivos: `src/ControlId.Net/Internal/ControlIdTransport.cs`, `src/ControlId.Net/Internal/ReadRetry.cs`, testes existentes com asserção de tipo exato de `ControlIdException` (rodada de correção, ~20 arquivos em tests/), `tests/ControlId.Net.Tests/OutcomeExceptionTests.cs` (novo), `tests/ControlId.Net.Tests/NotSupportedTests.cs` (novo), `tests/ControlId.Net.Tests/ResponseBodyOptionTests.cs` (novo)
- Depende de: T1
- Pronto quando:
  - Há um teste por tipo de falha.
  - Timeout em escrita e timeout em leitura têm tipos diferentes.
  - A segunda chamada a um comando marcado não faz requisição.
  - Os testes existentes que capturam `ControlIdException` seguem passando.
- modelo: sonnet | esforço: high

## T3
- O que: implementar `CreatePrimaryHandler` (#53). No `net8.0`, usar `SocketsHttpHandler` com `ConnectTimeout`, `MaxConnectionsPerServer` e validação de certificado via `SslOptions.RemoteCertificateValidationCallback`, mantendo a lógica de `CertificatePinning`.
- Arquivos: `src/ControlId.Net/Internal/HttpHandlers.cs`, `src/ControlId.Net/Internal/CertificatePinning.cs`, `tests/ControlId.Net.Tests/ConnectTimeoutTests.cs` (novo)
- Depende de: T1
- Pronto quando:
  - Contra um endereço que não responde (ex.: 10.255.255.1, ou um listener que não aceita), a chamada falha perto do `ConnectTimeout` (1 s no teste, `Timeout` de 30 s).
  - Os testes de certificado fixado continuam passando.
- modelo: sonnet | esforço: medium

## T4
- O que: criar a hora local dos eventos (#50), conforme o contrato. Trocar o uso dos `*Utc` nos testes e no código pelas propriedades novas e escrever testes novos: um evento às 10:00 no relógio do equipamento aparece como 10:00 com `Kind` `Unspecified`.
- Arquivos: `src/ControlId.Net/Internal/DeviceLocalTime.cs` (novo), `src/ControlId.Net/Access/Models/AccessLog.cs`, `src/ControlId.Net/Access/Models/AccessObjects.cs`, `src/ControlId.Net/Access/Models/AccessMoreObjects.cs`, `src/ControlId.Net/Access/Online/ExitButtonEvent.cs`, `src/ControlId.Net/Access/Online/OnlineEvents.cs`, `tests/ControlId.Net.Tests/AccessReportsTests.cs`, `tests/ControlId.Net.Tests/OnlineModeTests.cs`, `tests/ControlId.Net.Tests/DeviceLocalTimeTests.cs` (novo)
- Depende de: T1
- Pronto quando: não sobra nenhum CS0618 e os testes novos passam.
- modelo: sonnet | esforço: medium

## T5
- O que: ajustes no modo online (#51, #52).
  - Mapear `device_is_alive.fcgi` e `session_is_valid.fcgi` em GET e POST.
  - Criar `public sealed class ControlIdOnlineOptions`, com `OnlineAuthorization FallbackAuthorization` (padrão: `OnlineAuthorization.Deny(message: "Sistema indisponível")`) e `TimeSpan DecisionTimeout` (padrão de 3 s, `Timeout.InfiniteTimeSpan` desliga). O usuário configura por `services.Configure<ControlIdOnlineOptions>`.
  - Em `HandleIdentificationAsync`: exceção, `null` ou prazo vencido cancelam o token do handler e respondem 200 com o fallback. Falha de parse também responde o fallback. Nunca responder 500.
  - Log novo (EventId 2014) com o motivo e contador `controlid.server.online.fallbacks` com a tag `controlid.online.fallback.reason` (`exception`, `null`, `timeout`).
  - Log de aviso quando o mesmo equipamento falhar o alive repetidamente é opcional: só entra se couber nos arquivos da tarefa.
- Arquivos: `src/ControlId.Net.AspNetCore/ControlIdOnlineEndpoints.cs`, `src/ControlId.Net.AspNetCore/ControlIdOnlineOptions.cs` (novo), `src/ControlId.Net.AspNetCore/Log.cs`, `src/ControlId.Net.AspNetCore/Internal/CallbackTelemetry.cs`, `tests/ControlId.Net.Tests/OnlineFallbackTests.cs` (novo), `tests/ControlId.Net.Tests/OnlineAliveMethodsTests.cs` (novo)
- Depende de: T1
- Pronto quando:
  - Handlers que lançam, que devolvem `null` e que demoram recebem 200 com o fallback dentro do prazo.
  - GET e POST em `device_is_alive.fcgi` passam com a mesma regra de token.
- modelo: sonnet | esforço: high

## T6
- O que: criar `public readonly struct CardNumber` (#57), com:
  - `static long FromFacility(int facility, int number)` e `static (int Facility, int Number) ToFacility(long value)`
  - `static long Parse(string)` e `static bool TryParse(string, out long)`, aceitando `123,45678`, `123.45678`, `123-45678` e `12345678`
  - `static string Format(long value)` no formato `facility,número`
  - Validação de faixa e XML doc citando `ignore_facility` e o `identifier_id` dos eventos.
- Arquivos: `src/ControlId.Net/Access/CardNumber.cs` (novo), `tests/ControlId.Net.Tests/CardNumberTests.cs` (novo)
- Depende de: T1
- Pronto quando: há testes de ida e volta com facility 0 e números de 5 e 8 dígitos, e com entradas inválidas.
- modelo: sonnet | esforço: low

## T7
- O que: simulador.
  - Opção `DisabledCommands` (lista; respondem 400 `{"error":"Invalid command"}`), também por `--DisabledCommands`.
  - `user_set_image_list.fcgi` (recusa um corpo acima de `MaxImageListBytes`, opção nova, com padrão de 2 MB), `set_system_time.fcgi` e `enroller_state.fcgi` (estado fixo `NORMAL_STATE`).
  - Contador de requisições por endpoint em `GET /simulator/state` (`requests`).
  - `time` dos eventos gerado como hora de parede local (`DateTime.Now` codificado como epoch), coerente com #50.
- Arquivos: `tools/ControlId.Net.Simulator/SimulatorOptions.cs`, `tools/ControlId.Net.Simulator/AccessApi.cs`, `tools/ControlId.Net.Simulator/SimulatorControlApi.cs`, `tools/ControlId.Net.Simulator/SimulatorApp.cs`, `tools/ControlId.Net.Simulator/DeviceState.cs`, `tools/ControlId.Net.Simulator/README.md`
- Depende de: T1
- Pronto quando: o simulador compila, sobe e responde aos comandos novos, e `DisabledCommands` funciona (teste manual por `curl` ou pelos testes de integração existentes).
- modelo: sonnet | esforço: medium

## T8
- O que: fotos (#56 e a parte de fotos da #35).
  - Ler largura e altura do cabeçalho JPEG (SOF0/1/2) e PNG (IHDR), sem biblioteca.
  - Com `ValidateImages`, `SetUserImageAsync` lança `ControlIdImageRejectedException` antes da requisição.
  - `SetUserImagesAsync`:
    - valida cada foto; a inválida vira `FaceImageResult` com `Success=false` e erro descritivo para aquele `UserId`;
    - divide o restante em lotes por `MaxImageBatchBytes` e `MaxImagesPerBatch`;
    - se o equipamento recusar um lote (erro do equipamento, exceto `ControlIdNotSupportedException`), cai para `user_set_image` foto a foto só naquele lote;
    - junta os resultados na ordem de entrada.
  - Mencionar `TestUserImageAsync` no XML doc como checagem prévia de rosto.
- Arquivos: `src/ControlId.Net/Internal/ImageHeader.cs` (novo), `src/ControlId.Net/Access/AccessClient.cs` (só os métodos de imagem), `src/ControlId.Net/Access/IAccessClient.cs` (só o XML doc dos métodos de imagem), `tests/ControlId.Net.Tests/ImageValidationTests.cs` (novo), `tests/ControlId.Net.Tests/ImageBatchingTests.cs` (novo), testes existentes com fotos falsas (AccessObjectsTests, CoverageWave02Tests, FacePhase3Tests, DocumentationCoverageTests, IntegrationTests/AccessClientEndToEndTests)
- Depende de: T1, T2
- Pronto quando:
  - Uma foto 4000×3000 falha sem requisição, com motivo e dimensões na exceção.
  - Um lote de fotos que passa de 1 MB vira várias requisições.
  - Um lote recusado cai para foto a foto.
- modelo: sonnet | esforço: high

## T9
- O que: capacidades por firmware (#34).
  - Matriz interna de "não suportado" por firmware (6.9.12 e 6.13.1), copiada de `docs/COBERTURA.md`, seções "Observado num iDFace real".
  - `public sealed class DeviceCapabilities`, com `Model`, `Firmware`, `IReadOnlyCollection<string> UnsupportedCommands` e `bool Supports(string command)`.
  - `Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)` em `IAccessClient`. Ele lê `system_information`, preenche `transport.Firmware` e marca no cache os comandos da matriz.
  - Teste que lê `docs/COBERTURA.md` e garante que a matriz e o documento concordam.
- Arquivos: `src/ControlId.Net/Access/IAccessClient.Capabilities.cs` (novo), `src/ControlId.Net/Access/AccessClient.Capabilities.cs` (novo), `src/ControlId.Net/Access/Models/DeviceCapabilities.cs` (novo), `src/ControlId.Net/Internal/FirmwareCapabilityMatrix.cs` (novo), `tests/ControlId.Net.Tests/CapabilitiesTests.cs` (novo), `tests/ControlId.Net.Tests/Cobertura/CapabilityMatrixDocsTests.cs` (novo)
- Depende de: T2
- Pronto quando:
  - Chamar recurso ausente num 6.9.12 conhecido lança `ControlIdNotSupportedException` com o comando e o firmware, sem requisição.
  - O teste de concordância com o COBERTURA.md passa.
- modelo: sonnet | esforço: high

## T10
- O que: `Task<RemoteEnrollResult> EnrollAndWaitAsync(RemoteEnrollRequest request, TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)` (#36).
  - Inicia o cadastro assíncrono e acompanha por `enroller_state` até sair do estado de cadastro.
  - Devolve o resultado montado de `LastEnrollSucceeded` e `LastEnrollError`.
  - Prazo vencido ou cancelamento chamam `TryCancelRemoteEnrollAsync`.
  - XML doc com as limitações do iDFace (`save:false`, PIN `UNKNOWN`).
- Arquivos: `src/ControlId.Net/Access/IAccessClient.Enrollment.cs` (novo), `src/ControlId.Net/Access/AccessClient.Enrollment.cs` (novo), `tests/ControlId.Net.Tests/EnrollAndWaitTests.cs` (novo)
- Depende de: T1
- Pronto quando: há testes de sucesso, falha, prazo e cancelamento com `FakeDeviceHandler`.
- modelo: sonnet | esforço: medium

## T11
- O que: Push sem `transactions` (parte da #35).
  - Quando o resultado de um lote indicar recusa do formato (`Invalid member 'endpoint'`, "Invalid command" ou equivalente), lembrar por equipamento que ele não aceita `transactions`.
  - Reenfileirar os comandos do lote como envios individuais, sem perder os resultados nem os awaiters.
  - Os próximos lotes para esse equipamento já saem individuais.
  - Opção `PushServerOptions.UseTransactions` (`bool?`, `null` = automático).
- Arquivos: `src/ControlId.Net/Access/Push/PushServer.cs`, `src/ControlId.Net/Access/Push/PushServerOptions.cs`, `tests/ControlId.Net.Tests/PushTransactionFallbackTests.cs` (novo)
- Depende de: T1
- Pronto quando: um lote recusado pelo equipamento entrega todos os resultados via comandos individuais (teste), e os testes de Push existentes passam.
- modelo: sonnet | esforço: high

## T12
- O que: objetos (#33 e a parte de objetos da #35).
  - `CreateOrModifyObjectsAsync`: se o comando não for suportado (`ControlIdNotSupportedException` ou cache), usar `modify_objects` filtrando por id em cada item e `create_objects` só para os que deram `changes == 0`. Os valores precisam de `id`; sem `id`, lançar `ArgumentException`.
  - `CreateObjectsAsync` e o caminho nativo de `CreateOrModifyObjectsAsync` em lotes de `MaxObjectsPerRequest`, juntando ids e contagens.
  - XML doc sobre a atomicidade do fallback.
- Arquivos: `src/ControlId.Net/Access/AccessClient.cs` (só os métodos de objetos), `src/ControlId.Net/Access/IAccessClient.cs` (só o XML doc desses métodos), `tests/ControlId.Net.Tests/UpsertFallbackTests.cs` (novo), `tests/ControlId.Net.Tests/ObjectBatchingTests.cs` (novo)
- Depende de: T2, T8 (mesmo arquivo)
- Pronto quando:
  - O upsert passa com e sem `create_or_modify_objects` (Invalid command e 404).
  - Repetir o upsert não muda o estado.
  - 450 objetos viram 3 requisições.
- modelo: sonnet | esforço: high

## T13
- O que: guardião de configuração (#55).
  - No pacote DI: `public static IServiceCollection AddControlIdConfigurationGuard(this IServiceCollection services, Action<ControlIdConfigurationGuardOptions> configure)`.
  - Opções: `Interval` (padrão de 5 min), `ClockTolerance` (padrão de 60 s, `null` desliga), `TimeZone` (`TimeZoneInfo`, padrão `Local`) e `Devices` (nome do equipamento → `DesiredDeviceConfiguration`). `DesiredDeviceConfiguration` tem `bool? Online`, `long? OnlineServerId`, `MonitorTarget? Monitor` (Hostname, Port, Path, RequestTimeout) e `IDictionary<string, IDictionary<string,string>> Settings` (seções extras, como fuso e antipassback).
  - `BackgroundService` interno que, a cada ciclo, usa `ControlIdDevices.RunAsync` sobre `IControlIdClientFactory`. Lê com `GetConfigurationAsync` e só escreve o que difere. Acerta o relógio com `SetSystemTimeAsync` quando `system_information.time` diverge além da tolerância.
  - Log, contador `controlid.guard.corrections` e um `IHealthCheck` registrável (`IHealthChecksBuilder AddControlIdConfigurationGuard()`) com o último ciclo.
  - Adiciona `Microsoft.Extensions.Hosting.Abstractions` ao pacote DI.
- Arquivos: `src/ControlId.Net.DependencyInjection/ControlIdConfigurationGuard.cs` (novo; serviço e extensões), `src/ControlId.Net.DependencyInjection/ControlIdConfigurationGuardOptions.cs` (novo), `src/ControlId.Net.DependencyInjection/ControlId.Net.DependencyInjection.csproj`, `Directory.Packages.props`, `tests/ControlId.Net.Tests/ConfigurationGuardTests.cs` (novo)
- Depende de: T4
- Pronto quando:
  - Com o fake, `general.online` alterado por fora é restaurado no ciclo seguinte.
  - Configuração igual não gera `set_configuration`.
  - Um equipamento fora do ar não trava os demais.
- modelo: sonnet | esforço: high

## T14
- O que: descoberta de equipamentos (#58).
  - `public static class ControlIdDiscovery` com `Task<IReadOnlyList<DiscoveredDevice>> ScanAsync(IEnumerable<Uri> addresses, IEnumerable<DiscoveryCredential> credentials, DiscoveryOptions? options = null, CancellationToken cancellationToken = default)`.
  - `static IEnumerable<Uri> ExpandCidr(string cidr, int port = 80, string scheme = "http")`.
  - `DiscoveryOptions`: `MaxParallelism` 32, `ConnectTimeout` 1 s, `Timeout` 5 s e `bool TryFactoryDefaults` (padrão `false`).
  - `DiscoveredDevice`: `Uri Address`, `DiscoveryStatus Status` (`Found`, `PortClosed`, `NotControlId`, `CredentialRejected`, `Error`), `string? Model`, `Firmware`, `Serial`, `Mac`, `string? CredentialLogin` (nunca a senha).
  - Nada de senha em log ou exceção.
- Arquivos: `src/ControlId.Net/Discovery/ControlIdDiscovery.cs` (novo), `src/ControlId.Net/Discovery/DiscoveryModels.cs` (novo), `tests/ControlId.Net.Tests/DiscoveryTests.cs` (novo)
- Depende de: T2, T3
- Pronto quando: os testes com fakes cobrem os 5 status, e a varredura de endereços sem resposta termina perto do `ConnectTimeout`.
- modelo: sonnet | esforço: high

## T15
- O que: pacote `ControlId.Net.Testing` (#31).
  - `RecordingHandler : DelegatingHandler` grava requisição e resposta num cassete JSON e mascara `session`, `login`, `password`, `salt`, `image`, `template` e campos configuráveis.
  - `ReplayHandler` responde pelo cassete e falha com mensagem do que divergiu (ordem, endpoint ou corpo).
  - `Cassette` com load e save.
  - Projeto com `net8.0;netstandard2.0`, PublicAPI próprios preenchidos e `PackageValidationBaselineVersion` vazio, porque o pacote é novo.
  - Guia de migração em docs.
- Arquivos: `src/ControlId.Net.Testing/` (novo: csproj, `RecordingHandler.cs`, `ReplayHandler.cs`, `Cassette.cs`, `PublicAPI.Shipped.txt`, `PublicAPI.Unshipped.txt`), `ControlId.Net.sln`, `tests/ControlId.Net.Tests/ControlId.Net.Tests.csproj`, `tests/ControlId.Net.Tests/RecordReplayTests.cs` (novo), `docs/migrando-de-cliente-proprio.md` (novo)
- Depende de: T1
- Pronto quando: há um teste que grava contra `FakeDeviceHandler`, reproduz sem ele e falha com mensagem clara quando o corpo diverge.
- modelo: sonnet | esforço: medium

## T16
- O que: versões mínimas no `netstandard2.0` (#38).
  - Descobrir a menor versão segura de cada dependência pelas APIs usadas (ex.: `System.Text.Json` >= 8.0.5).
  - Declarar com `VersionOverride` (ou propriedades no `Directory.Packages.props`) só no alvo `netstandard2.0`.
  - Criar um projeto de teste `tests/ControlId.Net.MinDeps.Tests` (net48) que fixa as mínimas e roda um smoke do cliente com fake.
  - Adicionar o projeto à solução e a um job da CI.
  - Escrever o ADR 0030.
- Arquivos: `src/ControlId.Net/ControlId.Net.csproj`, `src/ControlId.Net.DependencyInjection/ControlId.Net.DependencyInjection.csproj`, `src/ControlId.Net.Testing/ControlId.Net.Testing.csproj`, `tests/ControlId.Net.Tests/ControlId.Net.Tests.csproj` (Configuration no net48, correção), `Directory.Packages.props`, `tests/ControlId.Net.MinDeps.Tests/` (novo), `ControlId.Net.sln`, `.github/workflows/ci.yml`, `docs/adr/0030-versoes-minimas-das-dependencias.md` (novo)
- Depende de: T13, T15 (mesmos arquivos)
- Pronto quando: `dotnet test tests/ControlId.Net.MinDeps.Tests -c Release` passa com as mínimas e o `dotnet pack` mostra as dependências mínimas no nuspec.
- modelo: sonnet | esforço: high

## T17
- O que: sincronizar pessoas pelo estado desejado (#54).
  - `Task<UserSyncResult> SyncUserAsync(DesiredUser desired, CancellationToken cancellationToken = default)`.
  - `Task<IReadOnlyList<UserSyncResult>> SyncUsersAsync(IEnumerable<DesiredUser> desired, UserSyncOptions? options = null, CancellationToken cancellationToken = default)`.
  - `DesiredUser` (`Id`, `Name`, `Registration`, `BeginTime`/`EndTime` como `DateTime?` local, `Password` em texto, cifrada com `HashPasswordAsync` só quando muda, `Cards` (`IReadOnlyCollection<long>`), `GroupIds`, `byte[]? Photo`).
  - `UserSyncOptions` (`RemoveMissing` + `DataLossConfirmation`).
  - `UserSyncResult` (`UserId`, `bool Success`, listas do que mudou e erros por item).
  - Lê usuários, cartões, `user_groups` e o `image_timestamp`. Compara a foto por hash SHA-256 guardado num campo derivado; se não houver como comparar, compara pelo hash local mantido por chamada e documenta.
  - Aplica a diferença com o upsert (T12) e as fotos (T8). Uma falha por pessoa não derruba o lote.
  - Teste de integração com o simulador: a segunda execução não faz nenhuma escrita (contador do T7).
- Arquivos: `src/ControlId.Net/Access/IAccessClient.Sync.cs` (novo), `src/ControlId.Net/Access/AccessClient.Sync.cs` (novo), `src/ControlId.Net/Access/Models/UserSync.cs` (novo), `tests/ControlId.Net.Tests/UserSyncTests.cs` (novo), `tests/ControlId.Net.IntegrationTests/UserSyncEndToEndTests.cs` (novo)
- Depende de: T7, T8, T12
- Pronto quando: idempotência provada pelo contador e um cartão recusado aparece só no relatório daquela pessoa.
- modelo: sonnet | esforço: high

## T18
- O que: teste de dados pessoais (#39).
  - Exercitar comandos com nome, cartão, foto, PIN e senha (incluindo erro do equipamento que ecoa o corpo).
  - Capturar logs (`ILoggerProvider` de teste), spans (`ActivityListener`) e métricas (`MeterListener`).
  - Verificar que nenhum valor sensível aparece neles nem em `Message`/`ResponseBody` com `ErrorOnly`.
  - Se achar vazamento fora do arquivo da tarefa, parar e reportar.
- Arquivos: `tests/ControlId.Net.Tests/PersonalDataLeakTests.cs` (novo)
- Depende de: T2, T8, T12
- Pronto quando: o teste passa e cobre logs, spans, métricas e exceções.
- modelo: sonnet | esforço: medium

## T19
- O que: testes de ponta a ponta com o simulador.
  - Upsert com `DisabledCommands=create_or_modify_objects`.
  - Descoberta em 3 simuladores mais 1 porta fechada.
  - Guardião restaurando `general.online`.
  - Lote de fotos.
  - Hora local de um evento.
- Arquivos: `tests/ControlId.Net.IntegrationTests/UpsertEndToEndTests.cs`, `DiscoveryEndToEndTests.cs`, `ConfigurationGuardEndToEndTests.cs`, `ImageBatchEndToEndTests.cs`, `LocalTimeEndToEndTests.cs` (todos novos), `tests/ControlId.Net.IntegrationTests/RunningSimulator.cs` (aceitar opções extras)
- Depende de: T7, T8, T12, T13, T14
- Pronto quando: `dotnet test tests/ControlId.Net.IntegrationTests` passa.
- modelo: sonnet | esforço: medium

## T20
- O que: preencher a API pública. Rodar `dotnet format analyzers ControlId.Net.sln --diagnostics RS0016 RS0017 RS0036 RS0037` (ou editar à mão) para levar a API nova a `PublicAPI.Unshipped.txt`. Depois, `dotnet build -c Release -warnaserror --no-incremental` sem erro. Aviso restante fora dos PublicAPI: parar e reportar o arquivo. Em seguida, `dotnet pack -c Release --no-build -o artifacts`: membros novos em interfaces (`IAccessClient`) quebram a validação de pacote (CP0006). Conforme o ADR 0006, gerar `CompatibilitySuppressions.xml` com `-p:ApiCompatGenerateSuppressionFile=true` só nos projetos afetados, conferir que só contém o esperado (membros novos de interface) e repetir o pack sem erro.
- Arquivos: `src/ControlId.Net/PublicAPI.Unshipped.txt`, `src/ControlId.Net.DependencyInjection/PublicAPI.Unshipped.txt`, `src/ControlId.Net.AspNetCore/PublicAPI.Unshipped.txt`, `src/ControlId.Net/CompatibilitySuppressions.xml` (novo); na rodada de correção, os avisos CA em CardNumber.cs, AccessClient.Sync.cs, AccessClient.cs, ControlIdDeviceOptions.cs, CertificatePinning.cs, RecordingHandler.cs e ReplayHandler.cs
- Depende de: Ondas 1 a 4
- Pronto quando: o build com `-warnaserror` e o `dotnet pack` passam.
- modelo: sonnet | esforço: medium

## T21
- O que: atualizar o `docs/guia.md` com seções ou trechos para:
  - hora local (#50);
  - `ConnectTimeout` e o caso do `HttpClient` próprio (#53);
  - exceções por desfecho e o exemplo da porta com `doors_state` (#32);
  - capacidades (#34);
  - upsert e lotes (#33, #35);
  - validação de fotos e `user_test_image` (#56);
  - `CardNumber` e `ignore_facility` (#57);
  - sincronização (#54);
  - guardião (#55);
  - descoberta (#58);
  - fallback e alive GET (#51, #52);
  - `EnrollAndWaitAsync` (#36);
  - o que o pacote registra e o que não registra, para LGPD (#39);
  - `MaxConcurrentRequests` = 1 (#37);
  - pacote Testing, com link para o guia de migração (#31).
- Arquivos: `docs/guia.md`
- Depende de: Ondas 1 a 4
- Pronto quando: cada issue citada tem trecho no guia, com exemplo de código que compila com as assinaturas reais.
- modelo: sonnet | esforço: medium

## T22
- O que: documentação de registro.
  - `docs/COBERTURA.md`: métodos GET e POST do alive, limites de foto por modelo, fallback do upsert e do Push, e uma nota de que a matriz em código espelha as listas.
  - `CHANGELOG.md` `[Unreleased]`: `Added`, `Changed` (padrão 1 e `*Utc` obsoletos) e `Fixed` (hora, 500 no online e 405 no alive GET).
  - `docs/adr/README.md`: linhas de 0025 a 0030.
  - `docs/toc.yml`: guia de migração.
  - `README.md`, `README.pt-BR.md` e `src/PACKAGE.md`: só se listarem recursos ou pacotes (incluir o Testing).
- Arquivos: `docs/COBERTURA.md`, `CHANGELOG.md`, `docs/adr/README.md`, `docs/toc.yml`, `README.md`, `README.pt-BR.md`, `src/PACKAGE.md`
- Depende de: Ondas 1 a 4
- Pronto quando: o CHANGELOG cita cada issue resolvida e o índice de ADR está completo.
- modelo: sonnet | esforço: medium

## T23
- O que: escrever os ADRs de 0025 a 0029 no formato do 0011 (`# NNNN. Título`, `Status: Aceita (2026-11-20)`, Contexto, Decisão, Consequências).
  - O 0026 avalia a variante `TryXxx` e registra que fica para depois.
  - O 0029 traz a política de compatibilidade (SemVer, o que conta como quebra, prazo do `[Obsolete]` e estratégia de crescimento de `IAccessClient`) e os critérios de saída do alpha, cada um com a issue ligada (#31, #32, #34, #38). Critério sem issue fica marcado como "issue a abrir".
- Arquivos: `docs/adr/0025-uma-requisicao-por-vez.md`, `docs/adr/0026-excecoes-por-desfecho.md`, `docs/adr/0027-capacidades-por-firmware.md`, `docs/adr/0028-hora-local-dos-eventos.md`, `docs/adr/0029-compatibilidade-e-criterios-para-a-1-0.md` (todos novos)
- Depende de: Ondas 1 a 4
- Pronto quando: os 5 arquivos existem e são coerentes com o código.
- modelo: sonnet | esforço: low

## Bloqueios
(vazio)

## Pendências que ficam para hardware (relatório final)
- #33 e #35: validar num iDFace e medir 1 mil e 10 mil usuários.
- #31: gravar cassetes nos firmwares 6.9.12 e 6.13.1.
- #36: investigar o PIN `UNKNOWN` e testar no firmware V6.26.
- #40: validar em outros firmwares e modelos.
- #47: superada pela base 0.10 (pode ser fechada).
