# Ações

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Abrir secbox (padrão)

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "sec_box",
				"parameters": "id=65793,reason=3"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Abrir secbox (3 segundos)

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "sec_box",
				"parameters": "id=65793,reason=3,timeout=3000"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Abrir porta 1 (padrão)

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
		{
			"action": "door",
			"parameters": "door=1"
		}
	]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Abrir porta 1 (3 segundos)

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
		{
			"action": "door",
			"parameters": "door=1,timeout=3000"
		}
	]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Abrir todas portas (padrão)

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "door",
				"parameters": "door=1"
			},
			{
				"action": "door",
				"parameters": "door=2"
			},
			{
				"action": "door",
				"parameters": "door=3"
			},
			{
				"action": "door",
				"parameters": "door=4"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Abrir todas portas (3 segundos)

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "door",
				"parameters": "door=1,timeout=3000"
			},
			{
				"action": "door",
				"parameters": "door=2,timeout=3000"
			},
			{
				"action": "door",
				"parameters": "door=3,timeout=3000"
			},
			{
				"action": "door",
				"parameters": "door=4,timeout=3000"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Abrir urna coletora

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "open_collector",
				"parameters": ""
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Manter secbox aberta

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "sec_box",
				"parameters": "id=65793,reason=3,timeout=0"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Manter porta aberta

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
		{
			"action": "door",
			"parameters": "door=1,timeout=0"
		}
	]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Fechar secbox

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "sec_box",
				"parameters": "id=65793,reason=3,timeout=-1"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Fechar porta 1

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
		{
			"action": "door",
			"parameters": "door=1,timeout=-1"
		}
	]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Fechar todas portas

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "door",
				"parameters": "door=1,timeout=-1"
			},
			{
				"action": "door",
				"parameters": "door=2,timeout=-1"
			},
			{
				"action": "door",
				"parameters": "door=3,timeout=-1"
			},
			{
				"action": "door",
				"parameters": "door=4,timeout=-1"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Liberar giro no sentido horário

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "catra",
				"parameters": "allow=clockwise"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Liberar giro no sentido anti-horário

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "catra",
				"parameters": "allow=anticlockwise"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Liberar giro em ambos sentidos

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "catra",
				"parameters": "allow=both"
			}
		]
}
```

**Exemplo de resposta — Open door (iDFlex)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Open door (iDAccess/iDFit/iDBox)** (200 OK):

```json
{"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
}
```

**Exemplo de resposta — Execute action failure** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

## Liberar relés catraca

`POST {{host}}/execute_actions.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"actions": [
			{
				"action": "catra",
				"parameters": "relay=1"
			}
		]
}
```

## Obter estado atual das portas

`POST {{host}}/doors_state.fcgi?session={{session}}`

## Acionamento remoto buzzer

`POST {{host}}/buzzer_buzz.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"frequency": 4000,
	"duty_cycle": 50,
	"timeout": 1000
}
```

## Personalizar mensagem de eventos

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "identifier": {
        "custom_auth_message": "Seja bem-vindo",
        "custom_deny_message": "Acesso negado",
        "custom_not_identified_message": "Usuário não reconhecido",
        "custom_mask_message": "Por favor, use máscara",
        "enable_custom_auth_message": "1",
        "enable_custom_deny_message": "1",
        "enable_custom_not_identified_message": "1",
        "enable_custom_mask_message": "1"
    }
}
```
