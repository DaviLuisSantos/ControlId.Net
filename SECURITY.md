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
- **Prefira HTTPS** quando o equipamento oferecer. No REP iDClass, que usa certificado autoassinado, fixe a impressão
  digital com `ServerCertificateSha256` em vez de aceitar qualquer certificado (veja o README).
- **A sessão vai na URL** (`?session=...`), porque é assim que a API do equipamento funciona. Não registre URLs de
  requisição em logs que outras pessoas leiam.
- **Proteja as rotas do servidor.** `MapControlIdPush`, `MapControlIdMonitor` e `MapControlIdOnline` não exigem
  autenticação por padrão; restrinja por rede ou encadeie `RequireAuthorization`/`RequireHost` no grupo devolvido.
- **Operações destrutivas** (reset de fábrica, apagar administradores, apagar todas as fotos ou templates) exigem
  `DataLossConfirmation.Confirmed`.
- **Biometria e fotos são dados pessoais sensíveis** (LGPD, art. 11). Evite guardar templates e fotos fora do
  equipamento sem necessidade, e apague-os de todos os equipamentos quando a pessoa pedir.
