<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/hardware/diagnostico-de-entradas-e-saidas/ -->

# Diagnóstico de entradas e saídas NEW!

Retorna, em uma única chamada, o estado de todos os GPIOs, relés e SecBoxes do equipamento, junto com a configuração relevante de cada um. Para ler um único GPIO, veja [Ler estado de GPIO](../hardware/ler-estado-de-gpio.md).

Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /io_state.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **gpios** *(array de objetos JSON)*: Um item por GPIO do equipamento. Cada item contém:
  > - **index** *(int)*: Número do GPIO.
  > - **role** *(string)*: Função do GPIO: `"tamper"` (sensor de violação), `"gpio_ext"` (entrada configurável), `"relay"` (relé), `"speaker"` (alto-falante) ou `"generic"`.
  > - **ext_id** *(int)*: Número da entrada configurável (o `N` de `gpio_extN`). Presente apenas quando **role** é `"gpio_ext"`.
  > - **mode** *(int)*: Modo de operação da entrada configurável, com os mesmos valores de `gpio_extN_mode` em [Sinais configuráveis iDFace Max](../particularidade-dos-produtos/sinais-configuraveis-idface-max.md). Presente apenas quando **role** é `"gpio_ext"`.
  > - **activation_mode** *(int)*: Modo de ativação da entrada configurável: `1` (pulso) ou `2` (borda). Presente apenas quando **role** é `"gpio_ext"`.
  > - **debounce_ms** *(int)*: Tempo de debounce da entrada configurável, em milissegundos. Presente apenas quando **role** é `"gpio_ext"`.
  > - **enabled** *(bool)*: Indica se o GPIO está habilitado.
  > - **available** *(bool)*: Indica se o estado do GPIO pôde ser lido. É `false` para GPIOs desabilitados. Os campos abaixo só estão presentes quando **available** é `true`.
  > - **in** *(bool)*: Indica se o GPIO está configurado como entrada (`true`) ou saída (`false`).
  > - **value** *(int)*: Valor atual do GPIO.
  > - **idle** *(int)*: Valor do GPIO quando ocioso.
  > - **active** *(bool)*: Indica se o GPIO está ativo, isto é, se **value** é diferente de **idle**.
  > - **notify** *(bool)*: Indica se o software de acesso é avisado de alterações nesse GPIO.
  > - **notified** *(int)*: Quantas vezes o GPIO notificou o software de acesso de uma alteração.
- **relays** *(array de objetos JSON)*: Um item por relé do equipamento. Cada item contém:
  > - **id** *(int)*: Número do relé, a partir de 1.
  > - **gpio** *(int)*: GPIO que aciona o relé.
  > - **enabled** *(bool)*: Indica se o relé está habilitado.
  > - **timeout_ms** *(int)*: Tempo de acionamento do relé, em milissegundos.
  > - **auto_close** *(bool)*: Indica se o fechamento automático está habilitado.
  > - **output_mode** *(int)*: Modo de saída do relé, com os mesmos valores de `relay_out_mode` em [Sinais configuráveis iDFace Max](../particularidade-dos-produtos/sinais-configuraveis-idface-max.md).
  > - **available** *(bool)*: Indica se o estado do relé pôde ser lido.
  > - **active** *(bool)*: Indica se o relé está acionado. Presente apenas quando **available** é `true`.
- **sec_boxes** *(array de objetos JSON)*: Um item por SecBox conectada. Cada item contém:
  > - **id** *(int)*: Identificador (UID) da SecBox.
  > - **serial** *(string)*: Número de série da SecBox.
  > - **version** *(string)*: Versão de firmware da SecBox.
  > - **enabled** *(bool)*: Indica se a SecBox está habilitada.
  > - **relay** *(bool)*: Estado do relé da SecBox.
  > - **rex** *(bool)*: Nível lido na entrada da botoeira da SecBox.
  > - **rex_active** *(bool)*: Indica se a botoeira está acionada.
  > - **door_sensor** *(bool)*: Nível lido na entrada do sensor de porta da SecBox.
  > - **door_sensor_enabled** *(bool)*: Indica se o sensor de porta está habilitado.
  > - **door_sensor_idle** *(int)*: Nível do sensor de porta quando ocioso.
  > - **door_sensor_active** *(bool)*: Indica se o sensor de porta está ativo, isto é, se o nível lido é diferente de **door_sensor_idle**.
  > - **open** *(bool)*: Indica se a porta da SecBox está aberta.

#### Exemplo de requisição

```
$.ajax({
    url: "/io_state.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "gpios": [
        {
            "index": 0,
            "role": "tamper",
            "enabled": true,
            "available": true,
            "in": true,
            "value": 1,
            "idle": 1,
            "active": false,
            "notify": true,
            "notified": 0
        },
        {
            "index": 1,
            "role": "gpio_ext",
            "ext_id": 1,
            "mode": 7,
            "activation_mode": 1,
            "debounce_ms": 50,
            "enabled": true,
            "available": true,
            "in": true,
            "value": 0,
            "idle": 0,
            "active": false,
            "notify": true,
            "notified": 3
        }
    ],
    "relays": [
        {
            "id": 1,
            "gpio": 4,
            "enabled": true,
            "timeout_ms": 3000,
            "auto_close": true,
            "output_mode": 0,
            "available": true,
            "active": false
        }
    ],
    "sec_boxes": [
        {
            "id": 65793,
            "serial": "0001010101",
            "version": "2.4.0",
            "enabled": true,
            "relay": false,
            "rex": true,
            "rex_active": false,
            "door_sensor": false,
            "door_sensor_enabled": true,
            "door_sensor_idle": 0,
            "door_sensor_active": false,
            "open": false
        }
    ]
}
```
