# 0027. Capacidades por firmware e degradação

Status: Aceita (2026-11-20)

## Contexto

Os firmwares da Control iD não têm todos os comandos. O iDFace 6.9.12 não tem `create_or_modify_objects`, os testes de
rede nem os comandos de template facial; o 6.13.1 não tem `get_catra_info`. Chamar um comando ausente custa uma ida ao
equipamento e volta como "Invalid command" ou 404, e cada chamada repete o erro.

## Decisão

- O transporte tem um cache de comandos sabidamente sem suporte (`IsKnownUnsupported`, `MarkUnsupported`). Um comando
  marcado lança `ControlIdNotSupportedException` antes de ir ao equipamento. Só "Invalid command" e 404 marcam o comando;
  "Invalid table" lança sem marcar, porque a falha é da tabela, e o comando serve para as outras. O `login` nunca é
  marcado, o que travaria o cliente.
- `FirmwareCapabilityMatrix` lista, por versão de firmware (`6.9.12`, `6.13.1`), os comandos observados em equipamento
  real. Ela espelha as seções "Observado num iDFace real" de `docs/COBERTURA.md`, e um teste garante que as duas
  concordam. Versões como `6.9.12-rc1` casam; `6.9.123` não.
- `AccessClient.GetCapabilitiesAsync` lê `system_information`, guarda o firmware no transporte, marca os comandos da matriz
  e devolve `DeviceCapabilities` (modelo, versão e lista de não suportados). Chamar é opcional: sem isso o cache se
  preenche aos poucos, a cada "Invalid command" recebido.
- Os métodos de alto nível degradam quando há alternativa: se `create_or_modify_objects` não existe, o fluxo cai para
  `create_objects` e `modify_objects`.
- O firmware entra na mensagem e em `ControlIdNotSupportedException.Firmware`.

## Consequências

- Um comando ausente falha de imediato e com mensagem clara, sem ida à rede depois da primeira vez.
- A matriz só cresce com observação em equipamento real; firmware fora dela não marca nada e depende do cache dinâmico.
- O cache vive no cliente e some ao recriá-lo. Um firmware atualizado exige um cliente novo, ou uma nova chamada de
  `GetCapabilitiesAsync`, que não desmarca comandos antigos.
