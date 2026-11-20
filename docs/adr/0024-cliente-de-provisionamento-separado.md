# 0024. Cliente de provisionamento separado para chaves HID e código OEM

Status: Aceita (2026-11-20).

## Contexto

A documentação da Access API tem 11 endpoints que podem deixar leitores ou equipamentos inutilizáveis em campo: a
hierarquia de chaves do leitor HID (OMNIKEY Reader Core: KEK padrão, KEK intermediária, chave de mídia, OIDs internos e
reset da configuração) e o código OEM com a versão de firmware aprovada. Uma KEK errada trava o leitor; um código OEM
errado muda a marca do equipamento. Quase nenhum integrador precisa disso, e quem precisa sabe que precisa.

Pôr esses métodos no `IAccessClient` os deixaria no autocompletar de toda chamada comum e obrigaria cada fake ou
decorador da interface a implementá-los. A documentação também cita dois endpoints que não servem a integrações:
`dual_sided_query` (rota entre dois equipamentos, assinada com o segredo compartilhado) e `tests.fcgi` (coleta de testes
de fábrica, só na coleção Postman).

## Decisão

- Os 11 endpoints ficam em `IAccessProvisioningClient`, fora do `IAccessClient`. O cliente só existe quando o integrador o
  cria com `client.CreateProvisioningClient()` (extensão sobre `IAccessClient`; só funciona com `AccessClient`). Ele usa a
  mesma conexão, sessão, novas tentativas e circuit breaker do `AccessClient` e não precisa ser descartado.
- Sem registro no contêiner de DI: quem quer o cliente o cria a partir do `IAccessClient` que já tem.
- Chaves, KEKs e licenças nunca entram em exceção nem em log; a validação (hexadecimal, OID de seis partes, versão
  `major.minor.patch`) não repete o valor recebido. O reset e a restauração da configuração do leitor exigem
  `DataLossConfirmation`.
- `hid_set_oid` infere o tipo pelo tipo JSON do valor, então há três sobrecargas (`bool`, `int`, `byte[]`), e o retorno de
  `hid_get_oid` vira `HidOidValue`, com o tipo que o equipamento inferiu.
- `dual_sided_query` e `tests.fcgi` não serão cobertos (`tests/ControlId.Net.Tests/Cobertura/nao-cobrir.txt`); continuam
  acessíveis por `SendRawAsync`.

## Consequências

- O `IAccessClient` não muda; o cliente novo é aditivo.
- Os métodos foram escritos a partir da documentação, sem leitor HID real. O summary da interface diz isso, e o primeiro
  uso em campo deve começar por um leitor de bancada.
- Um `IAccessClient` que não seja `AccessClient` (fake, decorador) recebe `NotSupportedException` ao criar o cliente;
  testes podem simular `IAccessProvisioningClient` diretamente.
