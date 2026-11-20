# Fotos e Logotipo

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Habilitar mostrar a logo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "general": {
        "show_logo": "5"
    }
}
```

## Desabilitar mostrar logo

`POST {{host}}/set_configuration.fcgi?session={{session}}`

**Corpo:**

```json
{
    "general": {
        "show_logo": "0"
    }
}
```

## Recuperar logo

`POST {{host}}/logo.fcgi?session={{session}}&id=5`

**Headers:** `Content-Type: image/png`

## Mudar Logo

`POST {{host}}/logo_change.fcgi?session={{session}}&id=5`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário `yr1u6TofA/5.png`

## Remover Logo

`POST {{host}}/logo_destroy.fcgi?session={{session}}&id=5`

## Remover logo sem id

`POST {{host}}/logo_destroy.fcgi?session={{session}}`
