# Push

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Ativar o push

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"push_server": {
		"push_request_timeout": "5000",
		"push_request_period": "15",
		"push_remote_address": "http://192.168.6.217:3000"
	}
}
```

## Desativar o push

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"push_server": {
		"push_remote_address": ""	
	}
}
```

## Conexão iDCloud

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "push_server": {
        "push_remote_address":"https://push.idsecure.com.br/api",
        "push_request_timeout":"30000",
        "push_request_period":"5"
    }
}
```

## Alterar código de verificação

`GET {{host}}/change_idcloud_code.fcgi?session={{session}}`
