<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/mensagens-sonoras-acesso-idface/ -->

# Mensagens sonoras de acesso iDFace

O controlador de acesso iDFace possui a capacidade de reproduzir mensagens sonoras como resposta a eventos de identificação.
O usuário pode escolher entre deixar o recurso desabilitado, utilizar as mensagens de voz padrão do dispositivo ou carregar seus próprios arquivos de áudio.

## Configuração do modo de funcionamento

Os eventos que possuem feedback sonoro, e as respectivas mensagens padrão, estão listados na tabela abaixo. Na terceira coluna da tabela, pode-se ver o nome do parâmetro que deve ser utilizado para configurar a mensagem de cada evento e, na quarta coluna, são apresentadas as entradas válidas.

Obs: Todos os parâmetros de configuração fazem parte do módulo `buzzer`.

| Eventos | Mensagem sonora padrão | Parâmetros | Entradas válidas |
| --- | --- | --- | --- |
| **Não identificado** | "Usuário não identificado" | audio_message_not_identified | disabled, default ou custom |
| **Autorizado** | "Acesso liberado" | audio_message_authorized | disabled, default ou custom |
| **Não autorizado** | "Acesso negado" | audio_message_not_authorized | disabled, default ou custom |
| **Use máscara** | "Por favor, use máscara" | audio_message_use_mask | disabled, default ou custom |

Quando inserida a entrada ***disabled***, a resposta sonora para aquele evento estará desativada. Se inserida a entrada ***default***, a resposta sonora será a mensagem sonora padrão do dispositivo. E se for inserido ***custom***, a resposta sonora será o áudio carregado pelo usuário.

### Exemplo de requisição

Esta requisição ativa as respostas sonoras padrões de identificação.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "buzzer": {
                "audio_message_not_identified": "default",
                "audio_message_authorized": "default",
                "audio_message_not_authorized": "default",
                "audio_message_use_mask": "default"
            }
        }
    )
});
```

### Exemplo de requisição

Esta requisição desabilita as repostas sonoras de identificação.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "buzzer": {
                "audio_message_not_identified": "disabled",
                "audio_message_authorized": "disabled",
                "audio_message_not_authorized": "disabled",
                "audio_message_use_mask": "disabled"
            }
        }
    )
});
```

## Alterar o volume de reprodução do áudio

Além de poder escolher o áudio, é possível escolher o volume em que a resposta sonora será emitida. O parâmetro responsável por ajustar o volume é o `audio_message_volume_gain`.
Existem 3 níveis de volume possíveis: normal, médio e alto. Normal é o volume padrão de fábrica e ele corresponde ao valor de entrada 1. Médio corresponde ao valor 2 e alto corresponde a 3. Um exemplo de requisição para ajustar o volume está mostrado abaixo:

### Exemplo de requisição

Esta requisição ajusta o volume das mensagens sonoras para médio.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "buzzer": {
                "audio_message_volume_gain": "2"
            }
        }
    )
});
```

## Carregamento da mensagem sonora customizada

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Conforme mencionado acima, é possível utilizar áudios customizados nas mensagens sonoras de identificação. Para isso, é preciso carregar o áudio desejado no dispositivo. O áudio pode ser carregado através da interface WEB ou através do uso da API.

É importante destacar, que alguns cuidados devem ser tomados ao carregar o áudio. Primeiramente, o limite de tamanho de arquivo de áudio suportado é 5MB. Além disso, no envio do arquivo via API, cada requisição é limitada a transportar no máximo 2MB de dados e, portanto, o envio deve ser feito de forma fragmentada. O usuário deve separar seu arquivo de áudio em blocos (2MB máx) e enviar sequencialmente cada parte, em requisições separadas. Para fazer isso, ele deve trabalhar com os parâmetros **current** e **total**, explicados abaixo:

### POST /set_audio_access_message.fcgi

*Parâmetros*

- **event** *(string)* : seleciona para qual dos eventos de identificação será utilizado o áudio a ser enviado. Abaixo estão as strings de entradas possíveis (**obrigatório**):
  > - **not_identified**: seleciona o evento 'Não identificado'.
  > - **authorized**: seleciona o evento 'Autorizado'.
  > - **not_authorized** : seleciona o evento 'Não autorizado'.
  > - **use_mask**: seleciona o evento 'Use máscara'.
- **current** *(int)* : corresponde ao valor da parte atual do áudio que esta sendo enviada. Exemplo: em um envio de um arquivo de áudio divido em 2 partes, o primeiro envio deve ter o valor current igual a 1 e o segundo envio igual a 2. (**obrigatório**).
- **total** *(int)* : corresponde ao número total de partes que serão enviadas. Exemplo: no caso do exemplo de um envio de um arquivo dividido em 2 partes, o número total de partes é igual a 2, então o total deve ser igual a 2 (**obrigatório**).

### Exemplo de requisição

Esta requisição carrega o arquivo de áudio no equipamento. A requisição corresponde ao primeiro envio de um arquivo dividido em 2 partes.

```
$.ajax({
    url: "/set_audio_access_message.fcgi?event=authorized&current=1&total=2&session=" + session 
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do áudio enviado]
});
```

## Download da mensagem sonora customizada

Uma vez carregada a mensagem sonora customizada, é possível fazer o download da mensagem via interface WEB do embarcado ou via API. A requisição para realizar isso esta mostrada abaixo:

### POST /get_audio_access_message.fcgi

*Parâmetros*

- **event** *(string)* : seleciona o evento de identificação cujo áudio personalizado se deseja obter. Abaixo estão as strings de entradas possíveis (**obrigatório**):
  > - **not_identified**: seleciona o evento 'Não identificado'.
  > - **authorized**: seleciona o evento 'Autorizado'.
  > - **not_authorized** : seleciona o evento 'Não autorizado'.
  > - **use_mask**: seleciona o evento 'Use máscara'.

*Resposta*

- Caso exista, será retornado o arquivo de áudio.

### Exemplo de requisição

```
$.ajax({
    url: "/get_audio_access_message.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "event": "authorized"
    })
});
```

## Verificação de existência de áudio customizado no dispositivo

Podemos verificar se já existe um áudio customizado carregado no equipamento. Para isso, utiliza-se a requisição abaixo:

### POST /has_audio_access_messages.fcgi

*Parâmetros*

- **Sem parâmetros de entrada**

*Resposta*

- **not_identified** *(bool)*: Retorna **true** caso já exista um arquivo de áudio customizado para o evento "Não identificado". E retorna **false** caso contrário.
- **authorized** *(bool)*: Retorna **true** caso já exista um arquivo de áudio customizado para o evento "Autorizado". E retorna **false** caso contrário.
- **not_authorized** *(bool)*: Retorna **true** caso já exista um arquivo de áudio customizado para o evento "Não autorizado". E retorna **false** caso contrário.
- **use_mask** *(bool)*: Retorna **true** caso já exista um arquivo de áudio customizado para o evento "Use máscara". E retorna **false** caso contrário.

### Exemplo de requisição

```
$.ajax({
    url: "//has_audio_access_messages.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
