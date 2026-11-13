# 0015. Avaliar Native AOT sem declarar compatibilidade

Status: Aceita (2026-11-13)

## Contexto

O pedido (#11) é saber se a biblioteca roda em Native AOT. A sonda `tools/ControlId.Net.AotProbe` (net10.0,
`PublishAot=true`, fora da solution) usa `AccessClient`, `RepClient`, `GetConfigurationSectionAsync<T>`,
`LoadObjectsAsync<T>` e `AfdParser`. Foi publicada com `dotnet publish -c Release -r win-x64`.

Resultado da análise do ILCompiler (a etapa de ligação nativa não concluiu nesta máquina, que não tem as ferramentas C++
do Visual Studio; a análise de trim/AOT, que é o que importa aqui, roda antes e foi completa):

| Área | Aviso | Contagem | Origem |
|---|---|---|---|
| JSON / modelos | IL2026 + IL3050 | 2 + 2 | `ControlIdJson.Serialize` e `Deserialize<T>` (reflexão do `System.Text.Json`) |
| JSON / conversores | IL2026 + IL3050 | 1 + 1 | `EmptyStringNumberConverter.Converter<T>.Write` |
| `ControlIdObjects` | nenhum | 0 | o uso na sonda não gerou aviso próprio |
| `ConfigurationSection` | IL2070 | 1 | mapeamento de propriedades por reflexão (`ConfigurationSectionMapper`) |
| AFD | nenhum | 0 | `AfdParser` é texto puro, sem reflexão |

Total: 3 IL2026, 3 IL3050, 1 IL2070. O ILCompiler agrupa por causa raiz, então a contagem real cresce conforme mais
tipos de modelo passam pela serialização.

## Decisão

Não ligar `IsAotCompatible` nos pacotes por enquanto. A promessa só vale com serialização sem reflexão, e hoje o JSON
dos modelos depende dela; declarar compatibilidade sem isso enganaria quem usa. O `AfdParser` já é seguro e pode ser
usado em AOT.

## Fases para chegar lá

1. Gerar um `JsonSerializerContext` (source generation) para os modelos e passá-lo em `ControlIdJson`; zera IL2026/IL3050
   de JSON.
2. Reescrever `EmptyStringNumberConverter` com conversores concretos por tipo numérico, sem `Converter<T>` genérico
   instanciado por reflexão.
3. Trocar o mapeamento de `ConfigurationSection` por um gerador de código ou por metadados registrados, ou anotar com
   `DynamicallyAccessedMembers`.
4. Ligar `IsAotCompatible` e a sonda como verificação no CI, com zero avisos.

## Consequências

- Nada muda para quem usa o pacote hoje.
- A sonda fica no repositório para medir o avanço de cada fase.
