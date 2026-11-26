# Segurança

## Versões com correção

Enquanto o pacote estiver em pré-lançamento (`0.x-alpha`), só a versão mais recente recebe correções. Atualize para ela
antes de relatar um problema.

## Como relatar uma vulnerabilidade

Não abra uma issue pública. Use o relato privado do GitHub: aba **Security** do repositório, **Report a vulnerability**.
Descreva o problema, como reproduzir e, se envolver um equipamento, o modelo e a versão do firmware.

A resposta inicial sai em até 7 dias. Se a falha for confirmada, a correção vai numa versão nova, com o crédito a quem
relatou (se quiser) nas notas da versão.

Falhas no firmware ou nos produtos da Control iD devem ser relatadas à própria Control iD; este projeto é independente.

## Uso seguro com os equipamentos

O pacote só faz o que a API do equipamento permite, então a segurança depende também de como ele é configurado:

- **Troque a senha padrão.** Os equipamentos saem de fábrica com `admin`/`admin`. Use `ChangeLoginAsync` ou a interface
  web, e guarde a nova senha num cofre de segredos, nunca no código ou no `appsettings.json` versionado.
- **Prefira HTTPS** quando o equipamento oferecer. O `RepClient` valida o certificado do REP iDClass por padrão
  ([ADR 0022](docs/adr/0022-tls-do-rep-validado.md)): como o de fábrica é autoassinado, fixe a impressão digital com
  `ServerCertificateSha256` (obtenha com `ControlIdCertificates.GetServerCertificateSha256Async`, numa rede confiável, e
  confira no equipamento). Não ligue `AcceptAnyServerCertificate`: login e senha ficariam expostos a quem se passar pelo
  equipamento, e o cliente registra o log 1013 quando a opção está ligada sem impressão digital (veja o
  [guia](docs/guia.md#certificado-do-rep)).
- **A sessão vai na URL** (`?session=...`), porque é assim que a API do equipamento funciona. Não registre URLs de
  requisição em logs que outras pessoas leiam.
- **Proteja as rotas do servidor.** `MapControlIdPush`, `MapControlIdMonitor` e `MapControlIdOnline` são fechadas por
  padrão ([ADR 0021](docs/adr/0021-callbacks-fechados-por-padrao.md)): sem `RequireControlIdToken`, respondem 401. Use um
  token longo e aleatório, guardado como segredo, e grave o mesmo no equipamento com `ConfigureCallbackSecurityAsync`
  (seção e formato no equipamento a validar). Para trocar o token sem parar os equipamentos, use `PreviousToken`. Com um
  token por equipamento (`DeviceToken`), deixe `AllowTokenFallback` desligado. `AllowAnonymousControlIdCallbacks()` só
  em rede isolada (ou para firmware anterior à 6.22, que não envia o token), sempre com restrição de rede.
- **Limite quem chama.** Encadeie `RequireRateLimiting` nas rotas de callback (um limite por IP corta quem tenta
  adivinhar o token), restrinja com `RequireHost` ou no firewall e, se souber quais equipamentos existem,
  `ControlIdCallbackOptions.IsKnownDevice`. O corpo dos callbacks já tem limite (`MaxRequestBodySize`, 4 MiB). Veja o
  [guia](docs/guia.md#callbacks-token-limites-e-rate-limiting) e o `samples/ControlId.Net.Server`.
- **HTTP trafega em claro**, inclusive o token dos callbacks: em produção, sirva as rotas por HTTPS (proxy reverso ou
  Kestrel com certificado) e proteja a sua própria API de operação com autenticação de verdade.
- **Operações destrutivas** (reset de fábrica, apagar administradores, apagar todas as fotos ou templates) exigem
  `DataLossConfirmation.Confirmed`.
- **Biometria e fotos são dados pessoais sensíveis** (LGPD, art. 11). Evite guardar templates e fotos fora do
  equipamento sem necessidade, e apague-os de todos os equipamentos quando a pessoa pedir.
