# 0030. Versões mínimas das dependências no netstandard2.0

Status: Aceita (2026-11-20). Complementa a 0010.

## Contexto

A 0010 deixou as dependências de runtime na 10.x, a major do único LTS ainda suportado. Quem usa o `netstandard2.0` (o
.NET Framework e outras bibliotecas) recebe essas dependências do pacote, não do framework. Instalar o `ControlId.Net`
num projeto que está na 8.x ou na 9.x força a subida de `System.Text.Json`, `Microsoft.Bcl.AsyncInterfaces`,
`System.Diagnostics.DiagnosticSource` e `Microsoft.Extensions.Logging.Abstractions` para a 10.x, com risco de conflito com
outras bibliotecas e de redirecionamento de binding no .NET Framework. O pacote de DI faz o mesmo com `Microsoft.Extensions.Http`,
`Hosting.Abstractions`, `Options.ConfigurationExtensions` e `Diagnostics.HealthChecks`.

O NuGet resolve a **menor** versão que satisfaz o grafo. Uma biblioteca só deve pedir o que realmente precisa e que não
tenha vulnerabilidade conhecida; a versão mais nova é problema de quem consome.

## Decisão

- No alvo `netstandard2.0`, cada dependência declara a menor versão segura que traz as APIs usadas:
  `System.Text.Json` 8.0.5 (as anteriores têm CVE-2024-30105 e CVE-2024-43485), `Microsoft.Bcl.AsyncInterfaces` 8.0.0,
  `System.Diagnostics.DiagnosticSource` 8.0.1, `Microsoft.Extensions.Logging.Abstractions` 8.0.2,
  `Microsoft.Extensions.Hosting.Abstractions` 8.0.1, `Microsoft.Extensions.Http` 8.0.1, `Microsoft.Extensions.Options.ConfigurationExtensions` 8.0.0 e
  `Microsoft.Extensions.Diagnostics.HealthChecks` 8.0.0.
- Os valores ficam em propriedades `ControlIdMin*` no `Directory.Packages.props`, e os projetos de `src/` as aplicam com
  `VersionOverride` só dentro de um `ItemGroup` condicionado ao `netstandard2.0`. O alvo `net8.0` segue na 10.x, como na 0010.
- A política é "a mais baixa segura, não a mais nova". Subir uma mínima exige um motivo: uma API que passou a ser usada ou
  uma vulnerabilidade. O Dependabot não enxerga essas propriedades, então elas não sobem sozinhas; quem revisa a
  segurança das dependências revisa também esta lista.
- O projeto `tests/ControlId.Net.MinDeps.Tests` (net48) consome o build `netstandard2.0` das bibliotecas sem nenhum
  pacote que suba as versões, roda um smoke do cliente e da DI com um equipamento falso e confere que as dependências
  carregadas estão na major 8. Ele roda no job `min-deps` da CI (Windows).

## Consequências

- Quem está na 8.x ou na 9.x instala o pacote sem que as dependências subam para a 10.x.
- O `ControlId.Net.Tests` no net48 também resolve as mínimas, porque consome o mesmo build `netstandard2.0`.
- Usar uma API que exista só numa versão mais nova das dependências quebra o build do `netstandard2.0` na hora; a correção
  é subir a mínima (e registrar o motivo aqui) ou evitar a API.
- A mínima não é testada em cada versão intermediária, só na versão fixada. Quem usar uma versão entre a mínima e a 10.x
  depende da compatibilidade de binário que a Microsoft mantém dentro da major.
