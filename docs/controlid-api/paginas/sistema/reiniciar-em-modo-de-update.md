<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/reiniciar-em-modo-de-update/ -->

# Reiniciar em modo de update

Reinicia o equipamento em modo de update. O método HTTP usado é o **POST**.

### POST /reboot_recovery.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Reinicia o equipamento em modo de update:

```
$.ajax({
    url: "/reboot_recovery.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
