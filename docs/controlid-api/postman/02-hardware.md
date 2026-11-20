# Hardware

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Buscar estado do GPIO

`POST {{host}}/gpio_state.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": 11
}
```

**Exemplo de resposta — Sucesso** (200 OK):

```json
{
    "enabled": 1,
    "in": 0,
    "pin": "pe00",
    "idle": 0,
    "pullup": 0,
    "value": 0,
    "notify": 1,
    "notified": 0
}
```

## Recarregar LED RGB

`POST {{host}}/led_rgb_refresh.fcgi?session={{session}}`

## Coletar testes de fábrica

`GET {{host}}/tests.fcgi?session={{session}}`
