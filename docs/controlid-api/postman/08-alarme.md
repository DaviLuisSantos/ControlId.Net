# Alarme

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Desativar alarme

`POST {{host}}/alarm_status.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "stop": true
}
```

**Exemplo de resposta — Alarm deactivation successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Alarm deactivation failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

**Exemplo de resposta — Invalid configuration key** (400 Bad Request):

```json
{
    "error": "Node or attribute not found. Node path: config->general->param[name=invalid_config]",
    "code": 1
}
```

## Obter status do alarme

`POST {{host}}/alarm_status.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Exemplo de resposta — Status got successful** (200 OK):

```json
{
    "active": false,
    "cause": 0
}
```

**Exemplo de resposta — Status getting failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

**Exemplo de resposta — Invalid configuration key** (400 Bad Request):

```json
{
    "error": "Node or attribute not found. Node path: config->general->param[name=invalid_config]",
    "code": 1
}
```

## Tempo de atraso do sensor de porta

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "door_sensor_delay": "0"
    }
}
```

## Habilitar violação de equipamento

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "device_violation_enabled": "1"
    }
}
```

## Tempo de acionamento com a secbox fechada

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "door_sensor_alarm_timeout_after_closure": "0"
    }
}
```

## Habilitar sensor de porta

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "door_sensor_enabled": "1"
    }
}
```

## Tempo de debounce acesso forçado

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "forced_access_debounce": "0"
    }
}
```

## Habilitar acesso forçado

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "forced_access_enabled": "1"
    }
}
```

## Habilitar cartão do pânico

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "panic_card_enabled": "1"
    }
}
```

## Atraso no dedo do pânico

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "panic_finger_delay": "1"
    }
}
```

## Habilitar dedo do pânico

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "panic_finger_enabled": "1"
    }
}
```

## Habilitar senha de pânico

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "panic_password_enabled": "1"
    }
}
```

## Habilitar pin do pânico

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "alarm": {
        "panic_pin_enabled": "1"
    }
}
```
