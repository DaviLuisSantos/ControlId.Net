# Migrando de um cliente próprio e testando sem equipamento

Este guia ajuda quem já tem um cliente HTTP escrito à mão para os equipamentos Control iD a trocá-lo pelo `ControlId.Net`, e mostra como gravar uma conversa real com o equipamento para reproduzi-la depois nos testes, sem hardware.

## 1. Trocar o cliente

1. Instale `ControlId.Net` e, para os testes, `ControlId.Net.Testing`.
2. Substitua as chamadas `login.fcgi`, `load_objects.fcgi` etc. pelos métodos do `AccessClient`. Ele faz o login sozinho, renova a sessão e repete leituras quando é seguro.
3. Se você já tem um `HttpClient` configurado (proxy, certificado, `IHttpClientFactory`), entregue-o ao construtor: `new AccessClient(opcoes, httpClient)`.
4. Troque as classes de erro próprias pelas exceções `ControlId.ControlIdException` e derivadas.

## 2. Gravar uma conversa com o equipamento

`RecordingHandler` fica entre o cliente e o equipamento e guarda cada requisição e resposta num `Cassette`.

```csharp
var cassette = new Cassette();
using var http = new HttpClient(new RecordingHandler(new HttpClientHandler(), cassette));
using var client = new AccessClient(opcoes, http);

await client.GetUsersAsync();

cassette.Save("cassetes/usuarios.json");
```

Os valores de `session`, `login`, `password`, `salt`, `image` e `template` saem mascarados como `***`, tanto no corpo JSON quanto na consulta da URL. Para mascarar outros campos, passe-os no terceiro argumento do construtor (por exemplo `new[] { "cpf" }`). Revise o arquivo antes de versioná-lo.

## 3. Reproduzir nos testes

```csharp
var cassette = Cassette.Load("cassetes/usuarios.json");
using var http = new HttpClient(new ReplayHandler(cassette));
using var client = new AccessClient(opcoes, http);

var usuarios = await client.GetUsersAsync();
```

O `ReplayHandler` responde na ordem gravada e confere método, endpoint e corpo de cada requisição. Se o código mudar e a conversa divergir, a chamada falha com `InvalidOperationException` dizendo o que era esperado e o que chegou, por exemplo `o corpo divergiu. Esperado: {"object":"users"}. Recebido: {"object":"groups"}`. Use os mesmos campos extras de máscara na gravação e na reprodução. `Remaining` informa quantas trocas do cassete ainda não foram usadas.
