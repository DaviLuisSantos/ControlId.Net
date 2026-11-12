# 0009. CI a cada push e pull request

Status: Aceita (2026-11-12). Substitui a [0002](0002-actions-so-na-publicacao.md).

## Contexto

A [0002](0002-actions-so-na-publicacao.md) deixou o `ci.yml` só manual para poupar a cota de minutos do GitHub Actions,
que é da conta inteira. Fato novo: em repositório público, os runners padrão (Linux e Windows) são gratuitos e não
consomem essa cota. O custo que motivou a 0002 não existe para este repositório.

Sem CI automática, um PR externo não é testado, os PRs do Dependabot chegam sem resultado de build, e não há um badge que
mostre o estado atual da `main`.

## Decisão

- O `ci.yml` roda em push na `main`, em todo pull request e à mão. Mudanças só em `*.md` e `docs/` não disparam.
- Mesmo conteúdo do `publish.yml`, sem publicar: build sem avisos (`-warnaserror`), testes em `net8.0`, `net10.0` e
  `net48`, validação de pacote no `dotnet pack` e resumo de cobertura na execução.
- Uma execução nova no mesmo PR ou branch cancela a anterior.
- O `publish.yml` não muda.

## Consequências

- PRs, inclusive os do Dependabot, chegam com o resultado dos três alvos.
- O badge do README passa a ser o do `ci.yml`.
- Se o repositório deixar de ser público, a cota volta a valer e esta decisão deve ser revista.
- O release-please e o OpenSSF Scorecard, deixados de fora pela 0002, podem voltar a ser avaliados.
