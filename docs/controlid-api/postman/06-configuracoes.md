# Configurações

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## iDBlock Next

### Habilitar relé 1 da catraca (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_1_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Desabilitar relé 1 da catraca (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_1_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Ativar relé 1 da catraca em sentido anti-horário (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_1_enable_direction": "left"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Ativar relé 1 da catraca em sentido horário (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_1_enable_direction": "right"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Habilitar relé 2 da catraca (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_2_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Desabilitar relé 2 da catraca (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_2_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Ativar relé 2 da catraca em sentido anti-horário (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_2_enable_direction": "left"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Ativar relé 2 da catraca em sentido horário (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_relay_2_enable_direction": "right"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

### Alterar modo de operação (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_role": "1",
		"catra_default_fsm": "0",
        "catra_side_to_enter": "0",
        "catra_timeout": "5000",
        "catra_sync_period": "5000",
        "catra_bio_sync_period": "1000",
        "catra_config_sync_period": "60"
	}
}
```

### Alterar modo de operação  iDFace ou IHM primários (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_role": "1"
	}
}
```

### Alterar modo de operação iDFace ou IHM secundários (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_role": "2"
	}
}
```

### Alterar modo de operação intervalo de tempo entre sincronismo de usuários (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_sync_period": "5000"
	}
}
```

### Alterar modo de operação intervalo de tempo entre sincronismo de biometrias (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_bio_sync_period": "1000"
	}
}
```

### Alterar modo de operação intervalo de tempo entre sincronismo de configurações (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_config_sync_period": "60"
	}
}
```

### Alterar modo de operação sentidos da catraca  (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"catra_default_fsm": "0"
	}
}
```

### Alterar modo de operação  sentido de giro da catraca (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_side_to_enter": "0"
	}
}
```

### Alterar modo de operação tempo de duração da liberação do giro (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
        "catra_timeout": "5000"
	}
}
```

### Habilitar saída de visitantes com cartão (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "sec_box": {
        "catra_collect_visitor_card": "1"
    }
}
```

### Desabilitar saída de visitantes com cartão (iDBlock Next)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "sec_box": {
        "catra_collect_visitor_card": "0"
    }
}
```

### Configurar LEDs na catraca

`POST {{host}}/remote_led_control.fcgi?session={{session}}`

Através da API podemos configurar as cores dos LEDs RGB presentes na iDBlock Next e engatilhar eventos que trocam estas cores e estados dos LEDs.

Em suma, as cores de evento autorizado e não-autorizado são permanentes em verde e vermelho, respectivamente, contudo podemos mudar a cor em estado de Idle para a que desejarmos, eviando-a com um evento de código 4, seguido de um evento de código 2 para ir ao estado Idle.

Para desligar as luzes, podemos enviar um evento de código 3.

**Corpo:**

```json
{
    "color": "0",
    "event": 1
}
```

## iDFace Max

### Alterar modo de saída secbox (normal)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"sec_box_out_mode": "0"
	}
}
```

### Alterar modo de saída secbox (rejeitados)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"sec_box_out_mode": "1"
	}
}
```

### Alterar modo de saída secbox (campainha)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"sec_box_out_mode": "2"
	}
}
```

### Alterar modo de saída secbox (alarme)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"sec_box_out_mode": "3"
	}
}
```

### Habilitar relé interno

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay1_enabled": "1"
	}
}
```

### Desabilitar relé interno

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay1_enabled": "0"
	}
}
```

### Habilitar fechamento inteligente relé interno

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay1_auto_close": "1"
	}
}
```

### Desabilitar fechamento inteligente relé interno

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay1_auto_close": "0"
	}
}
```

### Alterar timeout relé interno

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay1_enabled": "1",
		"relay1_timeout": "3000"
	}
}
```

### Alterar modo de saída relé interno (normal)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay_out_mode": "0"
	}
}
```

### Alterar modo de saída relé interno (rejeitados)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay_out_mode": "1"
	}
}
```

### Alterar modo de saída relé interno (campainha)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay_out_mode": "2"
	}
}
```

### Alterar modo de saída relé interno (alarme)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"relay_out_mode": "3"
	}
}
```

### Desabilitar GPIO1

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext1_mode": "0"
	}
}
```

### Alterar modo de operação GPIO2 (habilitar facial)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "1"
	}
}
```

### Alterar modo de operação GPIO2 (alarme)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "2"
	}
}
```

### Alterar modo de operação GPIO2 (emergência)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "3"
	}
}
```

### Alterar modo de operação GPIO2 (lockdown)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "4"
	}
}
```

### Alterar modo de operação GPIO2 (interfonia)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "5"
	}
}
```

### Alterar modo de operação GPIO2 (intertravamento)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "6"
	}
}
```

### Alterar modo de operação GPIO2 (abrir relé)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "7"
	}
}
```

### Alterar modo de operação GPIO2 (abrir secbox)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "8"
	}
}
```

### Alterar modo de operação GPIO2 (abrir relé/secbox)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext2_mode": "9"
	}
}
```

### Alterar nível ativo 0 GPIO3 (NC)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext3_idle": "0"
	}
}
```

### Alterar nível ativo 1 GPIO3 (NO)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"gpio_ext3_idle": "1"
	}
}
```

## Ativar modo online

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"online": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar modo online

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"online": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Habilitar acesso do SSH ao dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"ssh_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desabilita o acesso do SSH ao dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"ssh_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Habilitar endpoint de reinício do dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"url_reboot_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desabilita endpoint de reinício do dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"url_reboot_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar emergência

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"exception_mode": "emergency"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar emergência

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"exception_mode": "none"
	}
}
```

## Ativar extração de template

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"online_client": {
		"extract_template": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar extração de template

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"online_client": {
		"extract_template": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Habilitar relé 1 da catraca

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_1_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desabilitar relé 1 da catraca

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_1_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar relé 1 da catraca em sentido anti-horário

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_1_enable_direction": "left"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar relé 1 da catraca em sentido horário

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_1_enable_direction": "right"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Habilitar relé 2 da catraca

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_2_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desabilitar relé 2 da catraca

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_2_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar relé 2 da catraca em sentido anti-horário

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_2_enable_direction": "left"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar relé 2 da catraca em sentido horário

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"gpio": {
		"catra_relay_2_enable_direction": "right"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar identificação no dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"local_identification": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar identificação no dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"local_identification": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar identificação por Cartão

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "identifier": {
    "card_identification_enabled": "1"
  }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar identificação por Cartão

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "identifier": {
    "card_identification_enabled": "0"
  }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar identificação por Face

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "identifier": {
    "face_identification_enabled": "1"
  }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar identificação por Face

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "identifier": {
    "face_identification_enabled": "0"
  }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar identificação por QR Code

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "identifier": {
    "qrcode_identification_enabled": "1"
  }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar identificação por QR Code

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "identifier": {
    "qrcode_identification_enabled": "0"
  }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modificar horário do reboot diário

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"auto_reboot_hour": "1",
        "auto_reboot_minute": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Buscar todas configurações (iDAccess/iDFit)

`POST {{host}}/get_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": [
		"online",
		"beep_enabled",
		"relay1_enabled",
		"relay1_timeout",
		"relay1_auto_close",
		"relay2_enabled",
		"relay2_timeout",
		"relay2_auto_close",
		"bell_enabled",
		"bell_relay",
		"local_identification",
		"exception_mode",
		"language",
		"daylight_savings_time_start",
		"daylight_savings_time_end",
		"auto_reboot"
	],
	"mifare": ["byte_order"],
	"w_in0": ["byte_order"],
	"w_out0": ["data"],
	"alarm": [
		"siren_enabled"
	],
	"identifier": [
		"verbose_logging",
		"log_type",
		"multi_factor_authentication"
	],
	"bio_id": ["similarity_threshold_1ton"],
	"online_client": [
		"server_id", 
		"extract_template",
		"max_request_attempts"
	],
	"bio_module": ["var_min"],
	"monitor": [
		"path",
		"hostname",
		"port",
		"request_timeout"
	],
	"push_server": [
		"push_request_timeout",
		"push_request_period",
		"push_remote_address"
	]
}
```

## Buscar todas configurações (iDBlock)

`POST {{host}}/get_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": [
		"online",
		"beep_enabled",
		"bell_enabled",
		"bell_relay",
		"catra_timeout",
		"local_identification",
		"exception_mode",
		"language",
		"daylight_savings_time_start",
		"daylight_savings_time_end",
		"auto_reboot"
	],
	"w_in0": ["byte_order"],
	"w_in1": ["byte_order"],
	"alarm": [
		"siren_enabled",
		"siren_relay"
	],
	"identifier": [
		"verbose_logging",
		"log_type",
		"multi_factor_authentication"
	],
	"bio_id": ["similarity_threshold_1ton"],
	"online_client": [
		"server_id", 
		"extract_template",
		"max_request_attempts"
	],
	"catra": [
		"anti_passback",
		"daily_reset",
		"gateway",
		"operation_mode"
	],
	"bio_module": ["var_min"],
	"monitor": [
		"path",
		"hostname",
		"port",
		"request_timeout"
	],
	"push_server": [
		"push_request_timeout",
		"push_request_period",
		"push_remote_address"
	]
}
```

## Buscar todas configurações (iDBox)

`POST {{host}}/get_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": [
		"online",
		"beep_enabled",
		"relay1_enabled",
		"relay1_timeout",
		"relay1_auto_close",
		"relay2_enabled",
		"relay2_timeout",
		"relay2_auto_close",
		"relay3_enabled",
		"relay3_timeout",
		"relay3_auto_close",
		"relay4_enabled",
		"relay4_timeout",
		"relay4_auto_close",
		"bell_enabled",
		"bell_relay",
		"catra_timeout",
		"local_identification",
		"exception_mode",
		"language",
		"daylight_savings_time_start",
		"daylight_savings_time_end",
		"auto_reboot"
	],
	"w_in0": ["byte_order"],
	"w_in1": ["byte_order"],
	"w_in2": ["byte_order"],
	"w_in3": ["byte_order"],
	"alarm": [
		"siren_enabled",
		"siren_relay"
	],
	"identifier": [
		"verbose_logging",
		"log_type",
		"multi_factor_authentication"
	],
	"bio_id": ["similarity_threshold_1ton"],
	"online_client": [
		"server_id", 
		"extract_template",
		"max_request_attempts"
	],
	"monitor": [
		"path",
		"hostname",
		"port",
		"request_timeout"
	],
	"push_server": [
		"push_request_timeout",
		"push_request_period",
		"push_remote_address"
	]
}
```

## Buscar todas configurações (iDFlex)

`POST {{host}}/get_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": [
		"online",
		"beep_enabled",
		"bell_enabled",
		"bell_relay",
		"local_identification",
		"exception_mode",
		"language",
		"daylight_savings_time_start",
		"daylight_savings_time_end",
		"auto_reboot"
	],
	"mifare": ["byte_order"],
	"sec_box": ["out_mode"],
	"alarm": [
		"siren_enabled",
		"siren_relay"
	],
	"identifier": [
		"verbose_logging",
		"log_type",
		"multi_factor_authentication"
	],
	"bio_id": ["similarity_threshold_1ton"],
	"online_client": [
		"server_id", 
		"extract_template",
		"max_request_attempts"
	],
	"bio_module": ["var_min"],
	"monitor": [
		"path",
		"hostname",
		"port",
		"request_timeout"
	],
	"push_server": [
		"push_request_timeout",
		"push_request_period",
		"push_remote_address"
	]
}
```

## Alterar servidor

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"online_client": {
		"server_id": "473357"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Remover servidor

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"online_client": {
		"server_id": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar idioma (padrão)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"language": ""
	}
}
```

## Alterar idioma (espanhol)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"language": "spa_SPA"
	}
}
```

## Alterar idioma (inglês)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"language": "en_US"
	}
}
```

## Alterar idioma (português)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"language": "pt_BR"
	}
}
```

## Visualizar configurações do módulo mifare

`POST {{host}}/get_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"mifare": ["byte_order","read_block","read_sector","authentication_type","authentication_key"]
}
```

## Configuração de chave de cartão Mifare

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "mifare": {
        "authentication_type": "A",
        "authentication_key": "////////"
    }
}
```

**Exemplo de resposta — Configuration modified successfully** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo mifare (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"mifare": {
		"byte_order": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo mifare (W_26)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"mifare": {
		"byte_order": "W_26"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo mifare (LSB)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"mifare": {
		"byte_order": "LSB"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo mifare (LSB64)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"mifare": {
		"byte_order": "LSB_64"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo mifare (LSB W26)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"mifare": {
		"byte_order": "LSB_W_26"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo rs485 - enabled (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"rs485": {
		"enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo rs485 - enabled (1)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"rs485": {
		"enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo rs485 - legacy_mode (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"rs485": {
		"legacy_mode": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo rs485 - legacy_mode (1)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"rs485": {
		"legacy_mode": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar módulo rs485 - legacy_mode (2)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"rs485": {
		"legacy_mode": "2"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"data": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out (cartão autorizado)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"data": "CAD"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out (qualquer cartão)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"data": "RELAY_CARD"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_26)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "26"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_32)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "32"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_34)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "34"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_35)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "35"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_37)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "37"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_40)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "40"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_42)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "42"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar tamanho wiegand out (W_66)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_out0": {
		"size": "66"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out secbox (ID - padrão)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"out_mode": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out secbox (cartão autorizado)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"out_mode": "CARD"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out secbox (qualquer cartão)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"out_mode": "RELAY_CARD"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand out secbox (cartão do usuário)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"out_mode": "USERS_CARD"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_26)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "26"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_32)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "32"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_34)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "34"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_35)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "35"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_37(H10302))

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "37(H10302)"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_37(H10304))

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "37(H10304)"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_40)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "40"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_42)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "42"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_48)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "48"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_56)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "56"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_64)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "64"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand_format_size secbox (W_66)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"sec_box": {
		"wiegand_format_size": "66"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand in 0 (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_in0": {
		"byte_order": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand in 1 (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_in1": {
		"byte_order": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand in 2 (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_in2": {
		"byte_order": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar wiegand in 3 (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"w_in3": {
		"byte_order": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar card_reader0 (modo wiegand)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "card_reader0": {
        "mode": "WIN"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar card_reader0 (modo ABA Track II)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "card_reader0": {
        "mode": "MAG"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar card_reader0 (ordem dos bytes normal)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "card_reader0": {
        "mode": "WIN",
        "type": ""
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar card_reader0 (ordem dos bytes invertida)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "card_reader0": {
        "mode": "WIN",
        "type": "34"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar anti-passback

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"anti_passback": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar anti-passback

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"anti_passback": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo de operação (default)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"operation_mode": ""
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo de operação (ambas controladas)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"operation_mode": "blocked"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo de operação (entrada liberada)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"operation_mode": "entrance_open"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo de operação (saída liberada)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"operation_mode": "exit_open"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo de operação (ambas liberadas)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"catra": {
		"operation_mode": "both_open"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo do leitor HID (W37)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
        "format_w37": "1", 
        "format_w26": "0", 
        "format_mifare": "0", 
        "format_ask": "0", 
        "format_indala_b1": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo do leitor HID (W26)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
        "format_w37": "0", 
        "format_w26": "1", 
        "format_mifare": "0", 
        "format_ask": "0", 
        "format_indala_b1": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo do leitor HID (Mifare)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
        "format_w37": "0", 
        "format_w26": "0", 
        "format_mifare": "0", 
        "format_ask": "1", 
        "format_indala_b1": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo do leitor HID (ASK)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
        "format_w37": "0", 
        "format_w26": "0", 
        "format_mifare": "0", 
        "format_ask": "0", 
        "format_indala_b1": "1"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo do leitor HID (Indala)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
        "format_w37": "0", 
        "format_w26": "0", 
        "format_mifare": "0", 
        "format_ask": "1", 
        "format_indala_b1": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar Tamanho do código de usuário - RFID - (16 bits)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "rfid": {
        "ask_user_code_size" : "16"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar Tamanho do código de usuário - RFID - (24 bits)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "rfid": {
        "ask_user_code_size" : "24"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar Tamanho do código de usuário - RFID - (32 bits)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "rfid": {
        "ask_user_code_size" : "32"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar Tamanho do código de usuário - RFID - (40 bits)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "rfid": {
        "ask_user_code_size" : "40"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar Tamanho do código de área - RFID - (0 bits)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "rfid": {
        "ask_site_code_size" : "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar Tamanho do código de área - RFID - (8 bits)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "rfid": {
        "ask_site_code_size" : "8"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar w37_cardid_size (Leitor HID)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
       "w37_cardid_size": "35"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar uso de facility code (Leitor HID)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
       "ignore_facility": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar uso de facility code (Leitor HID)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "hid_reader": {
       "ignore_facility": "1"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modo de operação Alfanumérico - QR Code para linha facial

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_id": {
        "qrcode_legacy_mode_enabled": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modo de operação Somente Numérico Decimal - QR Code para linha facial

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_id": {
        "qrcode_legacy_mode_enabled": "1"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modo de operação Somente Numérico Hexadecimal - QR Code para linha facial Copy

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_id": {
        "qrcode_legacy_mode_enabled": "2"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modo de operação Alfanumérico - QR Code linha de Acesso V5

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "barras": {
        "qrcode_legacy_mode_enabled": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modo de operação Somente Numérico Decimal - QR Code linha de Acesso V5

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "barras": {
        "qrcode_legacy_mode_enabled": "1"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Modo de operação Somente Numérico Hexadecimal - QR Code linha de Acesso V5

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "barras": {
        "qrcode_legacy_mode_enabled": "2"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Alterar modo de operação QR Code (Legacy)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_id": {
        "qrcode_legacy_mode_enabled": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar beep

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"beep_enabled": "1"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desativar beep

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"beep_enabled": "0"
	}
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar licença Pro

`POST {{host}}/upgrade_fifty_thousand_face_templates.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "password" : "232468E3"
}
```

**Exemplo de resposta — License activated successful** (200 OK):

```json
{}
```

**Exemplo de resposta — License activation failed** (200 OK):

```json
{
    "error": "Invalid password",
    "code": 1
}
```

## Habilitar streaming de video RTSP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_enabled": "1"
    }
}
```

## Desabilitar streaming de video RTSP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_enabled": "0"
    }
}
```

## Alterar a porta do streaming de vídeo RTSP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_port": "556"
    }
}
```

## Configurar credenciais para streaming de video RTSP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_username": "admin",
        "rtsp_password": "admin"
    }
}
```

## Alterar o codec do streaming de vídeo (MJPEG)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_codec": "mjpeg"
    }
}
```

## Alterar o codec do streaming de vídeo (H.264)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_codec": "h264"
    }
}
```

## Alterar câmera da transmissão de vídeo (IR)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_rgb": "0"
    }
}
```

## Alterar câmera da transmissão de vídeo (RGB)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_rgb": "1"
    }
}
```

## Habilitar streaming de video ONVIF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "onvif_enabled": "1"
    }
}
```

## Alterar credencial de usuário do streaming de video ONVIF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "onvif_username": "onvif_user_example"
    }
}
```

## Definir senha para acesso à streaming de video ONVIF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "onvif_password": "onvif_password_example"
    }
}
```

## Desabilitar streaming de video ONVIF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "onvif_enabled": "0"
    }
}
```

## Alterar a porta do streaming de vídeo ONVIF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"onvif": {
        "onvif_port": "8001"
    }
}
```

## Alterar resolução do streaming para 720p

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_video_width": "1080",
		"rtsp_video_height": "720"
    }
}
```

## Alterar resolução do streaming para 360p

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_video_width": "640",
		"rtsp_video_height": "360"
    }
}
```

## Habilitar a transmissão de vídeo espelhada

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_flipped": "1"
    }
}
```

## Desabilitar a transmissão de vídeo espelhada

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"onvif": {
        "rtsp_flipped": "0"
    }
}
```

## Habilitar as mensagens de voz padrão do dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"buzzer": {
        "audio_message_not_identified": "default",
        "audio_message_authorized": "default",
        "audio_message_not_authorized": "default",
        "audio_message_use_mask": "default"
    }
}
```

## Desabilitar as mensagens de voz

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"buzzer": {
        "audio_message_not_identified": "disabled",
        "audio_message_authorized": "disabled",
        "audio_message_not_authorized": "disabled",
        "audio_message_use_mask": "disabled"
    }
}
```

## Habilitar as mensagens de voz customizadas

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"buzzer": {
        "audio_message_not_identified": "custom",
        "audio_message_authorized": "custom",
        "audio_message_not_authorized": "custom",
        "audio_message_use_mask": "custom"
    }
}
```

## Alterar o volume das mensagens de voz (baixo)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"buzzer": {
        "audio_message_volume_gain": "1"
    }
}
```

## Alterar o volume das mensagens de voz (médio)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"buzzer": {
        "audio_message_volume_gain": "2"
    }
}
```

## Alterar o volume das mensagens de voz (alto)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"buzzer": {
        "audio_message_volume_gain": "3"
    }
}
```

## Carregar mensagem sonora customizada

`POST {{host}}/set_audio_access_message.fcgi?event=authorized&current=1&total=1&session={{session}}`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário ``

## Download de mensagem sonora customizada

`POST {{host}}/get_audio_access_message.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "event": "authorized"
}
```

## Verificar áudios customizados de acesso presentes no dispositivo

`POST {{host}}/has_audio_access_messages.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

## Habilitar anti-passback

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"identifier": {
        "antipassback_enabled": "1"
    }
}
```

## Desabilitar anti-passback

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"identifier": {
        "antipassback_enabled": "0"
    }
}
```

## Alterar modo do anti-passback (daily_catra)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"identifier": {
        "antipassback_mode": "daily_catra"
    }
}
```

## Alterar modo do anti-passback (timed)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"identifier": {
        "antipassback_mode": "timed"
    }
}
```

## Alterar modo do anti-passback (timed_catra)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"identifier": {
        "antipassback_mode": "timed_catra"
    }
}
```

## Alterar tempo do anti-passback

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"identifier": {
        "antipassback_timeout": "60"
    }
}
```

## Alterar limiar de acendimento dos LEDs brancos (baixa luminosidade)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_module":{
        "light_threshold_led_activation": "40"
    }
}
```

## Alterar limiar de acendimento dos LEDs brancos (média luminosidade)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_module":{
        "light_threshold_led_activation": "280"
    }
}
```

## Alterar limiar de acendimento dos LEDs brancos (alta luminosidade)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_module":{
        "light_threshold_led_activation": "1500"
    }
}
```

## Alterar limiar de acendimento dos LEDs brancos (acesos em qualquer ambiente)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "face_module":{
        "light_threshold_led_activation": "3000"
    }
}
```

## Modificar Configurações UHF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"uhf": {
        "read_interval_diff_tags": "100"
    }
}
```

## Alterar modo de operação do relé interno iDUHF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"uhf": {
        "tag_detector_enabled": "1"
    }
}
```

## Modo Ponto

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"attendance_mode": "1"
	},
    "identifier" : {
        "log_type": "1"
    }
}
```

## Alterar modo de operação iDUHF

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"uhf": {
        "operation_mode": "continuous"
    }
}
```

## Habilita configuração para ocultar nome no Acesso

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"general": 
    {
        "hide_name_on_identification": "1"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Desabilita configuração para ocultar nome no Acesso

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"general": 
    {
        "hide_name_on_identification": "0"
    }
}
```

**Exemplo de resposta — Configuration modified successful** (200 OK):

```json
{}
```

**Exemplo de resposta — Configuration modifying failure** (401 Unauthorized):

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

## Ativar porta USB (iDFace)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"usb_port_enabled": "1"
	}
}
```

## Desativar porta USB (iDFace)

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"usb_port_enabled": "0"
	}
}
```

## Habilitar limpeza automática de todos os usuários e visitantes

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "general": {
        "clear_expired_users": "all"
    }
}
```

## Habilitar limpeza automática apenas para visitantes

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "general": {
        "clear_expired_users": "visitors"
    }
}
```

## Desabilitar limpeza automática

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "general": {
        "clear_expired_users": "none"
    }
}
```

## Habilitar o protocolo OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"enabled": "1"
	}
}
```

## Desabilitar o protocolo OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"enabled": "0"
	}
}
```

## Configura o endereço do módulo OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"pd_address": "9"
	}
}
```

## Habilitar o canal seguro obrigatório OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"enforce_secure_channel": "1"
	}
}
```

## Desabilitar o canal seguro obrigatório OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"enforce_secure_channel": "0"
	}
}
```

## Alterar a taxa de transmissão de dados OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"baud_rate": "19200"
	}
}
```

## Alterar formato de leitura do cartão OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"card_read_report_format": "wiegand"
	}
}
```

## Alterar tamanho do Wiegand OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"wiegand_size": "26"
	}
}
```

## Alterar o modo de saída OSDP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
	"osdp": {
		"out_mode": "CARD"
	}
}
```

## Habilitar o Modo de Instalação OSDP

`POST {{host}}/set_osdp_installation_mode.fcgi?session={{session}}`

**Corpo:**

```json
{
	"enable": true
}
```

## Desabilitar o Modo de Instalação OSDP

`POST {{host}}/set_osdp_installation_mode.fcgi?session={{session}}`

**Corpo:**

```json
{
	"enable": false
}
```

## Status do Modo de Instalação

`POST {{host}}/get_osdp_installation_mode.fcgi?session={{session}}`

## Habilitar NTP e configurar timezone

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "ntp": {
        "enabled": "1",
        "timezone": "UTC+3"
    }
}
```

## Desabilitar NTP

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "ntp": {
        "enabled": "0"
    }
}
```

## Habilitar acesso ao SNMP do dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

Comando para habilitar a requisição de parâmetros a serem encontrados pelo dispositivo conectado

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "snmp_agent": {
    "snmp_enabled": "1"
  }
}
```

## Desabilitar acesso ao SNMP do dispositivo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

Comando para desabilitar a requisição de parâmetros a serem encontrados pelo dispositivo conectado

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
  "snmp_agent": {
    "snmp_enabled": "0"
  }
}
```

## Configurar o Intertravamento via Rede

`POST {{host}}/set_network_interlock.fcgi?session={{session}}`

**Corpo:**

```json
{
	"interlock_enabled" : 1,
	"api_bypass_enabled" : 1,
	"button_bypass_enabled" : 1
}
```

## Configurar códigos para eventos não identificado e não autorizado via Wiegand

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"general": {
		"denied_transaction_code": 26,
		"send_code_when_not_identified": 1,
		"send_code_when_not_authorized": 0
	}
}
```
