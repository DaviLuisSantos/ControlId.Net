# 0026. Exceções por desfecho

Status: Aceita (2026-11-20)

## Contexto

Toda falha virava `ControlIdException`, e quem chamava só distinguia uma da outra lendo mensagem. A decisão que importa na
chamada é outra: dá para repetir? Uma conexão recusada não enviou nada e é segura de repetir. Um timeout numa leitura
também. Um timeout numa escrita não diz se o equipamento executou o comando (ADR 0013). E um comando que o firmware não
conhece não melhora com nova tentativa.

## Decisão

- Hierarquia sob `ControlIdException`, em `ControlIdOutcomeExceptions.cs`:
  `ControlIdConnectionException` (não conectou, o comando não saiu), `ControlIdTimeoutException` (leitura sem resposta no
  prazo), `ControlIdOutcomeUnknownException` (escrita enviada sem resposta, com `Command` e `TimedOut`),
  `ControlIdDeviceErrorException` (o equipamento respondeu com erro, com `DeviceMessage`),
  `ControlIdNotSupportedException` (subtipo do anterior: "Invalid command", "Invalid table" ou 404 no `.fcgi`) e
  `ControlIdImageRejectedException` (a imagem foi recusada antes do envio, com `ImageRejectionReason`).
- O transporte classifica no ponto da falha. Leitura (`ReadRetry.IsSafeToRepeat`) vira conexão ou timeout. Escrita sem
  resposta vira desfecho desconhecido, exceto quando a falha ocorreu antes de o comando sair (recusa, DNS, TLS), que é
  conexão.
- `ControlIdDeviceOptions.ExceptionResponseBody` (`ErrorOnly`, `Full`, `None`) decide o que `ResponseBody` guarda. O
  padrão `ErrorOnly` só guarda o JSON de erro do equipamento, para o corpo de uma resposta que não é erro não vazar em logs.
- A base `ControlIdException` não mudou: quem captura o tipo base segue funcionando. Quem comparava o tipo exato precisa
  trocar por `is`.
- **Variante `TryXxx` avaliada e adiada.** Métodos `TryCreateAsync` e similares, que devolvem `false` em vez de lançar,
  dobrariam a superfície de `IAccessClient`, que já é grande, e esconderiam justamente o desfecho desconhecido,
  que não cabe num booleano. Fica para depois, se houver demanda, e só para os casos sem ambiguidade (por exemplo, "não
  suportado").

## Consequências

- Quem chamava pode repetir só o que é seguro: `ControlIdConnectionException` e `ControlIdTimeoutException` sim,
  `ControlIdOutcomeUnknownException` só depois de conferir o estado.
- Mudar o tipo lançado é mudança observável. A política está no ADR 0029.
- A lista de comandos de leitura (ADR 0013) passa a decidir também o tipo da exceção, e precisa acompanhar os comandos
  novos.
