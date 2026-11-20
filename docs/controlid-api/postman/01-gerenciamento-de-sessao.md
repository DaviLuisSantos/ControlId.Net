# Gerenciamento de sessão

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

Neste tópico serão apresentados os comandos utilizados para administração da sessão nos equipamentos. Eles são importantes e necessários para iniciar, analisar e finalizar a comunicação via requisições HTTP com os dispositivos.

## Login

`POST {{host}}/login.fcgi`

Comando para criar uma sessão que é necessária para todos os outros comandos, exceto ***session_is_valid***. O método HTTP usado para o envio dos dados é o POST.

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "login": "{{login}}",
    "password": "{{password}}"
}
```

**Exemplo de resposta — Sucesso** (200 OK):

```json
{
    "session": "K5Jvtkv0tP7xhdl67oKTjTfJ"
}
```

## Logout

`POST {{host}}/logout.fcgi?session={{session}}`

Comando para finalizar a sessão corrente.

## Checar validade da sessão

`POST {{host}}/session_is_valid.fcgi?session={{session}}`

Comando para verificar a validade de uma sessão.

**Exemplo de resposta — Sucesso** (200 OK):

```json
{
    "session_is_valid": true
}
```

## Alterar login e senha

`POST {{host}}/change_login.fcgi?session={{session}}`

Comando para alterar o usuário e/ou a senha utilizados para login no equipamento.

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"login": "{{login}}",
	"password": "{{password}}"
}
```
