<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/modos-de-operacao/configurar-modo-online/ -->

# Configurar modo online

No modo online, no momento da autorização, o equipamento consulta o seu servidor/sistema para saber o que fazer: permitir o acesso do usuário ou não. Uma lista com todos os modos de operação pode ser acessada em: [Modos de Operação](../modos-de-operacao/introducao-aos-modos-de-operacao.md).

O seguinte passo a passo explica como habilitar e configurar o modo online (modo Pro) nos equipamentos de controle de acesso.

## Passo 1 - Criar um objeto **device**:

Esse objeto será a representação do seu servidor/sistema no equipamento.

#### Exemplo de requisição

```
$.ajax({
  url: "/create_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "devices",
    values: [{name: 'Meu servidor 1', ip: 'http://meuservidor.com.br/api', public_key: ''}]
  })
});
```

#### Exemplo de resposta

```
{"ids":[473359]}
```

**Importante:** Guarde esse *id*, será necessário utilizar esse valor no próximo passo.

**Observações:**

1. O atributo *ip* pode ser qualquer endereço, por exemplo:

   - 192.168.110.200:80/api2
   - http://192.168.110.200:80
   - 192.168.200.200:8080
   - http://meuservidor.com.br/servico
2. Não é necessário criar um objeto "devices" toda vez que for ativar o modo online (Pro) no controle de acesso. Esse objeto só precisa ser criado uma única vez na memória do equipamento. Depois de criado, basta realizar os passos 2 e 3, que são responsáveis por atribuir o endereço do servidor que será usado para recebimento dos eventos e ativar o modo online no dispositivo respectivamente.

## Passo 2 - Criar referência do seu servidor/sistema no controle de acesso:

**Observação:** O valor do *server_id* a ser passado é o *id* do servidor/sistema criado no [Passo 1](../modos-de-operacao/configurar-modo-online.md#passo-1-criar-um-objeto-device).

#### Exemplo de requisição

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    online_client: {
        server_id: "473359"
    }
  })
});
```

## Passo 3 - Ativar o modo online (Pro)

#### Exemplo de requisição

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {
        online: "1",
        local_identification: "1",
        ihm_enterprise_mode: "0"
    },
    online_client: {
      extract_template: "0",
      max_request_attempts: "3"
    }
  })
});
```

**Observação:** O parâmetro *ihm_enterprise_mode* não está disponível em **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**. Nesses equipamentos, remova a linha `ihm_enterprise_mode: "0"` da requisição acima; caso contrário, a requisição falha.

## Passo 4 - Verificar validade da sessão

Após esses passos, o equipamento irá se comunicar com o seu servidor/sistema. Para testar a comunicação, você pode implementar o endpoint [/session_is_valid.fcgi](../gerenciamento-secao/verificar-validade-da-sessao.md#verificar-validade-da-sessao).

Lembrando que o endpoint será concatenado com o valor de *ip* inserido no [Passo 1](../modos-de-operacao/configurar-modo-online.md#passo-1-criar-um-objeto-device), por exemplo:

- http://meuservidor.com/api/session_is_valid.fcgi
- 192.168.115.200/session_is_valid.fcgi
- 192.168.110.200:8080/session_is_valid.fcgi

**Observação:** Caso não seja respondido o [*device_is_alive.fcgi*](../modos-de-operacao/eventos-de-identificacao-online.md#post-device_is_alivefcgi) o equipamento continuará testando a comunicação por meio desta requisição, mas não conseguirá se comunicar com o servidor e não enviará outras requisições.

Após a realização das operações apresentadas acima, o controlador de acesso estará se comunicando com o seu servidor/sistema. Quando um usuário se identificar no dispositivo, ele enviará um evento de identificação para o seu servidor/sistema com as informações de acesso do usuário. Com isso, cabe ao seu servidor/sistema tratar esse evento e retornar com uma mensagem para o controle de acesso.
