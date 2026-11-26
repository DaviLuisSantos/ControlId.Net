# 0019. Token Bearer nas rotas de callback

Status: Aceita (2026-11-14). Complementada pela [0021](0021-callbacks-fechados-por-padrao.md) (2026-11-19): as rotas passam a ser fechadas por padrão.

## Contexto

As rotas de Push, Monitor e modo online recebem chamadas do equipamento sem autenticação: qualquer um na rede podia
postar eventos falsos ([SECURITY.md](../../SECURITY.md)). O firmware V6.22.3 passou a mandar um cabeçalho
`Authorization: Bearer`.

## Decisão

- `RequireControlIdToken(token)` ou `RequireControlIdToken(o => ...)` protege o grupo devolvido pelos `Map...`.
  `ControlIdTokenOptions` tem `Token` (único) e `DeviceToken` (função por equipamento).
- A comparação é em tempo constante; sem token ou com token errado, a resposta é 401.
- `ConfigureCallbackSecurityAsync` (extensão de `IAccessClient`) grava no equipamento o token e, opcionalmente, o
  certificado fixado.
- Continua opcional: nada muda para quem não chama o método.
- O nome da seção e o formato do token e do certificado no equipamento ainda **estão a validar** em hardware; a seção
  padrão é `general`, e o parâmetro `section` existe para ajustar sem nova versão.

## Consequências

- Rotas protegidas por uma linha, sem autenticação própria do ASP.NET Core.
- Os valores gravados no equipamento precisam de validação num equipamento real antes de marcar a cobertura.
