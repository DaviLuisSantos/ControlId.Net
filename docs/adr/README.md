# Decisões de arquitetura

Cada arquivo registra uma decisão: o contexto, o que foi decidido e as consequências. Uma decisão não é apagada quando
muda; ela ganha o status "Substituída" e aponta para a nova.

| # | Decisão | Status |
| --- | --- | --- |
| [0001](0001-escopo-equipamentos-control-id.md) | O pacote cobre o que o equipamento Control iD suporta | Aceita |
| [0002](0002-actions-so-na-publicacao.md) | GitHub Actions só ao lançar versão | Substituída pela 0009 |
| [0003](0003-sem-strong-naming.md) | Sem strong naming | Aceita |
| [0004](0004-confirmacao-para-operacoes-destrutivas.md) | Confirmação explícita para apagar dados sem volta | Aceita |
| [0005](0005-certificado-fixado-opcional.md) | Certificado fixado como opção, sem mudar o padrão | Aceita |
| [0006](0006-governanca-da-api-publica.md) | Validação de pacote e registro da API pública | Aceita |
| [0007](0007-configuracao-tolerante-a-parametros-ausentes.md) | Leitura de configuração tolerante a parâmetros que o modelo não tem | Aceita |
| [0008](0008-exclusao-em-lotes.md) | Exclusão em lotes, encerrando no lote incompleto | Aceita |
| [0009](0009-ci-em-push-e-pull-request.md) | CI a cada push e pull request | Aceita |
| [0010](0010-dependencias-no-lts-suportado.md) | Dependências de runtime no LTS mais antigo ainda suportado | Aceita |
| [0011](0011-simulador-de-equipamento.md) | Simulador de equipamento e testes de integração | Aceita |
| [0012](0012-telemetria-sem-dependencia-do-opentelemetry.md) | Telemetria pelas APIs do .NET, sem depender do OpenTelemetry | Aceita |
| [0013](0013-novas-tentativas-so-em-leituras.md) | Novas tentativas só em comandos de leitura, desligadas por padrão | Aceita |
| [0014](0014-manter-alvo-net8.md) | Manter o alvo net8.0 depois do fim do suporte ao .NET 8 | Aceita |
| [0015](0015-native-aot.md) | Avaliar Native AOT sem declarar compatibilidade | Aceita |
| [0016](0016-logs-por-options.md) | Logs pelo ILoggerFactory, escolhido nas options ou no container | Aceita |
