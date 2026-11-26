# Análise de inconsistências e branches pendentes

Análise feita sobre a base `0.8.0-alpha.1` (commit `30da687`), no worktree
`capataz/job-07e42f0a/relatorio` (`78416d8`), sem executar build, testes ou merge. As referências
`origin/main` e de branches vêm do estado local do Git, sem novo `git fetch`.

Arquivos comparados: `README.md`, `README.pt-BR.md`, `CHANGELOG.md`, `Directory.Build.props`,
`docs/adr/README.md`, `docs/COBERTURA.md` e `src/PACKAGE.md`, além do código citado como prova.

## 1. Inconsistências

### 1.1 Circuit breaker: inglês desatualizado em relação ao português, ao changelog e ao código

- `README.md:176` marca `Circuit breaker per device` como não concluído (`[ ]`).
- `README.pt-BR.md:177` marca o mesmo item como concluído (`[x]`).
- `CHANGELOG.md:12` registra o circuit breaker como entregue na `0.8.0-alpha.1`, com
  `CircuitBreakerFailureThreshold`/`CircuitBreakerBreakDuration`, `CircuitState` e
  `ControlIdCircuitOpenException`.
- O código confirma a entrega: `src/ControlId.Net/ControlIdDeviceOptions.cs:90`
  (`CircuitBreakerFailureThreshold`) e `src/ControlId.Net/ControlIdCircuitOpenException.cs:9`
  (exceção do circuito aberto).

Conclusão: o roadmap em inglês está atrasado; o português, o changelog e o código concordam entre si.

### 1.2 `src/PACKAGE.md` não lista o pacote de Push com EF Core

- `src/PACKAGE.md:9-15` lista só `ControlId.Net`, `ControlId.Net.DependencyInjection` e
  `ControlId.Net.AspNetCore`.
- `README.md:148` e `README.pt-BR.md:149` listam também
  `ControlId.Net.Push.EntityFrameworkCore` (fila de Push durável, `net10.0`).
- `CHANGELOG.md:15` anuncia o pacote como parte da `0.8.0-alpha.1`.
- O projeto existe em `src/ControlId.Net.Push.EntityFrameworkCore`: o `.csproj` define
  `net10.0` na linha 5 e `PackageId` `ControlId.Net.Push.EntityFrameworkCore` na linha 10.

Conclusão: a página curta do NuGet está incompleta; faltam o pacote de EF Core e a menção ao alvo
`net10.0`.

Complemento, no mesmo arquivo: `src/PACKAGE.md:4` e `:7` descrevem o parser de AFD apenas como
"Portaria 671/2021". O README (`README.md:30-31` e `README.pt-BR.md:31-32`) e o código
(`src/ControlId.Net/Rep/Afd/AfdParser.cs:11`) cobrem também a Portaria 1510/2009. É uma descrição
incompleta, não uma contradição (o texto não afirma exclusividade).

### 1.3 Nota de mutação não é reproduzível só com os números citados

- `README.md:155` e `README.pt-BR.md:156` informam **68,08%** de nota de mutação com
  **1.216 mortos, 23 timeout e 425 sobreviventes**.
- Somando as três categorias: `1.216 + 23 + 425 = 1.664`.
  - `1.216 / 1.664 = 73,08%`;
  - `(1.216 + 23) / 1.664 = 74,46%`;
  - `1.216 / (1.216 + 425) = 74,10%`.
- Para chegar a 68,08% com 1.216 mortos, o denominador teria de ser aproximadamente **1.786**, ou
  seja, cerca de **122 mutantes** não citados (provavelmente "sem cobertura").

Conclusão: a porcentagem não é reproduzível apenas com as categorias listadas. Falta o total de
mutantes ou a categoria "sem cobertura" no texto. O `CHANGELOG.md:34` cita a mesma nota, mas sem
contagens, então não permite conferir.

### 1.4 Contagens de objetos e seções não reconciliadas

Estas contagens não são contradições confirmadas, porque `origin/main` mantém os mesmos números;
ficam registradas como pontos a esclarecer.

- **Objetos:** `README.md:23`, `README.pt-BR.md:23` e `docs/COBERTURA.md:14` falam em **37 objetos**.
  A tabela de objetos de `docs/COBERTURA.md:173-186` lista **41 nomes**; no código da base há **31**
  usos de `[ControlIdObject]` (por exemplo, `src/ControlId.Net/Access/Models/AccessUser.cs:7`).
  Nenhum dos dois números é 37. O critério dos 37 não está documentado.
- **Seções:** `docs/COBERTURA.md:15` fala em **34 seções e 245 parâmetros**. A tabela
  `docs/COBERTURA.md:196-229` tem 34 linhas e a soma dos parâmetros é exatamente 245, mas `sec_box`
  aparece duas vezes (`:223` e `:224`), resultando em **33 nomes distintos**. No código há 34 classes
  de configuração, incluindo `SecBoxsSettings`, que é obsoleta
  (`src/ControlId.Net/Access/Configuration/DeviceSettings.cs:1262`). Ou seja, "34 seções" pode
  significar 34 classes (uma obsoleta) ou 34 linhas (com duplicata), mas não 34 seções distintas.

### 1.5 Desatualização em relação a `origin/main` (esperada nesta base)

Esta é a inconsistência principal do estado atual: a base está atrás de `origin/main` (`a75cd9f`).
O merge de `origin/main` deve corrigir os itens abaixo.

- **Versão:** `Directory.Build.props:17` fixa `0.8.0-alpha.1`. Existem as tags
  `v0.9.0-alpha.1` (commit `64f0163`) e `v0.10.0-alpha.1` (commit `3112cc5`), e `origin/main` tem
  `0.10.0-alpha.1` no mesmo arquivo.
- **Changelog:** `CHANGELOG.md:5-7` tem `[Unreleased]` vazio e a última versão em `0.8.0-alpha.1`;
  `origin/main` já tem seções `0.9.0-alpha.1` e `0.10.0-alpha.1`. O link
  `CHANGELOG.md:213` ainda compara `v0.8.0-alpha.1...HEAD`.
- **Contagem de testes:** `README.md:154` e `README.pt-BR.md:155` citam 494 testes unitários em
  .NET 8/.NET 10, 481 em .NET Framework 4.8 e 32 de integração; em `origin/main` os números são
  799, 832, 721 e 42, respectivamente. Não executei a suíte, então as contagens da base não foram
  verificadas por execução.
- **Cobertura:** `docs/COBERTURA.md:7` fala em 74 páginas e `:12` em 85 de 85 endpoints;
  `origin/main` documenta 96 páginas e 180 de 180 endpoints. A base não conhece os endpoints
  acrescentados nas versões 0.9/0.10.
- **ADRs:** `docs/adr/README.md:6-27` lista 20 decisões (0001 a 0020); `origin/main` tem 30
  (0001 a 0030), incluindo 0021 a 0030 em `origin/main:docs/adr/`.
- **Pacotes:** `origin/main` acrescenta `ControlId.Net.Testing` ao README e ao `src/PACKAGE.md`;
  essa pasta/pacote não existe na base.

O merge de `origin/main` deve trazer as correções dos itens 1.1, 1.2 e 1.5: a versão, o roadmap em
inglês, a tabela de pacotes e as contagens/documentação atualizadas já estão em `origin/main`. Os
itens 1.3 e 1.4 não foram alterados em `origin/main` e continuam como pontos a corrigir ou esclarecer.

### 1.6 Verificações que não acusaram inconsistência

- **Links relativos:** todos os links relativos dos sete arquivos analisados apontam para caminhos
  existentes (READMEs, `docs/guia.md`, `docs/COBERTURA.md`, ADRs, `benchmarks/README.md`,
  simulador, dashboard, `docs/images/dashboard.gif`, `SECURITY.md`, `CONTRIBUTING.md`, `LICENSE`).
  Os âncoras usados pelo `CHANGELOG.md` existem em `docs/guia.md`: `#observabilidade-opentelemetry`
  (linha 629), `#logs` (670), `#novas-tentativas-em-leituras` (703), `#health-checks` (724) e
  `#circuit-breaker` (761). Links externos não foram acessados.
- **ADRs listados:** as 20 entradas de `docs/adr/README.md` correspondem a 20 arquivos existentes;
  não há lacuna entre 0001 e 0020. O status "Substituída pela 0009" da 0002
  (`docs/adr/README.md:9`) aponta para um arquivo existente, e a 0009 também aponta de volta para a
  0002.
- **Endpoints:** a tabela de `docs/COBERTURA.md:28-153` e o teste
  `tests/ControlId.Net.Tests/DocumentationCoverageTests.cs:30-135` têm 86 entradas cada. A diferença
  aparente para as 85 da prosa é `enroller_state.fcgi` (`docs/COBERTURA.md:57`), marcado como
  "só no Postman": 85 documentados + 1 extra. Não é uma divergência real.
- **REP:** as 47 entradas da tabela de `docs/COBERTURA.md:329-366` (anunciadas em `:324-327`)
  correspondem às 47 entradas de
  `tests/ControlId.Net.Tests/RepDocumentationCoverageTests.cs:14-74`.
- **Alvos:** o README (`README.md:38`) cita `net8.0` e `netstandard2.0`, o que confere com
  `src/ControlId.Net/ControlId.Net.csproj:4`; os testes miram `net8.0;net10.0;net48`
  (`tests/ControlId.Net.Tests/ControlId.Net.Tests.csproj:8`); `ControlId.Net.AspNetCore` é `net8.0`
  e o pacote de EF é `net10.0`, como o README descreve (`README.md:147-148`).
- **Assinatura do quick start:** `src/PACKAGE.md:27` usa
  `GetAccessLogsAsync(afterId: 0, limit: 100)`, compatível com
  `src/ControlId.Net/Access/AccessClient.cs:706`
  (`GetAccessLogsAsync(long afterId = 0, int? limit = null, ...)`).

## 2. Branches pendentes

### 2.1 `main` local divergiu de `origin/main`

- `main` local: **`78416d8`** ("Merge capataz/job-932a4cfa/integration ...").
- `origin/main` (referência local, sem novo fetch): **`a75cd9f`** ("Resolve as issues abertas que
  não dependem de equipamento", 2026-11-20).
- Ancestral comum: **`30da687`**, que é a tag `v0.8.0-alpha.1`.
- `git rev-list --left-right --count main...origin/main` retorna **`3 13`**: a `main` local tem 3
  commits que `origin/main` não tem, e `origin/main` tem 13 commits que a `main` local não tem.
- Consequência: não é fast-forward. O merge de `origin/main` na `main` local cria um commit de
  merge e traz as versões 0.9/0.10, a documentação atualizada, os ADRs 0021 a 0030 e os pacotes
  novos. Os 3 commits locais (os merges `78416d8` e `e365b91`, mais `fabdd07`) são preservados.
- `origin/main` está 2 commits à frente da tag `v0.10.0-alpha.1` (`3112cc5`): os commits
  `98f18e7` e `a75cd9f`.
- O arquivo `docs/analise-inconsistencias.md` não existe em `main` nem em `origin/main`; por isso,
  este relatório não deve conflitar com o merge.

### 2.2 `capataz/job-cd633cf8/integration`

- Commit: **`ce119dd`** ("Merge capataz/job-cd633cf8/c5 into capataz/job-cd633cf8/integration").
- Parte de `a75cd9f`: `a75cd9f` é ancestral de `ce119dd`, e a branch está **49 commits à frente**
  de `a75cd9f`, sem commits de `a75cd9f` fora dela.
- Não está integrada em `origin/main`: `git branch --contains ce119dd` mostra apenas a própria
  branch, e `git branch -a --no-merged origin/main` a lista como não mergeada.
- **Conclusão não confirmada:** não verifiquei build, testes ou CI dessa branch, e não há no
  relatório evidência de que a integração foi validada. O estado dela deve ser confirmado antes de
  qualquer merge.

### 2.3 Demais branches

O comando `git branch -a --no-merged origin/main` lista outras branches locais e remotas
(`capataz/job-932a4cfa/*`, `capataz/job-be7cf70d/main`, `capataz/job-cd633cf8/a1` a `a4`,
`b1` a `b5` e `c1` a `c5`, `origin/auditoria-2026-11`, `origin/chore/*`,
`origin/cobertura-access-api`, `origin/dependabot/*`, `origin/fase-3-producao`,
`origin/feature/benchmarks`, `origin/release/0.9.0-alpha.1` e
`origin/release/0.10.0-alpha.1`).

**Estado não verificado.** Nenhuma delas foi analisada nesta tarefa; não se sabe se estão
concluídas, se podem ser mergeadas ou se já foram substituídas.

### 2.4 Comandos sugeridos

Não executei o merge nem o build (restrição da tarefa). Sequência sugerida para a atualização da
`main`:

```bash
git fetch
git branch -a --no-merged origin/main
git switch main
git merge origin/main
dotnet build
```

Depois do merge, vale conferir se os itens da seção 1.5 foram resolvidos e se a branch
`capataz/job-cd633cf8/integration` continua necessária ou já está contida no resultado.

STATUS: complete
