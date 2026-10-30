# 0006. Validação de pacote e registro da API pública

Status: Aceita (2026-10-30)

## Contexto

O pacote tem cerca de 2.400 símbolos públicos em três pacotes e dois alvos (`net8.0` e `netstandard2.0`). Mudanças na API
só apareciam lendo o código, e uma quebra de compatibilidade podia sair numa versão sem ninguém perceber.

## Decisão

- **Validação de pacote** (`EnablePackageValidation`): cada `dotnet pack` compara a API com a última versão publicada
  (`PackageValidationBaselineVersion`) e entre os dois alvos. Uma quebra vira erro; quebras intencionais ficam em
  `CompatibilitySuppressions.xml`, com o motivo. Ao publicar, a base passa para a nova versão e as supressões são apagadas.
- **PublicApiAnalyzers**: `PublicAPI.Shipped.txt` (o que foi publicado) e `PublicAPI.Unshipped.txt` (o que entra na
  próxima versão), por projeto. Mudar a API sem atualizar os arquivos é aviso no build, e toda mudança aparece no diff.
- **AnalysisLevel `latest-recommended`** nas bibliotecas, sem nenhum aviso; as exceções ficam no `Directory.Build.props`
  com o motivo (APIs ausentes no `netstandard2.0`, nomes que seguem a API do equipamento).
- RS0026/RS0027 (várias sobrecargas com parâmetros opcionais) desligadas: a API usa esse padrão de propósito, e a
  compatibilidade já é verificada pela validação de pacote.

## Consequências

- Quebras deixam de passar despercebidas; o publish.yml não publica uma quebra não declarada.
- Ao publicar uma versão: mover o conteúdo de `PublicAPI.Unshipped.txt` para `PublicAPI.Shipped.txt`, atualizar a base da
  validação e apagar `CompatibilitySuppressions.xml`.
