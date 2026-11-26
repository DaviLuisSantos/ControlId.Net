# 0021. Callbacks fechados por padrão

Status: Aceita (2026-11-19). Complementa a [0019](0019-token-bearer-nos-callbacks.md).

## Contexto

A [ADR 0019](0019-token-bearer-nos-callbacks.md) criou o token Bearer nas rotas de Push, Monitor e modo online, mas
deixou a proteção opcional: quem esquecia o `RequireControlIdToken` publicava rotas que aceitavam eventos falsos, comandos
retirados da fila e decisões de acesso de qualquer um na rede. A auditoria de 2026-11-19 achou ainda outros pontos abertos
nas mesmas rotas: o corpo era lido inteiro, sem limite, antes de conferir o token; o modo online autorizava pelo
`device_id` da query e entregava o do corpo; um token por equipamento devolvendo `null` caía no token global; e um handler
do Monitor que lançava impedia os seguintes.

## Decisão

- **Fechado por padrão.** `MapControlIdPush`, `MapControlIdMonitor` e `MapControlIdOnline` sem `RequireControlIdToken`
  respondem 401 com `WWW-Authenticate: Bearer` e registram o log 2006 uma vez por rota, com a instrução para resolver.
  Liberar sem token exige dizer isso no código: `.AllowAnonymousControlIdCallbacks()`. Com os dois, vale o token.
- **Token.** `ControlIdTokenOptions.PreviousToken` é aceito junto com `Token`, para rotação sem parar os equipamentos.
  Com `DeviceToken` definido, devolver `null` (ou um callback sem `deviceId`) recusa a requisição, a não ser com
  `AllowTokenFallback = true`: um token compartilhado vazado não autentica o parque inteiro. O cabeçalho `Bearer` é
  conferido antes de chamar `DeviceToken`.
- **Ordem e limites.** Com token único (sem `DeviceToken`) ou rota anônima, a autorização vem antes de ler o corpo. O corpo
  tem limite (`ControlIdCallbackOptions.MaxRequestBodySize`, padrão 4 MiB; acima, 413). `IsKnownDevice` opcional recusa com
  403 um `deviceId` fora da lista, sem tocar no `PushServer`, no `DeviceContactTracker` nem nos handlers.
- **Modo online.** Em `*_create.fcgi` e `new_rex_log.fcgi`, `device_id` da query diferente do corpo responde 403.
- **Isolamento.** Cada `IMonitorNotificationHandler` roda mesmo que outro lance; a resposta é 500 no fim se algum falhou.
- **Falha cedo.** `MapControlIdPush` sem `AddControlIdPush()` e `MapControlIdOnline` sem `IOnlineAccessHandler` lançam
  `InvalidOperationException` no mapeamento.
- `/push` e `/result` respondem com `Cache-Control: no-store`. As rotas ganham as métricas
  `controlid.server.callback.requests` e `controlid.server.callback.handler.duration`.
- Rate limiting fica com o ASP.NET Core (`RequireRateLimiting` no grupo devolvido): a biblioteca não escolhe a política,
  mas o guia e o sample mostram uma.

## Consequências

- **Quebra de comportamento:** toda instalação sem token passa a receber 401 ao atualizar. O log 2006 e o CHANGELOG dizem
  o que fazer.
- Firmware anterior à 6.22 não envia o cabeçalho `Authorization`. A saída é `AllowAnonymousControlIdCallbacks()` com
  restrição de rede (`RequireHost`, firewall, rede isolada), escolhida de forma explícita.
- A leitura de volta do token gravado no equipamento (`curl_bearer_token`) continua a validar em hardware, como na 0019.
