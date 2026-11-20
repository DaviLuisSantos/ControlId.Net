<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/interfonia-sip-idface/ -->

# Interfonia SIP iDFace

O controlador de acesso iDFace conta com um sistema de interfonia VoIP baseado no protocolo SIP (Session Initiation Protocol). Uma vez configurado, o controlador registra o usuário no servidor indicado e, assim, permite realizar e receber chamadas de voz.

Para ter acesso à funcionalidade, é necessário obter uma **licença PRO** para o iDFace.

Abaixo, estão apresentados alguns dos parâmetros configuráveis da interfonia SIP no iDFace. Eles podem ser determinadas através das interfaces gráfica e Web do equipamento ou através da API.

## Conexão SIP

Para que a conexão ao servidor SIP seja estabelecida, é preciso garantir que a interfonia esteja habilitada e fornecer os dados de acesso ao servidor SIP e da conta de usuário registrada nele que se deseja utilizar. Isso pode ser feito pelo menu **Configurações SIP**.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **enabled** | *string* | Habilita a interfonia SIP. |
| **server_ip** | *string* | Endereço do servidor SIP, podendo ser passado como URL ou como endereço IP. |
| **server_port** | *int* | Porta de acesso ao servidor. |
| **server_outbound_port** | *int* | Porta RTP inicial do SIP. Se configurada em 0, utiliza uma porta qualquer disponível. |
| **server_outbound_port_range** | *int* | Range de portas RTP do SIP. O SIP somente utilizará portas de rede disponíveis dentro do range especificado. |
| **numeric_branch_enabled** | *string* | Habilita ou não leitura do Ramal do usuário como um valor numérico. Valores: "0" para leitura em alfanumérico e "1" para numérico. |
| **branch** | *string* | Ramal do usuário registrado no servidor SIP. |
| **login** | *string* | Login do usuário registrado no servidor SIP. |
| **password** | *string* | Senha para login no servidor SIP. Para utilizar Autenticação Digest, a senha deve ter no máximo 16 caracteres. |
| **peer_to_peer_enabled** | *string* | Ativa a comunicação ponto-a-ponto na chamada SIP, com opções possíveis sendo **"1"** para ativado e **"0"** para desativado. |
| **audio_codecs_enabled** | *string* | Lista dos codecs de áudio habilitados (ativos) no dispositivo, os codecs devem ser separados por ponto e vírgula: (PCMU/8000/1;PCMA/8000/1;G722/16000/1;GSM/8000/1;iLBC/8000/1;speex/8000/1;speex/16000/1;speex/32000/1). |

### Exemplo de requisição

Esta requisição configura os parâmetros de conexão SIP.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "enabled": "1",
                "server_ip": "meu_servidor",
                "server_port": "5060",
                "server_outbound_port": "10000",
                "server_outbound_port_range": "1000",
                "numeric_branch_enabled": "1",
                "branch": "987",
                "login": "987",
                "password": "123456",
                "peer_to_peer_enabled": "0",
                "audio_codecs_enabled": "PCMU/8000/1;PCMA/8000/1"
            }
        }
    )
});
```

## Configurações Gerais de Chamada

### Configuração de períodos da chamada

Para uma chamada, podemos também configurar pela API as configurações de registro, tempo máximo de chamada ou ainda o timeout para enviar um keep-alive ao servidor, de acordo com os parâmetros a seguir:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **reg_status_query_period** | *int* | Período, em segundos, para a requisição de status do registro. |
| **server_retry_interval** | *int* | Período de envio do keep-alive, em segundos. |
| **max_call_time** | *int* | Tempo máximo de duração da chamada, em segundos. |
| **push_button_debounce** | *int* | Tempo de debounce da botoeira para evitar chamadas indesejadas, em milissegundos. Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |

### Exemplo de requisição

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "reg_status_query_period": "60",
                "server_retry_interval": "5",
                "max_call_time": "300",
                "push_button_debounce": "50"
            }
        }
    )
});
```

**Observação**: em **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**, remova o parâmetro `push_button_debounce` desta requisição, pois ele não existe nesses equipamentos.

### Auto atendimento

A ativação do atendimento automático permite que o equipamento aceite automaticamente as chamadas recebidas. É possível configurar um intervalo, em segundos, entre o recebimento da chamada e o seu atendimento pelo equipamento.

O parâmetro `auto_answer_enabled` do módulo `pjsip`, determina se o atendimento automático será habilitado ou não. Uma vez habilitado, o tempo para atendimento automático é configurado pelo parâmetro `auto_answer_delay`, também do módulo `pjsip`.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **auto_answer_enabled** | *string* | Habilita o auto atendimento das chamadas SIP. Valores: "0" para auto atendimento desligado e "1" para auto atendimento ligado. |
| **auto_answer_delay** | *string* | Tempo de espera em segundos para realização do auto atendimento. |

### Exemplo de requisição

Requisição para configurar atendimento automático habilitado em 5 segundos de chamada.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "auto_answer_enabled": "1",
                "auto_answer_delay": "5"
            }
        }
    }
    )
});
```

### Modo de discagem

Por padrão, quando a interfonia está habilitada e devidamente configurada, um botão é mostrado na tela principal do equipamento, pelo qual é possível iniciar uma nova ligação. A visibilidade desse botão pode ser configurada de acordo com as necessidades do usuário pelo parâmetro `auto_call_button_enabled`.
**Importante:** Esteja ciente de que quando a visibilidade do botão de discagem está desabilitada na tela do dispositivo, é necessário o uso da botoeira de discagem para que uma chamada SIP seja executada. A botoeira de discagem pode ser configurada de acordo com as necessidades do usuário pelo parâmetro `rex_enabled`.

Outra configuração importante é o modo de discagem quando o botão é acionado,que pode ser definido pelo parâmetro `dialing_display_mode`. Existem 3 modos de discagem possíveis e o usuário pode personalizar conforme desejado. A tabela abaixo mostra o parâmetro que deve ser utilizado para a personalização, as entradas possíveis e a descrição dos modos de discagem. Todos os parâmetros desta seção fazem parte do módulo `pjsip`.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **auto_call_button_enabled** | *string* | Habilita ou não a visibilidade do botão de ligação da tela principal do equipamento. Valores: "0" para desabilitar o botão e "1" para habilitá-lo. |
| **rex_enabled** | *string* | Habilita ou não a funcionalidade da ligação por botoeira de discagem. Valores: "0" para desabilitar a botoeira e "1" para habilitá-la. |
| **dialing_display_mode** | *string* | Valores possíveis são: **"0"**, para Discagem automática. Quando o botão de discagem é acionado, um número é chamado diretamente. **"1"**, para Discagem a partir da lista de contatos. Quando o botão de discagem é acionado, a lista de contatos é mostrada e a chamada é efetuada para o contato selecionado. **"2"**, para Discagem a partir do teclado numérico ou a partir da lista de contatos: quando o botão de discagem é acionado, é mostrado uma nova tela com um teclado numérico e um ícone para ir até à lista de contatos. Para efetuar a ligação o usuário pode escolher entre digitar um número no teclado ou buscar um contato na lista. |

Ao selecionar o modo de discagem automática é necessário definir o número para o qual a ligação será efetuada. Isto é feito através do parâmetro `auto_call_target`. Além disso, pode-se definir um nome relacionado a este número. Ele é definido através do parâmetro `custom_identifier_auto_call`. Esse nome será mostrado no display do iDFace ao efetuar ou receber ligações. Caso nenhum nome seja preenchido e não houver nenhum contato salvo vinculado a este número, o display mostrará apenas o número digitado.

Obs: o parâmetro `auto_call_enabled`, que era utilizado para configurar a discagem automática, não está sendo mais utilizado. A configuração da discagem automática deve ser feita exclusivamente pelo `dialing_display_mode`.

### Exemplo de requisição

Requisição para habilitar o botão de discagem, habilitar a discagem automática para o número 456, definir o nome do número como Portaria e habilitar botoeira de discagem.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "auto_call_button_enabled": "1",
                "rex_enabled": "1",
                "auto_call_target": "456",
                "custom_identifier_auto_call": "Portaria",
                "dialing_display_mode": "0"
            }
        }
    }
    )
});
```

### Configuração do SIP com vídeo

Podemos também configurar uma chamada para ser realizada com transmissão de vídeo (a depender também da configuração do cliente do outro lado e seu suporte a video h264). Para fazê-lo devemos configurar o dispositivo através do parâmetro a seguir:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **video_enabled** | *int* | Define a configuração de SIP com vídeo, sendo **"1"** para ativado e **"0"** para desativado. |

É importante notar que, após a configuração do dispositivo no modo SIP com vídeo, é necessário reiniciar o equipamento para garantir que o `video_stream` está devidamente configurado.

### Exemplo de requisição

Para configurar a chamada no modo SIP com vídeo, podemos enviar uma requisição como a seguinte:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "video_enabled": "1"
            }
        }
    }
    )
});
```

## Configurações de Som de Chamada Personalizado

### Som de Chamada Personalizado

É possível habilitar um som de chamada personalizado para o aparelho. Dessa forma, quando uma requisição de chamada for realizada pelo equipamento, enquanto a resposta não for recebida, o áudio será executado. **Caso opte-se por ativar essa configuração, deve-se anteriormente realizar o *upload* do áudio customizado** através do *endpoint* [`set_pjsip_audio_message`](../particularidade-dos-produtos/interfonia-sip-idface.md#post-set_pjsip_audio_messagefcgi). Ao habilitar essa configuração, **é necessário realizar o reinício do equipamento** para que as alterações entrem em vigor.

Além disso, é possível configurar o volume do som de chamada personalizado. O volume é configurado através de três possíveis níveis de ganho, sendo **"1"** para o volume original, **"2"** para um ganho de duas vezes o volume original e **"3"** para um ganho de três vezes.

A habilitação do som de chamada personalizado é feita por meio do parâmetro `pjsip_custom_audio_enabled`, do módulo `pjsip`, e a configuração do nível de volume do som de chamada personalizado é feita pelo parâmetro `custom_audio_volume_gain`, também do módulo `pjsip`.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **pjsip_custom_audio_enabled** | *string* | Habilita o uso do som de chamada personalizado. Valores: "0" para áudio personalizado desabilitado e "1" para áudio personalizado habilitado. |
| **custom_audio_volume_gain** | *string* | Realiza o controle do volume do som de chamada personalizado através de seu ganho. Valores: "1", "2" e "3". |

#### Exemplo de requisição

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "pjsip_custom_audio_enabled": "1",
                "custom_audio_volume_gain": "1"
            }
        }
    )
});
```

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /set_pjsip_audio_message.fcgi

Permite realizar o *upload* do áudio que será utilizado na realização de chamadas. O áudio enviado deve ser um arquivo binário, em formato ***.wav***, e ter tamanho inferior à 5 MB e, por limitações do protocolo de rede, deve ser enviado em blocos de no máximo 2 MB. Os blocos devem ser enviados de forma sequencial, sendo especificados os parâmetros **current** e **total** para tanto.

*Parâmetros*

- **current** *(int)*: Bloco atual do arquivo de som.
- **total** *(int)*: Número total de blocos do arquivo de som.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_pjsip_audio_message.fcgi?session=" + session + "&current=" + current + "&total=" + total,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: data_sound
    )
});
```

### POST /get_pjsip_audio_message.fcgi

Permite realizar o *download* do arquivo de áudio personalizado utilizado na realização de chamadas.

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo de áudio personalizado em formato binário, presente no corpo da resposta, se existir.  
  (Content-Type: audio/wav)

#### Exemplo de requisição

```
$.ajax({
    url: "/get_pjsip_audio_message.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

### POST /has_pjsip_audio_message.fcgi

Permite verificar se existe algum arquivo de áudio personalizado no aparelho.

- Esta chamada não possui parâmetros.

*Resposta*

- **file_exists** *(bool)*: Retorna **true** caso já exista um arquivo de áudio personalizado no aparelho e **false** caso contrário.

#### Exemplo de requisição

```
$.ajax({
    url: "/has_pjsip_audio_message.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Configurações de Volume

Ainda pela interface de configuração do interfone é possível determinar os **volumes** de captação do **microfone** e de saída do **alto-falante** do equipamento. Ambos os volumes podem ser configurados para valores entre 1 e 10.

### Exemplo de requisição

Requisição de configuração dos volumes de microfone e alto-falante.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "mic_volume": "5",
                "speaker_volume": "7"
            }
        }
    )
});
```

## Realizar Chamadas via API

Após o SIP estar devidamente configurado, é possível realizar chamadas no aparelho via API. As chamadas podem ser controladas a partir de três *endpoints* que serão explicados a seguir.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /make_sip_call.fcgi

Responsável por iniciar uma chamada. A chamada somente será realizada se o aparelho estiver em sua **tela inicial** ou em ***streaming* de identificação**.

*Parâmetros*

- **target** *(string)*: Ramal que receberá a ligação.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/make_sip_call.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "target": "503"
        }
    )
});
```

### POST /finalize_sip_call.fcgi

Responsável por finalizar uma chamada em andamento.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/finalize_sip_call.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

### POST /get_sip_status.fcgi

Retorna o ***status*** atual do SIP, incluindo o código de status e se há chamada em andamento.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **status** *(int)*: Indica código de status da chamada. Os códigos são consistentes com a definição padrão do protocolo SIP. Alguns dos possíveis retornos são:
  - -1 : "Desabilitado"
  - 0/100 : "Conectando"
  - 200 : "Conectado"
  - 401/403 : "Falha na autenticação"
  - 408 : "Falha ao conectar com o servidor"
  - 503 : "Falha na conexão de rede"
- **in_call** *(bool)*: Indica se há uma chamada em andamento. Retorna **true** se houver uma chamada ativa e **false** caso contrário.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_sip_status.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Liberação de acesso via interfonia

**Aviso: A liberação de acesso via interfonia do iDFace faz uso de códigos enviados por DTMF segundo a [RFC 2833](https://www.rfc-editor.org/rfc/rfc2833).**

É possível realizar a liberação de acesso através de um código discado através do interfone. Para isso, é preciso primeiro habilitar o recurso e registrar um novo código. Para habilitar/desabilitar deve-se utilizar o parâmetro `open_door_enabled` do módulo `pjsip`. Ele aceita como entradas válidas os valores "0" (desabilitado) ou "1" (habilitado).

Para registrar o código de liberação, utiliza-se o parâmetro `open_door_command`, também do módulo `pjsip`. Ele aceita como entrada caracteres numéricos e os seguintes caracteres especiais: '+', '\*' e '#'.

### Exemplo de requisição

Requisição para habilitar a liberação de acesso por interfonia com a discagem do código 12345.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "open_door_enabled": "1",
                "open_door_command": "12345"
            }
        }
    )
});
```

### Código Wiegand

É possível usar os parâmetros `open_door_wiegand_code_en` e `open_door_wiegand_code` para habilitar e configurar, respectivamente, um código wiegand, independente do código de liberação discado, que será enviado durante a ligação para abrir uma porta.

### Exemplo de requisição

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "open_door_wiegand_code_en" : "1",
                "open_door_wiegand_code": "0849"
            }
        }
    )
});
```

## Habilitação de identificação durante interfonia

**Aviso: A funcionalidade de identificação durante interfonia do iDFace está disponível a partir da versão de firmware 6.13.1.**

É possível realizar a identificação durante a chamada SIP. Para isso, é preciso primeiro habilitar o recurso. Para habilitar/desabilitar deve-se utilizar o parâmetro `facial_id_during_call_enabled` do módulo `pjsip`. Ele aceita como entradas válidas os valores "0" (desabilitado) ou "1" (habilitado).

### Exemplo de requisição

Requisição para habilitar a identificação durante interfonia.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "facial_id_during_call_enabled": "1"
            }
        }
    )
});
```

## Iniciar chamada de conferência SIP

Aciona uma chamada de conferência SIP a partir do equipamento. Opcionalmente especifica o ramal SIP de destino.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /make_sip_conference.fcgi

*Parâmetros*

- **target** *(string)*: Opcional. Ramal SIP de destino. Se omitido ou vazio, utiliza o destino padrão configurado.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/make_sip_conference.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        target: "sip:ramal@192.168.1.100"
    })
});
```

## Ícone personalizado de interfonia

Permite gerenciar um ícone PNG personalizado exibido na tela do equipamento durante chamadas SIP.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /get_phone_icon.fcgi

Retorna o ícone personalizado atual da interfonia como imagem PNG. Retorna erro se nenhum ícone personalizado foi definido.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Imagem PNG do ícone personalizado.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_phone_icon.fcgi?session=" + session,
    type: 'GET'
});
```

### POST /set_phone_icon.fcgi

Envia um ícone PNG personalizado para a tela de interfonia.

*Parâmetros*

- Imagem PNG em formato binário, enviada no corpo da requisição (`application/octet-stream`).

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_phone_icon.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes da imagem PNG]
});
```

### POST /remove_phone_icon.fcgi

Remove o ícone personalizado de interfonia, restaurando o padrão.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/remove_phone_icon.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Lista de contatos para interfonia

Configura o comportamento da lista de contatos.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **search_contacts_by_info** | *int* | Quando habilitado, a busca na lista de contatos é baseada no campo `info` do objeto contacts; caso contrário, é usado o campo `name`. Valores: "0" para desabilitado e "1" para habilitado. |
| **show_contact_mode** | *int* | Permite configurar que informações são mostradas na tela de contatos. Se o valor for 0, ambos os campos `name` e `info` são mostrados. Se o valor for 1, apenas o campo `name` é mostrado. Se o valor for 2, apenas o campo `info` é mostrado. |
| **main_contact_group_id** | *int* | Indica o ID do grupo de contatos que deve ser mostrado no modo quiosque quando a ação do botão é `mainContactGroup`. |

### Exemplo de requisição

Essa requisição configura os parâmetros referentes à lista de contatos.

```js
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "search_contacts_by_info": "0",
                "show_contact_mode": "0",
                "main_contact_group_id": "5"
            }
        }
    ) 
});
```

## Configuração de Ganho e Filtros de Eco/Ruído

É possível adequar a chamada ao ambiente, alterando o ganho do microfone e do alto-falante da interfonia e as configurações do filtro usado para supressão de eco e ruído. Todos os parâmetros desta seção fazem parte do módulo `pjsip`. Disponível a partir das versões 6.25.0, 7.9.10 e 8.7.0 do firmware.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **tx_level_gain** | *float* | Define o ganho do alto-falante. Pode ser configurado com valores entre **0** e **5**. |
| **rx_level_gain** | *float* | Define o ganho do microfone. Pode ser configurado com valores entre **0** e **5**. |
| **tail_ms** | *int* | Comprimento do filtro de eco, em milissegundos. |
| **ec_algorithm** | *string* | Algoritmo usado no filtro de eco. Valores: "webrtc", "webrtc_aec3", "speex" e "simple". |
| **ec_aggressiveness** | *string* | Agressividade do filtro de eco. Valores: "conservative", "moderate" e "aggressive". |
| **noise_suppression_enabled** | *int* | Habilita a supressão de ruído. Valores: "0" para desabilitado e "1" para habilitado. |

### Exemplo de requisição

Esta requisição configura os parâmetros de ganho e do filtro de eco e habilita a supressão de ruído.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "pjsip": {
                "tx_level_gain": "1.2",
                "rx_level_gain": "2.4",
                "tail_ms": "20",
                "ec_algorithm": "webrtc",
                "ec_aggressiveness": "aggressive",
                "noise_suppression_enabled": "1"
            }
        }
    )
});
```
