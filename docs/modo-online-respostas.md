# Respostas do modo online

Este documento descreve o JSON que o servidor devolve ao equipamento nas rotas de identificação do modo online
(`new_card.fcgi`, `new_user_identified.fcgi`, `new_pin.fcgi`, `new_face_template.fcgi` e as demais listadas em
`OnlineEvents.IdentificationEndpoints`), o significado dos códigos de evento e o que acontece quando o handler não
decide. O contrato é verificado por `tests/ControlId.Net.Tests/OnlineResponseContractTests.cs`.

## Formato da resposta

Quando a requisição passa na autenticação e nos limites, a resposta de uma identificação é **HTTP 200** com
`Content-Type: application/json` e um único objeto `result`:

```json
{
  "result": {
    "event": 7,
    "user_id": 6,
    "portal_id": 1,
    "message": "Acesso liberado",
    "user_name": "Neal Caffrey",
    "user_image": true,
    "user_image_hash": "3f786850e387550fdab836ed7e6dc881de23001b",
    "actions": [
      { "action": "catra", "parameters": "allow=clockwise" },
      { "action": "sec_box", "parameters": "id=65793, reason=3" },
      { "action": "door", "parameters": "door=1" }
    ]
  }
}
```

`OnlineAuthorization.ToResponseJson()` monta exatamente esse envelope. No ASP.NET Core, `MapControlIdOnline` já escreve
a resposta pelo handler; o método serve para quem monta a resposta manualmente (por exemplo, um endpoint próprio).

| Campo | Tipo JSON | Observação |
| --- | --- | --- |
| `event` | número | Código de `AccessEventType` (veja a tabela abaixo). Obrigatório na prática: é o que o equipamento interpreta. |
| `user_id` | número | Usuário identificado, quando houver. |
| `user_name` | string | Nome exibido no display. |
| `user_image` | booleano | `true` faz o equipamento pedir a foto em `GET user_get_image.fcgi`. |
| `user_image_hash` | string | SHA-1 da foto, para o equipamento reaproveitar a que já tem em cache. |
| `portal_id` | número | Portal do acesso (a documentação da Control iD o descreve como string na tabela, mas os exemplos oficiais e a biblioteca usam número). |
| `duress` | número | `1` para dedo de pânico, `0` para identificação comum. |
| `message` | string | Mensagem exibida no display. |
| `actions` | array | Ações que o equipamento executa. Cada item tem `action` e `parameters`, ambos strings. |

Campos nulos são **omitidos** (o serializador usa `JsonIgnoreCondition.WhenWritingNull`), então só aparecem os que fizerem
sentido para a decisão. `false`, `0` e strings vazias são emitidos normalmente.

Exemplos mínimos:

```csharp
// Concede o acesso.
OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1));
// {"result":{"event":7,"user_id":6,"user_name":"Neal Caffrey","actions":[{"action":"door","parameters":"door=1"}]}}

// Nega o acesso com mensagem.
OnlineAuthorization.Deny(6, "Neal Caffrey", "Fora do horário");
// {"result":{"event":6,"user_id":6,"user_name":"Neal Caffrey","message":"Fora do horário"}}

// Credencial não reconhecida.
OnlineAuthorization.NotIdentified("Credencial não reconhecida");
// {"result":{"event":3,"message":"Credencial não reconhecida"}}
```

Para os demais campos, use as propriedades de `OnlineAuthorization` (`PortalId`, `UserImage`, `UserImageHash`,
`Message`, `Duress`).

### Formato de `actions`

O campo `parameters` é a string que o firmware entende, não um objeto. As fábricas de `DeviceAction` produzem os
formatos documentados:

| Ação | JSON | Fábrica |
| --- | --- | --- |
| Relé de porta | `{"action":"door","parameters":"door=1"}` | `DeviceAction.OpenDoor(1)` |
| SecBox | `{"action":"sec_box","parameters":"id=65793, reason=3"}` | `DeviceAction.OpenSecBox(65793, reason: 3)` |
| Catraca liberada para um sentido | `{"action":"catra","parameters":"allow=clockwise"}` | `DeviceAction.ReleaseTurnstile(TurnstileDirection.Clockwise)` |
| Relé da catraca | `{"action":"catra","parameters":"relay=1"}` | `DeviceAction.ActivateTurnstileRelay(1)` |
| Coletor (urna) da catraca | `{"action":"open_collector","parameters":""}` | `DeviceAction.OpenTurnstileCollector()` |

`DeviceAction.ReleaseTurnstile` aceita `Clockwise`, `Anticlockwise` e `Both` (`allow=clockwise`, `allow=anticlockwise` e
`allow=both`). `DeviceAction.OpenSecBox` usa `reason: 1` (autorizado) por padrão; para uma abertura originada de comando
web, use `reason: 3`, como no exemplo. O motivo também pode ser `0` (desconhecido) ou `2` (botoeira). Para uma ação que
a biblioteca ainda não tenha, `new DeviceAction("nome", "parametros")` emite o par informado.

Exemplo de concessão com as três ações do contrato:

```csharp
var resposta = OnlineAuthorization.Grant(
    6,
    "Neal Caffrey",
    DeviceAction.ReleaseTurnstile(TurnstileDirection.Clockwise),
    DeviceAction.OpenSecBox(65793, reason: 3),
    DeviceAction.OpenDoor(1));

resposta.PortalId = 1;
resposta.UserImage = true;
resposta.UserImageHash = hashSha1;
resposta.Message = "Acesso liberado";

return Results.Text(resposta.ToResponseJson(), "application/json");
```

## Códigos de `event`

O código vai como número (o enum não é serializado como texto). Os valores de `AccessEventType` são:

| Código | `AccessEventType` | Significado |
| --- | --- | --- |
| 0 | `Unknown` | Código que esta versão não conhece (leitura de logs de firmware novo); não use para decidir. |
| 1 | `InvalidDevice` | Equipamento inválido. |
| 2 | `InvalidIdentificationParameters` | Parâmetros de identificação inválidos. |
| 3 | `NotIdentified` | Não identificado. |
| 4 | `IdentificationPending` | Identificação pendente. |
| 5 | `IdentificationTimeout` | Tempo de identificação esgotado. |
| 6 | `AccessDenied` | Acesso negado. |
| 7 | `AccessGranted` | Acesso concedido. |
| 8 | `AccessPending` | Acesso pendente (depende de mais de uma pessoa). |
| 9 | `NotAdministrator` | Usuário não é administrador (tentou acessar o menu). |
| 10 | `UnidentifiedAccess` | Acesso não identificado (portal aberto pela API sem motivo). |
| 11 | `ExitButton` | Acesso por botoeira. |
| 12 | `WebInterface` | Acesso pela interface web. |
| 13 | `GaveUp` | Desistência de entrada (iDBlock). |
| 14 | `NoResponse` | Sem resposta (nenhuma ação é tomada). |
| 15 | `Intercom` | Acesso pela interfonia (iDFace). |

A documentação da linha iDAccess também lista `16` (acesso via OSDP, a partir da V5.9.4), `17` (acesso negado por
intertravamento, a partir da V5.16.0) e `18` (liberação pelo lado oposto, a partir da V5.20.0). O enum desta versão não
tem nomes para eles; para emitir um deles, faça o cast — o número é enviado normalmente:

```csharp
var resposta = OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1));
resposta.Event = (AccessEventType)16;
```

## Fallback configurável

Quando o `IOnlineAccessHandler` não decide, o equipamento recebe **200** com a `ControlIdOnlineOptions.FallbackAuthorization`
no lugar de um erro 500. O 200 evita que o equipamento conte a falha como problema de comunicação e entre em contingência.
O fallback vale para:

- exceção síncrona ou assíncrona lançada pelo handler;
- handler que devolve `null`;
- prazo de `DecisionTimeout` vencido (o `CancellationToken` do handler é cancelado);
- requisição de identificação que não pôde ser interpretada.

O padrão é negar o acesso com `event: 6` e a mensagem `"Sistema indisponível"`. Para trocar, configure as opções antes de
mapear as rotas:

```csharp
builder.Services.AddControlIdOnline<MeuHandler>();
builder.Services.Configure<ControlIdOnlineOptions>(o =>
{
    // O equipamento recebe {"result":{"event":3,"message":"mensagem"}}.
    o.FallbackAuthorization = OnlineAuthorization.NotIdentified("mensagem");

    // Padrão: 3 s. O equipamento espera no máximo 5 s por resposta.
    o.DecisionTimeout = TimeSpan.FromSeconds(3);
});

app.MapControlIdOnline("/api").RequireControlIdToken(token);
```

`DecisionTimeout` precisa ser maior que zero ou `Timeout.InfiniteTimeSpan` (desliga o prazo). O fallback não passa pelo
handler e continua sujeito à autenticação da rota: uma requisição sem token válido recebe 401, não o fallback.

## `device_id`, token e divergências

As rotas de callback são **fechadas por padrão** (401). A proteção vem de `RequireControlIdToken(...)`; sem ela, só
`AllowAnonymousControlIdCallbacks()` libera as rotas, e isso deve ficar restrito a rede isolada ou a outra autenticação na
frente. O token pode ser único/compartilhado (`ControlIdTokenOptions.Token` e `PreviousToken`) ou por equipamento
(`ControlIdTokenOptions.DeviceToken`).

### Sem `device_id`

- **Rotas de identificação** (`new_*.fcgi`): com token único ou rota anônima, a requisição é aceita e o handler recebe o
  evento com `DeviceId == null`; a checagem de equipamento conhecido (`ControlIdCallbackOptions.IsKnownDevice`) não é
  aplicada sem um id. Com token por equipamento, não há como escolher o token: a resposta é **401**, a menos que
  `AllowTokenFallback = true` e um `Token` compartilhado esteja configurado — nesse caso o handler também recebe
  `DeviceId == null`.
- **Rotas de cadastro** (`card_create.fcgi`, `face_create.fcgi`, `face_template_create.fcgi`, `tag_create.fcgi` etc.),
  **`new_user_enrolled.fcgi`** e **`new_rex_log.fcgi`**: o `device_id` pode vir da query ou do corpo. Faltando nos dois,
  vale a mesma regra acima (anônima/token único aceita; token por equipamento responde 401, salvo `AllowTokenFallback`).
- **`device_is_alive.fcgi` e `session_is_valid.fcgi`**: só usam o `device_id` da query. Sem ele, rota anônima/token único
  responde 200; token por equipamento responde 401, salvo `AllowTokenFallback`.
- **`user_get_image.fcgi`**: o `device_id` segue a regra da autenticação e o `user_id` continua obrigatório; sem ele a
  resposta é 400.

### `device_id` divergente

Nas rotas de cadastro, `new_user_enrolled.fcgi` e `new_rex_log.fcgi`, quando o `device_id` aparece **na query e no corpo**
com valores diferentes, a resposta é **403** (`CallbackGuard.SameDevice`) e o handler não é chamado. A checagem vem depois
da autenticação e não depende de qual token autorizou a requisição: numa rota anônima (liberada sem token), a divergência
também responde 403. Se a rota exigir token e ele faltar ou for inválido, a resposta é **401** antes de chegar a essa
checagem. Para autenticar e checar equipamento conhecido, a rota usa o `device_id` da query quando presente; mesmo que o
token dela seja válido, a divergência com o corpo continua dando 403.

Nas rotas de identificação não existe essa comparação entre query e corpo: os dois conjuntos de campos são mesclados e,
se ambos trouxerem `device_id`, o valor do formulário vence (é o último aplicado). Se a aplicação precisar recusar essa
divergência, valide-a no handler ou em um filtro antes de `MapControlIdOnline`.

Em qualquer rota, se `ControlIdCallbackOptions.IsKnownDevice` estiver configurado e recusar o id, a resposta é **403**; sem
`device_id`, a checagem é ignorada. Corpo acima de `MaxRequestBodySize` (4 MiB por padrão) responde **413** antes de chegar
ao handler.
