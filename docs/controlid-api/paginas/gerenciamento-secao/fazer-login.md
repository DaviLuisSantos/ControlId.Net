<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/gerenciamento-secao/fazer-login/ -->

# Fazer login

Comando para criar uma sessão, que é necessária para todos os outros comandos, exceto **session_is_valid**. O método HTTP usado para o envio dos dados é o **POST**.

### POST /login.fcgi

*Parâmetros*

- **login** *(string)* : O valor padrão é **admin**
- **password** *(string)* : O valor padrão é **admin**

*Resposta*

- **session** *(string)* : Código da sessão iniciada.

#### Exemplo de requisição

Esta requisição irá criar a sessão e armazená-la na variável *session*.

```
$.ajax({
  url: "/login.fcgi",
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    login: 'admin',
    password: 'admin'
  }),
  success: function(data) {
    session = data.session;
  }
});
```

#### Exemplo de resposta

```
{
    "session": "q/AcfpiU3QLRUqHKNrAh5srT"
}
```
