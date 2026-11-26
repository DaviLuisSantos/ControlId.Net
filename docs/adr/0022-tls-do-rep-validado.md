# 0022. TLS do REP validado por padrão

Status: Aceita (2026-11-19). Cumpre o que a [0005](0005-certificado-fixado-opcional.md) deixou para uma versão futura.

## Contexto

O REP iDClass sai de fábrica com certificado autoassinado. Por isso o atalho `new RepClient(url, ...)` e o
`AddControlIdRep` do pacote de DI aceitavam qualquer certificado (`AcceptAnyServerCertificate = true`): qualquer um na rede
podia se passar pelo relógio e receber login e senha. A [ADR 0005](0005-certificado-fixado-opcional.md) criou a impressão
digital fixada (`ServerCertificateSha256`) sem mudar o padrão, e registrou que a validação por padrão viria depois, com
aviso no CHANGELOG. A auditoria de 2026-11-19 classificou o ponto como P1.

## Decisão

- O atalho passa a ser `RepClient(string baseAddress, string login = "admin", string password = "admin",
  RepMode mode = RepMode.Portaria671, string? serverCertificateSha256 = null)` e não liga mais
  `AcceptAnyServerCertificate`. Com a impressão digital, só aquele certificado passa; sem ela, só um certificado válido
  para o .NET.
- `ControlIdRepOptions.AcceptAnyServerCertificate` passa a ter padrão `false`.
- Quem precisar aceitar qualquer certificado liga `AcceptAnyServerCertificate = true` de forma explícita (no construtor
  com `ControlIdDeviceOptions` ou no `appsettings.json`). O cliente registra o log 1013 (Warning) ao ser criado assim.
- Com o certificado recusado, a mensagem da `ControlIdException` diz como resolver: fixar a impressão digital em
  `ServerCertificateSha256`, obtida com `ControlIdCertificates.GetServerCertificateSha256Async`.
- O construtor antigo sai em vez de ficar obsoleto: manter a assinatura deixaria um caminho que aceita qualquer
  certificado, ou mudaria o comportamento dele em silêncio. A quebra binária fica registrada em
  `src/ControlId.Net/CompatibilitySuppressions.xml`.

## Consequências

- **Quebra:** quem usava o atalho ou o DI sem impressão digital passa a receber erro de TLS ao atualizar. O código-fonte
  continua compilando (o parâmetro novo é opcional), mas binários compilados contra a 0.8.0-alpha.1 precisam ser
  recompilados.
- Migração: ler a impressão digital uma vez com `ControlIdCertificates.GetServerCertificateSha256Async`, conferir no
  equipamento e passar em `serverCertificateSha256` (ou `ServerCertificateSha256` na configuração).
- Se o REP trocar de certificado, as chamadas falham até a configuração ser atualizada, como já acontecia com a impressão
  digital fixada.
