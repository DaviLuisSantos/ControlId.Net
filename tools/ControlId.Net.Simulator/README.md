# Simulador de equipamento Control iD

Servidor que fala a API `*.fcgi` da linha de acesso (iDFace, iDFlex, iDAccess), com estado em memória. Serve para testar
o ControlId.Net, os samples e a sua própria aplicação sem um equipamento na mesa.

```bash
docker run --rm -p 8080:8080 ghcr.io/daviluissantos/controlid-simulator
# ou, a partir do repositório:
dotnet run --project tools/ControlId.Net.Simulator -- --urls http://0.0.0.0:8080
```

```csharp
using var device = new AccessClient("http://localhost:8080", "admin", "admin");
var users = await device.GetUsersAsync();   // Ana, Bruno e Carla, dos dados de exemplo
```

## O que ele simula

| Área | Comandos |
| --- | --- |
| Sessão | `login`, `logout`, `session_is_valid`; sessão inválida devolve 401, como o equipamento |
| Objetos | `load_objects` (com `where`, `order`, `limit`, `offset`, `fields`), `create_objects`, `modify_objects`, `destroy_objects` (com `limit`), `create_or_modify_objects` |
| Fotos | `user_set_image`, `user_get_image` (inclusive com `get_timestamp`), `user_list_images`, `user_destroy_image` |
| Configuração | `get_configuration`, `set_configuration` |
| Equipamento | `execute_actions` (porta), `doors_state`, `system_information`, `message_to_screen`, `buzzer_buzz`, `change_login`, `reboot` |
| Monitor | Notificações `dao` (logs de acesso) e `door` enviadas para o `hostname`, `port` e `path` da seção `monitor` |

Comportamentos do firmware real que o simulador reproduz, porque a biblioteca depende deles:

- `get_configuration` recusa a leitura inteira se um parâmetro não existir, com a mensagem `Node or attribute not found.
  Node path: config->{seção}->param[name={campo}]` (iDFace 6.9.12, [ADR 0007](../../docs/adr/0007-configuracao-tolerante-a-parametros-ausentes.md)).
- Comando desconhecido devolve HTTP 400 com `Invalid command`.
- Nas notificações do Monitor, os valores de `object_changes` chegam como texto (`"id": "519"`).
- `system_information` devolve o `device_id` como texto.

Um comando fora da lista acima responde `Invalid command`. Parâmetros inexistentes em `set_configuration` são aceitos (o
equipamento os recusaria).

## Rotas de controle

Rotas que não existem no equipamento e provocam o que, no mundo real, vem de uma pessoa ou de um evento:

| Rota | Efeito |
| --- | --- |
| `POST /simulator/identify` `{"userId": 1}` ou `{"cardValue": 4194301001}` | Uma pessoa se identifica: grava o log de acesso (liberado ou não identificado), abre a porta e notifica o Monitor |
| `POST /simulator/expire-sessions` | Encerra as sessões, como um reinício; o cliente deve refazer o login sozinho |
| `POST /simulator/reset` | Volta ao estado inicial |
| `GET /simulator/state` | Tabelas, portas, fotos, destino do Monitor e as últimas entregas |
| `GET /health` | Health check |

## Configuração

Seção `Simulator` do `appsettings.json`, argumentos (`--Simulator:Password=...`) ou variáveis de ambiente
(`Simulator__Password=...`):

| Opção | Padrão |
| --- | --- |
| `Login` / `Password` | `admin` / `admin` |
| `DeviceId` | `935107` |
| `Serial` / `Firmware` | `SIM0000001` / `6.13.1-sim` |
| `Doors` | `1` (até 4) |
| `DoorOpenTime` | `00:00:03` |
| `SeedSampleData` | `true`: três usuários e dois cartões |

Com Docker, o Monitor do simulador precisa alcançar a sua máquina: use `host.docker.internal` como `hostname`.

## Testes de integração

`tests/ControlId.Net.IntegrationTests` sobe um simulador por teste, numa porta livre, e usa a biblioteca com HTTP de
verdade. O teste do Monitor sobe também um servidor com as rotas do `ControlId.Net.AspNetCore` e confere que a
identificação chega lá. Veja a [ADR 0011](../../docs/adr/0011-simulador-de-equipamento.md).
