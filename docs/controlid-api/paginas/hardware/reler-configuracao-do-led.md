<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/hardware/reler-configuracao-do-led/ -->

# Reler configuração do LED

Uma vez que a configuração do LED RGB tenha sido alterada, este comando sinaliza ao software de acesso do equipamento que a configuração de LED deve ser recarregada.

Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /led_rgb_refresh.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Sinaliza para o firmware de acesso reler a configuração do LED RGB:

```
$.ajax({
    url: "/led_rgb_refresh.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
