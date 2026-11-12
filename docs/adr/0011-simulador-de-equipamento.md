# 0011. Simulador de equipamento e testes de integração

Status: Aceita (2026-11-12)

## Contexto

Os testes unitários simulam o equipamento com o `FakeDeviceHandler`: cada teste roteiriza as respostas HTTP que espera.
Isso testa bem cada método, mas não o conjunto: sessão, paginação, exclusão em lotes e Monitor dependem de um estado que
muda entre chamadas. E quem não tem um equipamento (quem avalia o projeto, quem contribui, quem desenvolve uma aplicação)
não consegue experimentar a biblioteca.

## Decisão

- Um simulador em `tools/ControlId.Net.Simulator`: ASP.NET Core, estado em memória, a API `*.fcgi` da linha de acesso e o
  envio das notificações do Monitor. Distribuído como imagem Docker (chiseled, sem root) no GitHub Container Registry.
- O simulador **não referencia** o ControlId.Net. Ele segue o protocolo do equipamento com `System.Text.Json.Nodes`, sem
  os modelos da biblioteca; assim, um erro de serialização da biblioteca não se repete no simulador e passa despercebido.
- Ele reproduz as divergências do firmware de que a biblioteca depende (mensagem de parâmetro inexistente, valores em
  texto no Monitor, `device_id` em texto), cada uma documentada no README do simulador.
- Rotas `/simulator/*` provocam o que vem de fora da API: uma pessoa se identificando, um reinício.
- Os testes de integração sobem o simulador em processo, com Kestrel numa porta livre, um por teste. Não usam Docker
  (Testcontainers): rodam em qualquer sistema, em menos de 1 segundo, e em paralelo. A imagem é validada num job próprio
  da CI, com um teste de fumaça.

## Consequências

- Sessão, re-login, paginação, exclusão em lotes, fotos, configuração tolerante e Monitor são testados de ponta a ponta,
  com HTTP de verdade.
- O simulador é uma segunda implementação do protocolo e precisa acompanhar o que se aprende com equipamentos reais: uma
  divergência nova vai para o `docs/COBERTURA.md`, para a biblioteca e para o simulador.
- Fica de fora nesta versão: REP iDClass, Push e modo online. Os testes unitários continuam cobrindo esses fluxos.
