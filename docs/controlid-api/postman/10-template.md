# Template

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Extrair template

`POST {{host}}/template_extract.fcgi?session={{session}}&width=260&height=300`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário ``

**Exemplo de resposta — 200 - OK** (200 OK):

```json
{
    "quality": 88,
    "template": "base64"
}
```

## Verificar template

`POST {{host}}/template_match.fcgi?session={{session}}&temp_num=2&size0=858&size1=858`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário ``

**Exemplo de resposta — 200 - OK** (200 OK):

```json
{
    "template": "base64"
}
```

## Verificar template único

`POST {{host}}/template_match.fcgi?session={{session}}&temp_num=1&size0=894`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário ``

**Exemplo de resposta — 200 - OK** (200 OK):

```json
{
    "template": "base64"
}
```

## Habilitar sincronismo de biometrias

`POST {{host}}/template_sync_init.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

## Desabilitar sincronismo de biomatrias

`POST {{host}}/template_sync_end.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

## Verificar validade de template

`POST {{host}}/is_valid_biometry.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "biometry": "SUNSUzIxAAABRgEBAAAAAMUAxQAEASwBAAAAgRgFzwCLAI0PqgDJAHAP4QDTAJcP2ADeACIPtgDgAHQPSx8r+zNvPf8jURvaRXReJ3MqPYzesfO1uQcKVQLfESAXAQDVCYwFANNcDz4IALBhA/tdRgYAyWEMNcEKAJ5oAClUPgsAkHP9MP/+wMBCBgCRgPD8/v/+BQCXhf3/MgUA044WwDYMAIqS9zM8/0EMAIqe9P4z/zVBDACRpfr/MTJADACPsff//MAp/1UIAIXE5Pv/J8EDAKXJdMMMAIjN5/37/z7/NQQAstyDxsIIALrhEP3+/8FXDACL4uL9//r//MD+wGQFALLidMOqBQDd4iD/TAkQoA3M+vcewsELEJgRwPz7+/v8//2JBxCvG6348PrAwkRCAQEAAAAWAAAAAAIFAAAAAAAARUI="
}
```
