# Interfonia SIP

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Configurar conexão

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "enabled": "1",
        "server_ip": "meu_servidor",
        "server_port": "5060",
        "server_outbound_port": "10000",
        "server_outbound_port_range": "1000",
        "numeric_branch_enabled": "0",
        "branch": "987",
        "login": "987",
        "password": "123456"
    }
}
```

## Configurar chamada

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "pjsip": {
        "auto_answer_enabled": "1",
        "auto_answer_delay": "5",
        "dialing_display_mode": "0",
        "auto_call_target": "456",
        "auto_call_button_enabled": "1",
        "custom_identifier_auto_call": "Portaria",
        "reg_status_query_period": "60",
        "server_retry_interval": "5",
        "max_call_time": "300",
        "push_button_debounce": "50",
        "video_enabled": "1"
    }
}
```

## Configurar volumes

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:**

```json
{
    "pjsip": {
        "mic_volume": "7",
        "speaker_volume": "8"
    }
}
```

## Habilitar liberação de acesso via interfonia

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "open_door_enabled": "1"
    }
}
```

## Desabilitar liberação de acesso via interfonia

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "open_door_enabled": "0"
    }
}
```

## Habilitar identificação durante interfonia

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "facial_id_during_call_enabled": "1"
    }
}
```

## Desabilitar identificação durante interfonia

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "facial_id_during_call_enabled": "0"
    }
}
```

## Habilitar chamada por botoeira de discagem

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "rex_enabled": "1"
    }
}
```

## Desabilitar chamada por botoeira de discagem

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "rex_enabled": "0"
    }
}
```

## Registrar código de discagem para liberação

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "pjsip": {
        "open_door_command": "12345"
    }
}
```

## Habilitar som de chamada personalizado

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"pjsip": {
        "pjsip_custom_audio_enabled": "1"
    }
}
```

## Desabilitar som de chamada personalizado

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"pjsip": {
        "pjsip_custom_audio_enabled": "0"
    }
}
```

## Configurar volume do som de chamada personalizado

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"pjsip": {
        "custom_audio_volume_gain": "1"
    }
}
```

## Carregar som de chamada personalizado

`POST {{host}}/set_pjsip_audio_message.fcgi?session={{session}}&current=1&total=1`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário `/home/luis/Downloads/ring.wav`

## Download de som de chamada personalizado

`POST {{host}}/get_pjsip_audio_message.fcgi?session={{session}}`

## Verificar se existe som de chamada personalizado no dispositivo

`POST {{host}}/has_audio_access_messages.fcgi?session={{session}}`

**Exemplo de resposta — Arquivo de som personalizado existente** (200 OK):

```json
{
    "file_exists": true
}
```

**Exemplo de resposta — Arquivo de som personalizado não existente** (200 OK):

```json
{
    "file_exists": false
}
```

## Realizar chamada SIP

`POST {{host}}/make_sip_call.fcgi?session={{session}}`

**Corpo:**

```json
{
    "target": "1234"
}
```

## Finalizar chamada SIP

`POST {{host}}/finalize_sip_call.fcgi?session={{session}}`

## Verificar status da chamada SIP

`POST {{host}}/get_sip_status.fcgi?session={{session}}`

**Exemplo de resposta — Chamada em andamento** (200 OK):

```json
{
    "status": 200,
    "in_call": true
}
```

**Exemplo de resposta — Nenhuma chamada em andamento** (200 OK):

```json
{
    "status": 200,
    "in_call": false
}
```
