<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/intertravamento-via-rede/ -->

# Configurar o intertravamento via rede

São três configurações possíveis de serem modificadas:

- Ativar e desativar o intertravamento via rede;
- Ativar e desativar a opção de ignorar o intertravamento via rede ao abrir a porta via API;
- Ativar e desativar a opção de ignorar o intertravamento via rede ao abrir a porta via botoeira.

Vale ressaltar que, para o funcionamento do intertravamento via rede, é necessário cadastrar um ou mais dispositivos remotos, utilizando a API ou as interfaces do dispositivo.

O método HTTP usado é o **POST** e o **contentType** é **application/json**.

### POST /set_network_interlock.fcgi NEW!

*Parâmetros*

- O parâmetro será um **objeto JSON** com as três configurações do intertravamento.
- Atentar-se ao detalhe de que, para qualquer requisição, obrigatoriamente as três configurações devem ser enviadas.
- **interlock_enabled** *(int)*: Habilita a regra de intertravamento via rede neste equipamento.
- **api_bypass_enabled** *(int)*: Quando habilitado, as requisições de abertura feitas pela API ignoram a regra de intertravamento.
- **rex_bypass_enabled** *(int)*: Quando habilitado, o acionamento da botoeira (REX, request-to-exit) ignora a regra de intertravamento.

*Resposta*

- A resposta será um **objeto JSON** vazio caso a mudança de configurações seja bem-sucedida, ou com uma mensagem de erro caso contrário.

#### Exemplo de requisição

Habilita o intertravamento via rede, sem habilitar as opções de ignorar o intertravamento ao abrir a porta via botoeira ou API:

```
$.ajax({
  url: "/set_network_interlock.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    "interlock_enabled" : 1,
    "api_bypass_enabled" : 0,
    "rex_bypass_enabled" : 0
  })
});
```

Habilita a opção de ignorar o intertravamento ao abrir a porta via API:

```
$.ajax({
  url: "/set_network_interlock.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    "interlock_enabled" : 1,
    "api_bypass_enabled" : 1,
    "rex_bypass_enabled" : 0
  })
});
```

Habilita a opção de ignorar o intertravamento ao abrir a porta tanto via API quanto via botoeira:

```
$.ajax({
  url: "/set_network_interlock.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    "interlock_enabled" : 1,
    "api_bypass_enabled" : 1,
    "rex_bypass_enabled" : 1
  })
});
```

Desabilita o intertravamento via rede:

```
$.ajax({
  url: "/set_network_interlock.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    "interlock_enabled" : 0,
    "api_bypass_enabled" : 0,
    "rex_bypass_enabled" : 0
  })
});
```
