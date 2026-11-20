<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/modificar-configuracoes/ -->

# Modificar Configurações

Modifica as configurações especificadas do equipamento. Elas estão descritas em [Parâmetros de Configuração](../configuracoes/parametros-configuracao.md).
O método HTTP usado é o **POST** e o **contentType** é **application/json**.

### POST /set_configuration.fcgi

*Parâmetros*

- O parâmetro será um **Objeto JSON** correspondente aos parâmetros de configuração que se deseja modificar.

*Resposta*

- A resposta será um **Objeto JSON** correspondente aos parâmetros de configuração modificados.

#### Exemplo de requisição

Habilita o som de beep e configura o tempo de ativação do relay 1 para 3 segundos:

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {"beep_enabled": "1"}
  })
});
```
