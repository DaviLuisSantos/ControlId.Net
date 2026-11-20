<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/monitor/introducao-ao-monitor/ -->

# Introdução ao *Monitor*

Tanto no modo *Standalone* quanto nos modos *Pro* ou *Enterprise*, para se monitorar eventos assíncronos será necessário utilizar os serviços disponibilizados pelo Monitor.

Como o próprio nome sugere, o monitor permite o monitoramento de eventos que ocorrem no equipamento. Ele os envia a um servidor externo que deve ser configurado através dos *endpoints* descritos a seguir.

Para utilizar o *monitor* é necessário definir alguns parâmetros de configuração descritos em [*monitor*](../configuracoes/parametros-configuracao.md#monitor).

#### Exemplo de requisição

Esta requisição serve para configurar o *monitor*.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
             "monitor": {
                  "request_timeout": "5000",
                  "hostname": "192.168.0.20",
                  "port": "8000",
                  "path": "api/notifications" //path padrão
              }
        }
    )
});
```

**Observação:** O parâmetro hostname no exemplo de configuração acima também pode ser um endereço de domínio, como `meuservidor.com`, além de um endereço IP.

## Endpoints

Um *endpoint* de um web server é a URL pela qual um de seus serviços pode ser acessado por uma aplicação cliente. Portanto, *endpoints* são interfaces entre a API e a aplicação consumidora.

No caso do monitor, o terminal de controle de acesso assume o papel de aplicação cliente e é necessário estabelecer um servidor externo que implemente os *endpoints* que se deseja consumir.

A URL final para a qual os eventos serão envidados pelo equipamento será:

```
hostname:port/endpoint
```

**Exemplos de Endpoints:**

- http://meuservidor.com.br/api/notifications/dao
- 192.168.110.200:80/api/notifications/dao

### Logs

#### POST /api/notifications/dao

Enviado quando há alterações nas tabelas [access_logs](../objetos/lista-de-objetos.md#access_logs), [templates](../objetos/lista-de-objetos.md#templates), [cards](../objetos/lista-de-objetos.md#cards) e [alarm_logs](../objetos/lista-de-objetos.md#alarm_logs), sejam elas de inserção, alteração ou deleção.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "object_changes": [
    {
      "object": "access_logs",
      "type": "inserted",
      "values": {
        "id": "519",
        "time": "1532977090",
        "event": "12",
        "device_id": "478435",
        "identifier_id": "0",
        "user_id": "0",
        "portal_id": "1",
        "identification_rule_id": "0",
        "card_value": "0",
        "log_type_id": "-1"
      }
    }
  ],
  "device_id": 478435
}
```

#### POST /api/notifications/usb_drive

Enviado quando existe a inclusão de log de auditoria de usb_drive. Veja [Exportar Relatório](../objetos/exportar-relatorio.md) para mais informações.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "usb_drive": {
    "event":"Export formatted access logs routine succeeded [uuid=2C9D-476B]"
  },
  "device_id": 478435,
  "time" : 1490271121
}
```

### Cadastro remoto

#### POST /api/notifications/template

Enviado quando um template é cadastrado remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false** e a configuração [*extract_template*](../configuracoes/parametros-configuracao.md#online_client) estiver **ativada** (caso contrário, é enviada a notificação [fingerprint](../monitor/introducao-ao-monitor.md#post-apinotificationsfingerprint)). Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **finger_type** *(int)*: 1 se for dedo de pânico ou 0 caso contrário.
- **template** *(string)*: Template biométrico extraído pelo equipamento.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

#### POST /api/notifications/fingerprint

Enviado quando uma biometria é cadastrada remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false** e a configuração [*extract_template*](../configuracoes/parametros-configuracao.md#online_client) estiver **desativada**. Contém as imagens do dedo capturadas pelo equipamento. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **finger_type** *(int)*: 1 se for dedo de pânico ou 0 caso contrário.
- **card_id** *(string)*: Matrícula do usuário (pode ser vazio).
- **device_id** *(int64)*: ID do equipamento.
- **fingerprints** *(array de objetos JSON)*: Lista com as imagens das digitais capturadas, cada uma com os campos **width** *(int)*, **height** *(int)* e **image** *(string)*, a imagem em escala de cinza (1 byte por pixel) codificada em Base64.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "user_id": 1,
  "finger_type": 0,
  "card_id": "",
  "device_id": 478435,
  "fingerprints": [
    {
      "width": 300,
      "height": 200,
      "image": "/9j/4AAQSkZJRgABAQEASABIAAD ... QK5JP3FQw2eE1oQf/9k="
    }
  ]
}
```

#### POST /api/notifications/user_image

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Enviado quando uma face é cadastrada remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false** e a configuração **return_face_template** do módulo **enroller** esteja definida como **0 (padrão)**. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **user_image** *(string)*: Foto do usuário codificada em Base64.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

#### POST /api/notifications/face_template

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Enviado quando uma face é cadastrada remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false** e a configuração **return_face_template** do módulo **enroller** esteja definida como **1**. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **face_template** *(string)*: Template facial codificado em Base64.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

#### POST /api/notifications/card

Enviado quando um cartão é cadastrado remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false**. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **value** *(int64)*: Número do cartão.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

#### POST /api/notifications/tag

Disponível apenas em **equipamentos da linha iDAccess**.

Enviado quando uma tag é cadastrada remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false**. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **value** *(string)*: Valor da tag.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

#### POST /api/notifications/pin

Enviado quando um PIN é cadastrado remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false**. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **value** *(string)*: Valor do PIN.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

#### POST /api/notifications/password

Enviado quando uma senha é cadastrada remotamente, caso os parâmetros **sync** e **save** da requisição no *endpoint* **/remote_enroll.fcgi** forem **false**. Veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md) para mais informações.

Campos enviados no JSON:

- **user_id** *(int64)*: ID do usuário cadastrado.
- **value** *(string)*: Valor da senha.
- **device_id** *(int64)*: ID do equipamento.
- **time** *(int)*: Momento do envio em Unix Timestamp.

### Eventos de catraca

#### POST /api/notifications/catra_event

Este *endpoint* é exclusivo para a catraca iDBlock e envia eventos de confirmação do giro. Os eventos possíveis são:

- **EVENT_TURN_LEFT**: Confirmação de giro à esquerda, que poderá ser entrada ou saída dependendo do valor do parâmetro [gateway](../configuracoes/parametros-configuracao.md#catra).
- **EVENT_TURN_RIGHT**: Confirmação de giro à direita, que poderá ser entrada ou saída dependendo do valor do parâmetro [gateway](../configuracoes/parametros-configuracao.md#catra).
- **EVENT_GIVE_UP**: Ocorre quando um usuário se identifica na catraca e tem seu acesso autorizado, porém não passa por ela, desistindo assim da realização do acesso/giro.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "event": {
    "type": 7,
    "name": 'TURN LEFT',
    "time": 1484126902,
    "uuid": "0e039178"
  },
  "access_event_id": 15,
  "device_id": 935107,
  "time": 1484126902
}
```

Observação:

- O JSON enviado possui um campo opcional *access_event_id* que associa o evento reportado com o identificador correspondente da tabela [access_events](../objetos/lista-de-objetos.md#access_events). Esse campo é incluído quando o parâmetro de configuração do *monitor* [inform_access_event_id](../configuracoes/parametros-configuracao.md#monitor) está configurado com o valor 1.

### Estado online

#### POST /api/notifications/operation_mode

Uma requisição é enviada quando há mudança no modo de operação do equipamento (ex.: entra ou sai do modo de contingência). Quando o equipamento liga, esta requisição é enviada com o campo "last_offline": 0. Além disso há o campo exception_mode que indica se o equipamento está no modo exceção (podendo ser vazio).

Formato do JSON que o equipamento envia para o servidor:

```
{
  "operation_mode": {
    "mode": 0,
    "mode_name": "DEFAULT",
    "time": 1490271121,
    "last_offline": 1490261121
    "exception_mode": "none"
  },
  "device_id": 123456
}
```

#### POST /api/notifications/device_is_alive

Uma requisição é enviada neste *endpoint* para monitorar a conexão ao dispositivo quando o **modo standalone está habilitado.** O intervalo entre as requisições é determinado pela configuração *alive_interval*, sendo de 30 segundos por padrão. Essa configuração, dada em milisegundos, pode ser modificada através do endpoint *set_configuration* e obtida através do endpoint *get_configuration*, ambos com os seguintes parâmetros:

```
'set_configuration'
{
  monitor: {
    alive_interval: 30000
  }
}
```

E

```
'get_configuration'
{
  monitor: [alive_interval]
}
```

Na mensagem, é enviado no corpo da requisição (JSON) o número de logs disponíveis .

Formato do JSON que o equipamento envia para o servidor:

```
{
  "access_logs": 0,
  "device_id": 6613047045004349,
  "time": 1739376235
}
```

### Ações

#### POST /api/notifications/door

Uma requisição é enviada neste *endpoint* quando ocorre uma alteração de estado das portas, o que, na maior parte dos casos, indicará quando uma porta é aberta ou fechada. Esta alteração é percebida pelo equipamento de duas formas diferentes:

- Abertura ou fechamento de relé.
- Alteração de leitura do sensor de porta. (Somente se o sensor correspondente estiver ativo: consultar [door_sensorN_enabled](../configuracoes/parametros-configuracao.md#general))

Na mensagem, consta o id da porta que mudou de estado e o seu estado atual (aberta ou fechada). O id da porta é representado por um número inteiro que vai de 1 ao número de portas disponíveis, podendo chegar a 4, caso seja uma iDBox. O estado da porta é um valor booleano *open*: verdadeiro se estiver aberta e falso se estiver fechada.

O estado informado no parâmetro *open* poderá ser o estado lido do sensor de porta, caso o sensor de porta esteja ativo, ou o estado do relé, caso contrário.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "door": {
    "id" : 1,
    "open" : true
  },
  "access_event_id": 15,
  "device_id": 1038508,
  "time": 1575475894
}
```

Observação:

- O JSON enviado possui um campo opcional *access_event_id* que associa o evento reportado com o identificador correspondente da tabela [access_events](../objetos/lista-de-objetos.md#access_events). Esse campo é incluído quando o parâmetro de configuração do *monitor* [inform_access_event_id](../configuracoes/parametros-configuracao.md#monitor) está configurado com o valor 1.
- Nos **equipamentos da linha iDAccess**, o JSON também contém o objeto **event**, com os campos **type** *(int)* (código do evento: 1 para abertura, 2 para fechamento e 3 para desistência), **name** *(string)* (nome do evento: "OPEN", "CLOSE" ou "GIVE UP"), **time** *(int)* (momento do evento em Unix Timestamp) e **uuid** *(string)* (identificador único do evento de acesso).
- Nos **equipamentos da linha iDAccess**, quando o usuário tem o acesso liberado mas desiste de passar pela porta, é enviada uma notificação em que o objeto **door** contém o campo **give_up** *(bool)* com valor **true** no lugar do campo **open**.

Exemplo de JSON enviado por um equipamento da linha iDAccess:

```
{
  "door": {
    "id" : 1,
    "open" : true
  },
  "event": {
    "type": 1,
    "name": "OPEN",
    "time": 1575475894,
    "uuid": "0e039178"
  },
  "access_event_id": 15,
  "device_id": 1038508,
  "time": 1575475894
}
```

#### POST /api/notifications/secbox

Uma requisição é enviada neste *endpoint* quando o relê da security box muda de estado, o que indicará quando uma porta é aberta ou fechada.

Na mensagem, consta o id da porta que mudou de estado e o estado atual. O id da porta é representado por um número inteiro enquanto o estado é um valor booleano, que será verdadeiro se ela estiver aberta e falso se estiver fechada.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "secbox": {
    "id" : 122641794705028910,
    "open" : true
  },
  "event": {
    "type": 1,
    "name": "OPEN",
    "time": 1575475894,
    "uuid": "0e039178"
  },
  "access_event_id": 15,
  "device_id": 1038508,
  "time": 1575475894
}
```

Observação:

- O JSON enviado possui um campo opcional *access_event_id* que associa o evento reportado com o identificador correspondente da tabela [access_events](../objetos/lista-de-objetos.md#access_events). Esse campo é incluído quando o parâmetro de configuração do *monitor* [inform_access_event_id](../configuracoes/parametros-configuracao.md#monitor) está configurado com o valor 1.
- O objeto **event** contém os campos **type** *(int)* (código do evento: 1 para abertura, 2 para fechamento e 3 para desistência), **name** *(string)* (nome do evento: "OPEN", "CLOSE" ou "GIVE UP"), **time** *(int)* (momento do evento em Unix Timestamp) e **uuid** *(string)* (identificador único do evento de acesso). Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- Quando o usuário tem o acesso liberado mas desiste de passar pela porta, é enviada uma notificação em que o objeto **secbox** contém o campo **give_up** *(bool)* com valor **true** no lugar do campo **open**. Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

#### POST /api/notifications/access_photo

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Uma requisição é enviada neste *endpoint* quando há algum tipo de evento de identificação e a **opção para envio de imagens via monitor está habilitada.** Essa opção se encontra tanto na interface do equipamento quanto na interface WEB, e pode ser modificada através do endpoint *set_configuration* e obtida através do endpoint *get_configuration*, ambos com os seguintes parâmetros:

```
'set_configuration'
{
  monitor: {
    enable_photo_upload: 1
  }
}
```

E

```
'get_configuration'
{
  monitor: [enable_photo_upload]
}
```

Na mensagem, consta o id da porta acionada pela identificação, o id do dispositivo que realizou a identificação, o horário no qual houve a identificação, o id do usuário identificado, bem como uma foto tirada pelo equipamento no momento da identificação.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "device_id": "478435",
  "time": "1532977090",
  "portal_id": "1",
  "identifier_id": "0",
  "event": "7",
  "user_id": "0",
  "access_photo" : foto em jpg codificada em base64.
}
```

Observação:
- A foto enviada estará em formato jpeg, codificada em base64.

### Interfonia

#### POST /api/notifications/sip_call

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Uma requisição é enviada neste *endpoint* sempre que o equipamento realiza uma chamada de interfonia SIP. A mensagem não possui campos específicos: contém apenas o ID do equipamento e o momento do envio.

Formato do JSON que o equipamento envia para o servidor:

```
{
  "device_id": 478435,
  "time": 1490271121
}
```
