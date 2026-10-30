# 0005. Certificado fixado como opção, sem mudar o padrão

Status: Aceita (2026-10-30)

## Contexto

O REP iDClass usa HTTPS com certificado autoassinado, então `new RepClient(url, ...)` aceita qualquer certificado. Isso
permite que alguém na rede se passe pelo equipamento. Validar por padrão quebraria todos os usuários de REP ao atualizar.

## Decisão

- `ServerCertificateSha256` (em `ControlIdDeviceOptions` e `ControlIdRepOptions`) fixa a impressão digital SHA-256 do
  certificado. Com ela, só aquele certificado é aceito, mesmo autoassinado, e `AcceptAnyServerCertificate` é ignorado.
- `ControlIdCertificates.GetServerCertificateSha256Async` lê a impressão digital do equipamento uma vez, para gravar na
  configuração ("confiar no primeiro uso", conferindo o valor no navegador).
- O padrão continua o mesmo; a documentação recomenda fixar.

## Consequências

- Quem fixa o certificado fica protegido; quem não fixa continua como antes.
- Se o equipamento trocar de certificado, as chamadas falham até a configuração ser atualizada (é o comportamento desejado).
- Mudar o padrão para "validar" fica para uma versão futura, com aviso antecipado no CHANGELOG.
