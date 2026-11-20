<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/desativar-interface-web/ -->

# Desativar Interface Web

Ativa ou desativa a interface Web, mantendo sempre a API funcional. O método HTTP usado é o **POST** e o contentType é **application/json**.

### POST /set_configuration.fcgi

*Parâmetros*

- O parâmetro será um Objeto JSON correspondente à configuração **web_server_enabled**.
- web_server_enabled (string) : Habilita a interface Web. A API continua completamente funcional, apenas a interface Web é alterada. Para desabilitar "0" e para habilitar "1".

*Outras formas de desativar*

- Diretamente pelo dispositivo usando a tela de **Interface Web** .
- Usando a própria interface Web, na aba **Desativar Interface Web**.

#### Exemplo de requisição

Desativa a Interface Web:

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {"web_server_enabled": "0"}
  })
});
```
