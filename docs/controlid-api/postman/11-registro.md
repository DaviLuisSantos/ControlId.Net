# Registro

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Registrar dedo de pânico (sync)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "biometry",
    "user_id": 1,
    "save": false,
    "sync": true,
    "panic_finger": 1
}
```

## Registrar dedo de pânico (async)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "biometry",
    "user_id": 1,
    "save": false,
    "sync": false,
    "panic_finger": 1
}
```

## Registrar dedo (sync)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "biometry",
    "user_id": 1,
    "save": false,
    "sync": true,
    "panic_finger": 0
}
```

## Registrar senha (sync)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "password",
    "user_id": 1,
    "save": false,
    "sync": true
}
```

## Registrar PIN (sync)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "pin",
    "user_id": 1,
    "save": false,
    "sync": true
}
```

## Registrar PIN (async)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "pin",
    "user_id": 1,
    "save": false,
    "sync": false
}
```

## Registrar senha (async)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "password",
    "user_id": 1,
    "save": false,
    "sync": false
}
```

## Registrar dedo (async)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "biometry",
    "user_id": 1,
    "save": false,
    "sync": false,
    "panic_finger": 0
}
```

## Registrar face (sync)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "face",
    "user_id": 1,
    "save": false,
    "sync": true,
    "auto": true,
    "countdown": 10
}
```

## Registrar face (async)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "face",
    "user_id": 1,
    "save": false,
    "sync": false,
    "auto": true,
    "countdown": 10
}
```

## Registrar cartão proximidade (sync)

`POST {{host}}/remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "type": "card",
    "user_id": 1,
    "save": true,
    "sync": true,
    "message": ""
}
```

## Cancelar registro remoto

`POST {{host}}/cancel_remote_enroll.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{}
```

## Obter estado atual do registro

`POST {{host}}/enroller_state.fcgi?session={{session}}`

**Exemplo de resposta — 200 - OK** (200 OK):

```json
{
    "enroller_state": "NORMAL_STATE",
    "biometry_sate": "NOT_ENROLLING",
    "last_enroll": false,
    "last_enroll_error": "UNKNOWN"
}
```
