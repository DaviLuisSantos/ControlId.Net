<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/ativar-desativar-usb/ -->

# Ativar e desativar a porta USB

Ativa e desativa a porta USB. Quando desativada, impossibilita qualquer tipo de ação através da porta. O método HTTP usado é o **POST** e o **contentType** é **application/json**.

### POST /set_configuration.fcgi

*Parâmetros*

- O parâmetro será um **Objeto JSON** correspondente aos parâmetros de configuração que se deseja modificar.

*Resposta*

- A resposta será um **Objeto JSON** correspondente aos parâmetros de configuração modificados.

#### Exemplo de requisição

Habilita a porta USB:

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {"usb_port_enabled": "1"}
  })
});
```

Desabilita a porta USB:

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {"usb_port_enabled": "0"}
  })
});
```
