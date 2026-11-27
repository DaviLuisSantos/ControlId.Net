# Matriz de validação em equipamento real

Esta é a matriz **modelo × firmware × operação** do que já foi observado num equipamento Control iD real e do que ainda
não foi. A única fonte de verdade é o [docs/COBERTURA.md](COBERTURA.md), nas seções "Observado num iDFace real". Este
documento não acrescenta observações: onde o COBERTURA.md não registra o teste, a operação fica como **não validado**.

## Como ler a matriz

- **validado**: há registro explícito da operação na seção "Observado num iDFace real" do COBERTURA.md, para aquele
  modelo e firmware. A data e o vínculo do registro estão na linha.
- **não validado**: não há registro de teste em equipamento real. Isso não quer dizer que a operação falhou; quer dizer
  que ninguém registrou a observação.
- A coluna **Validador / registro** aponta para a seção do COBERTURA.md. O COBERTURA.md não registra o nome de quem
  executou os testes, então nenhum nome de pessoa é inventado aqui. Se um nome passar a constar no COBERTURA.md, ele
  pode ser transcrito para esta coluna.
- As datas são somente as dos registros do COBERTURA.md: **2026-10-13** (firmware 6.9.12) e **2026-10-30** (firmware
  6.13.1). Um traço (`—`) significa "sem registro", não uma data desconhecida a preencher.
- O cassete sintético de `tests/ControlId.Net.Tests/Cassettes/` **não** entra nesta matriz como validação: ele é escrito
  à mão, sem equipamento, e só prova o formato e o fluxo login + `system_information` do cliente.

## Resumo

| Modelo | Firmware | Operações com registro em iDFace real | Data |
| --- | --- | --- | --- |
| iDFace | 6.9.12 | login/re-login, cartão no `remote_enroll` síncrono e `set_configuration` (gravação genérica) | 2026-10-13 |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `system_information` (somente leitura) | 2026-10-30 |

Nenhuma outra combinação de modelo, firmware e operação tem registro de teste em equipamento real. iDAccess, iDFlex,
iDBox, iDUHF, leitor HID/OEM e REP iDClass aparecem como **não validado** na tabela abaixo.

## Matriz

| Modelo | Firmware | Operação | Status | Data | Validador / registro |
| --- | --- | --- | --- | --- | --- |
| iDFace | 6.9.12 | login | validado | 2026-10-13 | COBERTURA.md, seção "Observado num iDFace real (firmware 6.9.12)": chamadas simultâneas num cliente novo fazem um único login |
| iDFace | 6.9.12 | re-login (sessão expirada) | validado | 2026-10-13 | COBERTURA.md, seção "Observado num iDFace real (firmware 6.9.12)": a sessão expirada é refeita uma vez só |
| iDFace | 6.9.12 | upsert (`create_or_modify_objects` e fallback) | não validado | — | — |
| iDFace | 6.9.12 | lote de fotos (`user_set_image_list`) | não validado | — | — |
| iDFace | 6.9.12 | cartões (CRUD de `cards`) | não validado | — | — |
| iDFace | 6.9.12 | cartões — cadastro remoto (`remote_enroll` síncrono) | validado | 2026-10-13 | COBERTURA.md, seção "Observado num iDFace real (firmware 6.9.12)": "Face e cartão síncronos funcionam" |
| iDFace | 6.9.12 | grupos (`groups` e `user_groups`) | não validado | — | — |
| iDFace | 6.9.12 | `execute_actions` — porta | não validado | — | — |
| iDFace | 6.9.12 | `execute_actions` — catraca | não validado | — | — |
| iDFace | 6.9.12 | `set_configuration` (gravação) | validado | 2026-10-13 | COBERTURA.md, seção "Observado num iDFace real (firmware 6.9.12)": a escrita não é atômica |
| iDFace | 6.9.12 | `set_configuration` — modo online (`online_client`) | não validado | — | — |
| iDFace | 6.9.12 | `set_configuration` — Monitor (`monitor`) | não validado | — | — |
| iDFace | 6.9.12 | `user_hash_password` | não validado | — | — |
| iDFace | 6.9.12 | `system_information` | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | login | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | re-login (sessão expirada) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | upsert (`create_or_modify_objects` e fallback) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | lote de fotos (`user_set_image_list`) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | cartões (CRUD de `cards`) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | cartões — cadastro remoto (`remote_enroll` síncrono) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | grupos (`groups` e `user_groups`) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `execute_actions` — porta | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `execute_actions` — catraca | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `set_configuration` (gravação) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `set_configuration` — modo online (`online_client`) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `set_configuration` — Monitor (`monitor`) | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `user_hash_password` | não validado | — | — |
| iDFace (serial 0M0200/00FCE4) | 6.13.1 | `system_information` | validado | 2026-10-30 | COBERTURA.md, seção "Observado num iDFace real (firmware 6.13.1)": a resposta traz `auth802_1x`, `secbox_version`, `iDCloud_code` e `online_available` |
| iDAccess | não registrado | todas as operações desta matriz | não validado | — | — |
| iDFlex | não registrado | todas as operações desta matriz | não validado | — | — |
| iDBox | não registrado | todas as operações desta matriz | não validado | — | — |
| iDUHF | não registrado | todas as operações desta matriz | não validado | — | — |
| Leitor HID / OEM (`IAccessProvisioningClient`) | não registrado | chaves HID e código OEM | não validado | — | — |
| REP iDClass (`RepClient`) | não registrado | todas as operações REP | não validado | — | — |

Nas linhas de iDAccess, iDFlex, iDBox, iDUHF e REP, "todas as operações desta matriz" significa que cada operação
listada nas linhas do iDFace está **não validada** para aquele modelo; não há nenhuma exceção validada. No leitor
HID/OEM, o mesmo vale para as chaves HID e o código OEM, que ficam fora do `IAccessClient`.

## Notas por operação

- **login e re-login:** a seção do firmware 6.9.12 registra um único login para chamadas simultâneas e o relogin uma
  vez só depois de a sessão expirar. A seção do 6.13.1 diz que o aparelho foi testado "só com leituras", mas não
  registra login ou re-login como observação separada; por isso essas linhas ficam como não validadas.
- **upsert:** o firmware 6.9.12 lista `create_or_modify_objects` entre os comandos inexistentes (o conjunto responde
  "Invalid command" ou "Invalid table"). O fallback com `modify_objects` e `create_objects` é comportamento do pacote
  descrito no COBERTURA.md, mas não está na lista de observações de campo; nada foi validado no equipamento.
- **lote de fotos:** o COBERTURA.md documenta os limites de foto e de lote como opções do pacote, sem registro de um
  envio real de `user_set_image_list`.
- **cartões:** o cadastro remoto síncrono de cartão foi observado no 6.9.12. O CRUD da tabela `cards` não foi.
- **grupos:** o COBERTURA.md lista `groups` e `user_groups` como objetos suportados, mas não registra teste em
  equipamento real.
- **`execute_actions` (porta e catraca):** não há registro de acionamento de porta ou catraca por `execute_actions`. O
  modo online liberando acesso e o `remote_user_authorization` observados no 6.9.12 são operações diferentes. No
  6.13.1, `get_catra_info` não existe, mas isso é uma leitura, não o acionamento da catraca.
- **`set_configuration` (online e Monitor):** o 6.9.12 registra que a escrita não é atômica, sem dizer quais seções
  foram gravadas. Por isso só a linha de gravação genérica é validada; `online_client` e `monitor` ficam como não
  validados.
- **`user_hash_password`:** o endpoint está implementado, mas não há registro de uso em equipamento real.
- **`system_information`:** o 6.13.1 registra os campos extras que a resposta traz. O 6.9.12 é identificado no
  COBERTURA.md, mas a chamada de `system_information` não aparece na lista de observações daquela seção.
- **HID/OEM:** o COBERTURA.md diz que o `IAccessProvisioningClient` foi implementado conforme a documentação, "sem
  teste em leitor HID real". Nenhuma operação HID ou OEM é validada.
- **REP iDClass:** o COBERTURA.md registra que nenhum REP foi testado; tudo segue a documentação. Nenhuma operação REP
  é validada.
- **iDAccess, iDFlex, iDBox e iDUHF:** não há registro de teste de campo neste documento; todas as operações ficam como
  não validadas.

## Como gravar um cassete com `RecordingHandler`

O [`RecordingHandler`](../src/ControlId.Net.Testing/RecordingHandler.cs) fica entre o `HttpClient` e o equipamento real:
repassa cada requisição e grava a resposta no [`Cassette`](../src/ControlId.Net.Testing/Cassette.cs).

```csharp
using System;
using System.Net.Http;
using ControlId.Access;
using ControlId.Testing;

var opcoes = new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.0.2.10"),
    Login = "admin",
    Password = Environment.GetEnvironmentVariable("CONTROLID_PASSWORD")
        ?? throw new InvalidOperationException("Defina CONTROLID_PASSWORD."),
};

var cassette = new Cassette();
using var http = new HttpClient(new RecordingHandler(new HttpClientHandler(), cassette));
using var client = new AccessClient(opcoes, http);

await client.LoginAsync();
_ = await client.GetSystemInformationAsync();

cassette.Save("tests/ControlId.Net.Tests/Cassettes/nome-do-cenario.json");
```

Passo a passo:

1. Use um equipamento real e credenciais de teste; nunca grave a senha no arquivo. O `RecordingHandler` mascara
   `session`, `login`, `password`, `salt`, `image` e `template` como `***`, no corpo JSON e na consulta da URL.
2. Para mascarar outros campos, passe-os no terceiro argumento, por exemplo
   `new RecordingHandler(new HttpClientHandler(), cassette, new[] { "cpf" })`. Use a **mesma** lista no
   `ReplayHandler` ao reproduzir.
3. Execute somente as chamadas do cenário que o cassete deve cobrir. O login sob demanda entra como primeira troca
   quando o cenário não chama `LoginAsync()` explicitamente.
4. Revise o JSON antes de versionar: confira se não sobrou dado pessoal, serial, nome de usuário ou credencial. O
   mascaramento cobre os campos conhecidos, não todo e qualquer dado do equipamento.
5. Salve o arquivo em `tests/ControlId.Net.Tests/Cassettes/`. O csproj do projeto de testes copia
   `Cassettes/**/*.json` para a pasta de saída em todos os alvos.
6. Registre o arquivo em `Scenarios`, dentro de `CassetteRegressionTests.cs`, com as chamadas que consomem as trocas
   na ordem gravada. Sem esse registro, o teste `Every_cassette_has_a_scenario` falha.
7. Rode `dotnet test`. O teste de regressão carrega o cassete com `Cassette.Load`, responde por `ReplayHandler` e
   confere que todas as trocas foram consumidas (`Remaining == 0`).

Um arquivo de cassete é a lista ordenada de trocas. Cada troca tem método, caminho (com `session` mascarado), corpo da
requisição (mascarado), código HTTP, tipo de conteúdo e corpo da resposta; respostas binárias usam as propriedades
`*IsBase64`. O caminho inclui a barra inicial, como em `/login.fcgi` ou `/system_information.fcgi?session=***`.

O cassete `sintetico-login-system-information.json` foi escrito à mão nesse formato, sem equipamento, e cobre login +
`system_information`. Ele demonstra o formato e protege o fluxo inicial do cliente, mas não é uma captura de campo e
não deve ser citado como validação na matriz.

## Como o teste de regressão funciona

O [`CassetteRegressionTests`](../tests/ControlId.Net.Tests/CassetteRegressionTests.cs) descobre todos os `*.json` da
pasta `Cassettes/` copiada para a saída, procura o cenário correspondente em `Scenarios`, carrega o cassete e reproduz
as chamadas com `ReplayHandler` e um `AccessClient` sem equipamento. Ao final, exige que não tenha sobrado nenhuma
troca (`Remaining == 0`).

O teste `Every_cassette_has_a_scenario` compara a lista de arquivos com a lista de cenários. Ele falha tanto para um
arquivo novo sem cenário quanto para um cenário que perdeu o arquivo. Para acrescentar um cassete, os dois lados
precisam entrar juntos no mesmo commit.

Os mesmos testes rodam nos alvos `net8.0`, `net10.0` e `net48`; a cópia dos cassetes no csproj garante que a pasta
exista na saída também na CI. O teste não fala com a rede: qualquer divergência de método, endpoint, ordem ou corpo é
reportada pelo `ReplayHandler` como `InvalidOperationException`, dizendo o que era esperado e o que chegou.

## Manutenção

- Só marque **validado** depois de a observação constar do COBERTURA.md, com modelo, firmware e data. Atualize os dois
  documentos juntos.
- Não use o cassete sintético nem a reprodução sem equipamento como prova de validação de campo: o replay prova apenas
  que o cliente continua enviando a mesma conversa.
- Se o cliente mudar o corpo, o endpoint ou a ordem das chamadas, o replay falha. Regrave o cassete com o
  `RecordingHandler` em vez de editar a conversa à mão, exceto para o cassete sintético.
- Ao observar um modelo, firmware ou operação nova, acrescente a linha na matriz com a data e o vínculo do registro;
  não preencha data ou validador por inferência.
