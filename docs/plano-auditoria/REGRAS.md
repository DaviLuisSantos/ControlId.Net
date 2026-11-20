# Regras para os executores do plano da auditoria (apagar na T10)

- Plano: `.claude/plans/2026-11-19-auditoria.md`. Leia a seção "Contratos" e a sua tarefa inteira antes de começar.
  Contexto dos achados: `docs/auditoria-2026-11-19.md`.
- Repositório: `D:\Projetos\ControlId.Net`, branch `auditoria-2026-11`. Outros executores trabalham **ao mesmo tempo na mesma
  árvore**. Só edite os arquivos listados na sua tarefa. Nunca reverta, formate ou "arrume" arquivos de outra tarefa.
  Nada de `git checkout/stash/reset/commit/clean` — o líder faz um commit por onda.
- API pública nova: **não** edite `PublicAPI.*.txt` (exceto a T0). Grave as linhas no formato do PublicApiAnalyzers em
  `docs/plano-auditoria/api-T<n>.txt`, uma por linha, prefixadas pelo projeto: `ControlId.Net: <linha>`. Avisos RS0016/RS0017 são
  esperados até a T10. Não edite `CHANGELOG.md`, `docs/guia.md`, READMEs nem ADRs (é da T10); se algo precisar ir para lá, liste no
  relatório final.
- Estilo: C# do repo (sem `ImplicitUsings` em `src/`, `ConfigureAwait(false)`, XML docs em **português** em todo membro público,
  mensagens de exceção em português). O núcleo `ControlId.Net` compila para `net8.0` e `netstandard2.0`: nada de API que não exista no
  netstandard2.0 sem `#if`. Logs só com `[LoggerMessage]`, nunca com corpo, token, sessão ou senha.
- Testes: xUnit em `tests/ControlId.Net.Tests` (alvos net8.0, net10.0, net48; arquivos `AspNetCore*.cs` ficam fora do net48). Use relógio
  falso/gancho interno em vez de `Task.Delay` para tempo. Cada comportamento novo do contrato precisa de teste.
- Build/teste: compile só o necessário e rode testes com filtro, por exemplo
  `dotnet build src/ControlId.Net -c Release` e `dotnet test tests/ControlId.Net.Tests -c Release -f net10.0 --filter "FullyQualifiedName~Push"`.
  Antes de terminar, rode a classe de testes completa que você tocou em net8.0, net10.0 e (se aplicável) net48. Erro de arquivo travado
  (`being used by another process`, `MSB3026/MSB3027`) é outro executor compilando: espere alguns segundos e repita.
- Relatório final (sua última mensagem): arquivos alterados/criados, o que do contrato ficou de fora ou mudou (e por quê), comandos de teste
  rodados com o resultado, e itens para a T10 (CHANGELOG/guia/ADR).
