<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/remover-administradores/ -->

# Remover administradores

Remove todos os administradores do equipamento. O método HTTP usado é o **POST**.

### POST /delete_admins.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Remove todos os administradores do equipamento:

```
$.ajax({
    url: "/delete_admins.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
