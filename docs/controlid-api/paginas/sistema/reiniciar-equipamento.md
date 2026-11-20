<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/reiniciar-equipamento/ -->

# Reiniciar Equipamento

O reinício do equipamento pode ser feito de dois modos.

### POST /reboot.fcgi

Reinicia o equipamento tendo uma sessão válida. O método HTTP usado é o **POST**.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Reinicia o equipamento:

```
$.ajax({
    url: "/reboot.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

### GET /reboot

Reinicia o equipamento sem necessitar de uma sessão válida. Para esse modo é preciso que a configuração `url_reboot_enabled`, do módulo `general`, tenha sido habilitada. O método HTTP usado é o **GET**.

**Cuidado:** Esse recurso deve ser usado com muito cuidado, pois a execução desse endpoint fará com que o dispositivo seja reinicializado. Adicionamos esse recurso para dar suporte a situações de reinicialização remota de equipamentos quando a aplicação não está mais respondendo.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Reinicia o equipamento:

```
$.ajax({
    url: "/reboot",
    type: 'GET',
    contentType: 'application/json'
});
```
