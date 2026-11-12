# Contribuindo

Obrigado pelo interesse. O escopo do pacote é: **o que o equipamento Control iD suporta, o pacote suporta**, nada além
(outras marcas terão pacotes próprios). Antes de algo grande, abra uma issue para conversar.

## Ambiente

- SDK do .NET 10 (compila todos os alvos) e, no Windows, o .NET Framework 4.8 para os testes `net48`.
- `dotnet build` e `dotnet test` na raiz. Em Linux ou macOS: `dotnet test -f net8.0` (e `-f net10.0`).

A CI roda em todo pull request: build sem avisos, testes em `net8.0`, `net10.0` e `net48` e validação da API pública
([ADR 0009](docs/adr/0009-ci-em-push-e-pull-request.md)). Rode `dotnet test` antes de enviar para não depender dela.

## O que um PR precisa ter

- **Teste** para o comportamento novo ou corrigido. Os testes simulam o equipamento com o `FakeDeviceHandler`; quando a
  correção vier de um equipamento real, use a resposta real dele no teste e cite o modelo e o firmware.
- **Simulador**: comportamento observado num equipamento real que a biblioteca passa a tratar entra também no
  [simulador](tools/ControlId.Net.Simulator/README.md), com um teste em `tests/ControlId.Net.IntegrationTests`.
- **CHANGELOG.md**: uma linha na seção `[Unreleased]`, no formato [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).
- **API pública**: mudanças na API aparecem como aviso RS0016/RS0017 no build. Acrescente as linhas em
  `PublicAPI.Unshipped.txt` do projeto (a IDE sugere a correção). Se a mudança quebra a compatibilidade com a última
  versão publicada, o `dotnet pack` falha; quebras intencionais vão em `CompatibilitySuppressions.xml`, com o motivo.
- **Documentação**: XML em português nos membros públicos; o `docs/guia.md` e o `docs/COBERTURA.md` quando mudar o que o pacote
  cobre.
- **Nenhum aviso novo** no build (`dotnet build --no-incremental`).

## Testes em equipamento real

Diferenças entre a documentação e o firmware são comuns. Ao testar num equipamento, registre o que viu na seção
"Observado num ... real" do `docs/COBERTURA.md`: modelo, versão do firmware, o que funcionou, o que não existe nele e o que
ficou sem conclusão. Prefira leituras; operações destrutivas exigem `DataLossConfirmation.Confirmed` e um backup antes.

## Commits

Mensagens em português, com um título curto no imperativo ("Corrige ...", "Adiciona ...") e, no corpo, o porquê da
mudança. Um assunto por commit.

## Segurança

Vulnerabilidades não vão em issues públicas: veja o [SECURITY.md](SECURITY.md).
