<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/gerenciamento-secao/fazer-logout/ -->

# Fazer logout

Comando para finalizar a sessão corrente.

### POST /logout.fcgi

*Parâmetros*

- *Essa chamada não possui parâmetros*

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
  url: "/logout.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json'
});
```
