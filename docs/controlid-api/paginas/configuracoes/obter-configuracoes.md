<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/obter-configuracoes/ -->

# Obter Configurações

Obtêm as configurações especificadas do equipamento. Elas estão descritas em [Parâmetros de Configuração](../configuracoes/parametros-configuracao.md).
O método HTTP usado é o **POST** e o **contentType** é **application/json**.

### POST /get_configuration.fcgi

*Parâmetros*

- O parâmetro será um **Objeto JSON** correspondente aos parâmetros de configuração solicitados.

*Resposta*

- A resposta será um **Objeto JSON** correspondente aos parâmetros de configuração solicitados.

#### Exemplo de requisição

Obtêm o som de beep e o tempo de ativação do relay 1:

```
$.ajax({
  url: "/get_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: ["beep_enabled"]
  })
});
```
