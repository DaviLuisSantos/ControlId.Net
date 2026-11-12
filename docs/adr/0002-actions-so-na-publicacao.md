# 0002. GitHub Actions só ao lançar versão

Status: Substituída pela [0009](0009-ci-em-push-e-pull-request.md) (2026-11-12). Aceita em 2026-10-14.

## Contexto

A cota de minutos do GitHub Actions é da conta inteira, compartilhada com outros projetos. A CI rodava a cada push e pull
request, com um job Linux e outro Windows (para o `net48`).

## Decisão

- O `ci.yml` só roda quando disparado à mão (`workflow_dispatch`).
- O `publish.yml`, disparado pela tag `v*.*.*`, testa os três alvos e só publica se todos passarem.
- O que precisa de Actions para governança roda dentro do `publish.yml`: validação de API (no `dotnet pack`), cobertura,
  SBOM e atestação de proveniência.
- Ferramentas que não usam minutos continuam: o Dependabot abre PRs mensais agrupados.
- Ficam de fora o release-please e o OpenSSF Scorecard, que rodariam a cada push ou por agenda.

## Consequências

- Os testes rodam localmente antes de cada push (`dotnet test`); o CONTRIBUTING e o modelo de PR pedem isso.
- Um PR externo não é testado automaticamente.
- O badge do README é o do `publish.yml`: o do `ci.yml` mostraria só a última execução manual.
- Se a cota deixar de ser problema, basta voltar `push` e `pull_request` no `ci.yml`.
