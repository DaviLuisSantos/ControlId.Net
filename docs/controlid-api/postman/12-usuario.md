# Usuário

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Atualizar imagem do usuário

`POST {{host}}/user_set_image.fcgi?session={{session}}&user_id=10`

**Headers:** `Content-Type: application/octet-stream`

**Corpo:** arquivo binário `/C:/Users/Douglas Miguel/Downloads/foto.jpg`

## Obter imagem do usuário

`POST {{host}}/user_get_image.fcgi?session={{session}}&user_id=10`

## Gerar HASH de senha

`POST {{host}}/user_hash_password.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"password": "123"
}
```

## Adicionar campo customizado

`POST {{host}}/object_add_field.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "c_users",
	"column_name": "birthdate",
	"name": "Birthdate",
	"type": "TEXT",
	"constraint": "NONE",
	"default_value": "",
	"unique": false
}
```

## Apagar campo customizado

`POST {{host}}/object_remove_fields.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"ids": "<Array de int>: -> campos a serem deletados"
}
```
