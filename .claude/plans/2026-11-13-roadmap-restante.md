# PLAN — Roadmap restante do ControlId.Net

## Contexto
Itens abertos do roadmap: #10 Stryker, #11 AOT, #13 dashboard, #14 DocFX, #25 logs, #26 simulador REP/Push/online.
Decisões do usuário: ILogger via `ControlIdDeviceOptions.LoggerFactory`; Stryker por workflow manual + nota no README;
GitHub Pages ligado (fonte: Actions); dashboard em Blazor Server.
Regras do repo: commits/PR em português, sem coautoria de IA; build com `-warnaserror`; API pública em
`PublicAPI.Unshipped.txt`; CHANGELOG `[Unreleased]`; XML docs em português; ADRs em `docs/adr/`.

**#11 (AOT), escopo honesto:** ~135 tipos serializados por reflection e ~65 corpos `Dictionary<string,object>`.
Marcar `IsAotCompatible` seria falso. Entrega: projeto de prova que publica com AOT, contagem de avisos IL2026/IL3050
por área e ADR com o caminho (fase 1 anotações, fase 2 source-gen). Não liga `IsAotCompatible`.

## Contratos

**C1 — Logs (núcleo)**
- `ControlIdDeviceOptions.LoggerFactory : Microsoft.Extensions.Logging.ILoggerFactory?` (padrão null = `NullLogger`).
  Pacote `Microsoft.Extensions.Logging.Abstractions` 10.0.15 no `ControlId.Net` (todos os alvos).
- Categoria `ControlId.Net` (constante pública `ControlIdTelemetry.LoggerCategory = "ControlId.Net"`).
- `internal static partial class Log` (LoggerMessage gerado) em `src/ControlId.Net/Internal/Log.cs`, EventIds:
  1001 LoginSucceeded (Debug: device), 1002 LoginRefused (Warning: device, status), 1003 SessionExpired (Information:
  device, command), 1004 Retrying (Warning: device, command, attempt, reason), 1005 DeviceError (Warning: device,
  command, status, deviceCode, message), 1006 CommunicationFailed (Warning: device, command, exception).
  `device` = `BaseAddress.Authority`. Nunca logar sessão, login, senha nem corpo.
- DI: `ControlIdClientFactory` recebe `ILoggerFactory` do container e o passa por **construtor interno** de
  `AccessClient`/`RepClient` (não alterar a instância de options do `IOptionsMonitor`). Explícito em options vence.

**C2 — Simulador, tipo de equipamento**
- `SimulatorOptions.Kind : "Access" | "Rep"` (padrão `Access`; `--Simulator:Kind=Rep`). `SimulatorApp.Build` mapeia
  `AccessApi` ou `RepApi` conforme o tipo; `/health` e `/simulator/state` nos dois.
- REP em HTTP puro; mesmos formatos de erro (`{"error","code"}`), sessão `?session=`, `mode=671` aceito e ignorado
  onde não se aplica.
- Rota de controle REP: `POST /simulator/punch {"cpf":N}` grava marcação (tipo 3, leiaute 671, NSR crescente, CRC-16/KERMIT).

**C3 — Simulador, Push e online (tipo Access)**
- Push: com `push_server.push_remote_address` configurado, um `BackgroundService` faz `GET http://{addr}/push?deviceId={id}&uuid={uuid}`
  a cada `push_request_period` s; executa o comando (simples ou `transactions`) pelos mesmos handlers do `AccessApi`
  (método interno `ExecuteAsync(command, body)` sem sessão) e responde `POST /result?deviceId&uuid` com
  `{"response":...}` / `{"error":...}` / `{"transactions_results":[...]}`.
- Online: com `general.online=1`, `/simulator/identify` consulta o servidor (`devices` com `id = online_client.server_id`,
  campo `ip` = endereço base) em `new_user_identified.fcgi` ou `new_card.fcgi` (form-urlencoded), aplica `result.event`
  (7 abre porta e loga; 6/3 loga) e cai na decisão local se o servidor falhar (`max_request_attempts`).
  Responde `device_is_alive` a cada `alive_interval`.

## Tarefas

## T1 — Logs estruturados no núcleo (#25)
- Arquivos: `src/ControlId.Net/ControlIdDeviceOptions.cs`, `src/ControlId.Net/ControlIdTelemetry.cs`,
  `src/ControlId.Net/Internal/ControlIdTransport.cs`, `src/ControlId.Net/Internal/Log.cs` (novo),
  `src/ControlId.Net/Access/AccessClient.cs`, `src/ControlId.Net/Rep/RepClient.cs` (só construtor interno com ILogger),
  `src/ControlId.Net/ControlId.Net.csproj`, `src/ControlId.Net/PublicAPI.Unshipped.txt`,
  `tests/ControlId.Net.Tests/LoggingTests.cs` (novo).
- Dependências: nenhuma.
- Pronto: C1 implementado; testes com logger em memória cobrem login, sessão expirada, nova tentativa, erro do equipamento
  e ausência de sessão/senha no texto; build net8/netstandard2.0 sem avisos; testes nos 3 alvos.
- modelo: sonnet · esforço: high

## T2 — Simulador do REP iDClass (#26, parte 1)
- Arquivos: `tools/ControlId.Net.Simulator/SimulatorOptions.cs`, `.../SimulatorApp.cs`, `.../RepApi.cs` (novo),
  `.../RepState.cs` (novo), `.../AfdWriter.cs` (novo), `tests/ControlId.Net.IntegrationTests/RunningSimulator.cs`,
  `tests/ControlId.Net.IntegrationTests/RepEndToEndTests.cs` (novo).
- Dependências: nenhuma.
- Pronto: C2; comandos `login`, `session_is_valid`, `logout`, `get_about`, `get_info`, `get_system_information`,
  `get_system_date_time`/`set_system_date_time` (fuso com `mode=671`), `count_users`, `load_users` (limit/offset),
  `add_users`, `update_users`, `remove_users`, `get_afd` (`initial_nsr`/`initial_date`, ISO-8859-1); `RunningSimulator`
  aceita o tipo; ≥6 testes de integração com `RepClient` real, inclusive AFD lido pelo `AfdParser` com CRC válido.
- modelo: sonnet · esforço: high

## T3 — Simulador: Push e modo online (#26, parte 2)
- Arquivos: `tools/ControlId.Net.Simulator/AccessApi.cs`, `.../DeviceState.cs`, `.../SimulatorControlApi.cs`,
  `.../SimulatorApp.cs`, `.../PushClient.cs` (novo), `.../OnlineClient.cs` (novo),
  `tests/ControlId.Net.IntegrationTests/PushOnlineEndToEndTests.cs` (novo).
- Dependências: T2.
- Pronto: C3; testes de integração: comando via `PushServer` (`MapControlIdPush`) executado pelo simulador (simples e em
  lote); identificação no modo online liberada e negada por um `IOnlineAccessHandler`, com contingência quando o servidor
  cai; `ConfigureOnlineModeAsync`/`ConfigurePushAsync` do cliente funcionam contra o simulador.
- modelo: sonnet · esforço: high

## T4 — Mutation testing com Stryker.NET (#10)
- Arquivos: `stryker-config.json` (novo, raiz), `.github/workflows/mutation.yml` (novo), `.gitignore`.
- Dependências: nenhuma.
- Pronto: config mira `src/ControlId.Net` com `tests/ControlId.Net.Tests` em net8.0 (confirmar opção no `--help`);
  workflow `workflow_dispatch` instala o Stryker global com versão fixa, roda e publica o HTML como artefato e a nota no
  job summary; `StrykerOutput/` e `coverage/` no `.gitignore`; rodar localmente e informar a nota no relatório
  (se passar de 20 min, limitar a `Rep/Afd/**` e `Internal/**` e informar o escopo).
- modelo: sonnet · esforço: medium

## T5 — Site de documentação com DocFX (#14)
- Arquivos: `docs/docfx.json` (novo), `docs/toc.yml` (novo), `docs/index.md` (novo), `.github/workflows/docs.yml` (novo),
  `docs/.gitignore` (novo: `_site/`, `api/`).
- Dependências: nenhuma.
- Pronto: `docfx docs/docfx.json` gera API dos 3 pacotes (net8.0) + guia, cobertura e ADRs, sem erros; workflow em push na
  `main` (paths `src/**`, `docs/**`) e manual, com `actions/upload-pages-artifact` + `actions/deploy-pages`; Pages ligado
  via `gh api -X POST repos/DaviLuisSantos/ControlId.Net/pages -f build_type=workflow`; docfx instalado no workflow com
  versão fixa. Informar a URL do site no relatório.
- modelo: sonnet · esforço: medium

## T6 — Avaliação de Native AOT (#11)
- Arquivos: `tools/ControlId.Net.AotProbe/` (novo: csproj `PublishAot=true` net10.0 + `Program.cs` que usa
  `AccessClient`, `RepClient` e `AfdParser`), `docs/adr/0015-native-aot.md` (novo).
- Dependências: nenhuma. Não adicionar à solution.
- Pronto: `dotnet publish -c Release -r win-x64` do probe executado; avisos IL2026/IL3050/IL2xxx contados por área
  (JSON/modelos, ControlIdObjects, ConfigurationSection, AFD); ADR 0015 com os números, a decisão (não ligar
  `IsAotCompatible`; AFD já é seguro) e as fases para chegar lá.
- modelo: sonnet · esforço: medium

## T7 — Logs no pacote de DI e no ASP.NET Core (#25, parte 2)
- Arquivos: `src/ControlId.Net.DependencyInjection/ControlIdClientFactory.cs`,
  `src/ControlId.Net.DependencyInjection/ControlIdDeviceServiceCollectionExtensions.cs`,
  `src/ControlId.Net.AspNetCore/ControlIdEndpointRouteBuilderExtensions.cs`,
  `src/ControlId.Net.AspNetCore/ControlIdOnlineEndpoints.cs`, `src/ControlId.Net.AspNetCore/Log.cs` (novo),
  `src/Directory.Build.props` (remover CA1848 do NoWarn e o comentário),
  `tests/ControlId.Net.Tests/DependencyInjectionTests.cs`, `docs/adr/0016-logs-por-options.md` (novo).
- Dependências: T1.
- Pronto: clientes do DI logam pelo `ILoggerFactory` do container; as 4 chamadas do ASP.NET Core viram LoggerMessage;
  CA1848 ativo sem avisos; teste de DI confirma o log; ADR 0016.
- modelo: sonnet · esforço: medium

## T8 — Dashboard em tempo real (Blazor Server) (#13)
- Arquivos: `samples/ControlId.Net.Dashboard/` (novo: csproj net10.0 Web referenciando `ControlId.Net.AspNetCore`,
  `Program.cs`, componentes Razor, CSS, `Dockerfile`), `samples/docker-compose.yml`.
- Dependências: nenhuma (usa só API pública existente).
- Pronto: recebe Monitor (`AddControlIdMonitor`/`MapControlIdMonitor`), mantém os últimos eventos em memória e atualiza a
  página ao vivo (eventos de acesso com nome do usuário, estado da porta, equipamento online); botões "abrir porta" e
  "identificar usuário" (chama `/simulator/identify`) via `AccessClient`; ao iniciar, configura o Monitor do equipamento
  apontando para si; `docker compose -f samples/docker-compose.yml up` sobe simulador + dashboard e funciona sem passo
  manual; build sem avisos; layout limpo e legível.
- modelo: sonnet · esforço: high

## T9 — Integração final e documentação
- Arquivos: `ControlId.Net.sln`, `README.md`, `README.pt-BR.md`, `CHANGELOG.md`, `docs/guia.md`, `docs/adr/README.md`,
  `tools/ControlId.Net.Simulator/README.md`, `.github/workflows/ci.yml`.
- Dependências: T1–T8.
- Pronto: dashboard na solution; CI faz smoke do simulador REP (`--Simulator:Kind=Rep`); READMEs com roadmap marcado,
  contagem de testes atual, linha de mutation score (de T4), links do site (T5), dashboard e AOT (T6); guia com logs e
  simulador REP/Push/online; ADRs 0015/0016 no índice; CHANGELOG `[Unreleased]` com tudo.
- modelo: sonnet · esforço: medium

## Ondas
- **Onda 1:** T1, T2, T4, T5, T6 — ✅ concluída (verificação passou)
- **Onda 2:** T3, T7, T8 — ✅ concluída (verificação passou)
- **Onda 3:** T9 — ✅ concluída (verificação passou)

## Verificação (fim de cada onda)
```
dotnet build -c Release -warnaserror --no-incremental
dotnet test -c Release --no-build
```
Onda 1 também: relatórios de T4 (nota), T5 (URL e build docfx ok), T6 (contagem de avisos).
Onda 2 também: `dotnet build samples/ControlId.Net.Dashboard -c Release -warnaserror`.
Onda 3 também: `dotnet pack -c Release --no-build -o artifacts` (validação de API).

## Entrega
Uma branch por onda (`roadmap/onda-<n>`), PR com a CI verde e merge (rebase) antes da onda seguinte.

## Resultados para a T9
- Mutation score (T4): 68,08% em src/ControlId.Net (1216 mortos, 23 timeout, 425 sobreviventes), Stryker 5.1.0, net8.0. Rodar de dentro de tests/ControlId.Net.Tests.
- Site (T5): https://daviluissantos.github.io/ControlId.Net/ (Pages ainda precisa ser ligado pelo usuário).
- AOT (T6): ADR 0015; sonda em tools/ControlId.Net.AotProbe; 3 IL2026, 3 IL3050, 1 IL2070 (agrupados por causa raiz).
- Simulador REP (T2): --Simulator:Kind=Rep, POST /simulator/punch {cpf}.
- Testes após a onda 2: 494 (net8/net10), 481 (net48), 32 integração.
- Dashboard (T8): samples/ControlId.Net.Dashboard, http://localhost:8081 no compose; dotnet run na porta 5080. Circuito interativo do Blazor não testado.
- Logs (T7): ADR 0016; EventIds 1001-1006 (núcleo) e 2001-2004 (ASP.NET Core); CA1848 ativo.

## Pendências (para o Davi)
- [ ] Ligar o GitHub Pages (fonte: GitHub Actions): `gh api -X POST repos/DaviLuisSantos/ControlId.Net/pages -f build_type=workflow`; depois rodar o workflow `docs.yml` e conferir https://daviluissantos.github.io/ControlId.Net/.
- [ ] Testar o dashboard no navegador (`docker compose -f samples/docker-compose.yml up`, http://localhost:8081): botões "Abrir porta", "Identificar", "Cartão desconhecido" e atualização ao vivo pelo circuito do Blazor (só o HTML renderizado no servidor foi conferido).
- [ ] Gravar um GIF do dashboard e colocar no README.
- [ ] Opcional: instalar a carga de trabalho C++ do Visual Studio e gerar o binário nativo da sonda `tools/ControlId.Net.AotProbe` (a contagem de avisos da ADR 0015 não muda).
- [ ] Acompanhar o teste `PushOnlineEndToEndTests.Executes_a_batch_of_commands_and_reports_each_result`: estourou o prazo uma vez na CI (causa provável corrigida, não confirmada). Se voltar, a mensagem de erro traz o estado do Push do simulador.
- [ ] Rodar o workflow `mutation.yml` uma vez na CI (só rodou localmente: 68,08%).
- [ ] Lançar a 0.7.0-alpha.1 (versão, CHANGELOG, tag, depois base da API), como na 0.6.
