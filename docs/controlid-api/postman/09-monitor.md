# Monitor

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Ativar o monitor

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"monitor": {
		"request_timeout": "5000",
		"hostname": "192.168.6.217",
		"port": "3000"
	}
}
```

## Desativar o monitor

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"monitor": {
		"hostname": "",
		"port": ""
	}
}
```

## Alterar URL

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"monitor": {
		"path": "api/v1/notifications"
	}
}
```

## Voltar URL para padrão

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"monitor": {
		"path": "api/notifications"
	}
}
```
