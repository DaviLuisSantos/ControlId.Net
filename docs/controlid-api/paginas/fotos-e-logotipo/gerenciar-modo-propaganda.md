<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/fotos-e-logotipo/gerenciar-modo-propaganda/ -->

# Gerenciar Modo Propaganda

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Para configurar o modo propaganda via API, é necessário haver um vídeo carregado no dispositivo para habilitá-lo. Caso não haja, é possível carregar um novo video utilizando o comando `send_video.fcgi`. Após isso, o usuário pode habilitar ou desabilitar o vídeo carregado por meio do comando `set_custom_video.fcgi`.

As regras para envio de vídeo são:

> - **Formato mp4**
> - **Orientação do vídeo na vertical** - preferencialmente na dimensão 800x1280.
> - **Arquivos com menos de 20MB**

## Carregar vídeo remotamente

Para carregar um novo vídeo no dispositivo, o usuário pode utilizar a API, ou optar por carregar via Web ou Pendrive. No entanto, por API, o tamanho máximo de cada pacote de requisição é 2MB de dados, dessa forma o envio deve ser feito de forma fragmentada. O usuário deve separar seu vídeo em blocos (2MB máx) e enviar sequencialmente cada parte, em requisições separadas. Para fazer isso, ele deve trabalhar com os parâmetros **current** e **total**, explicados abaixo:

### POST /send_video.fcgi

*Parâmetros*

- **current** *(int)* : corresponde ao valor da parte atual do vídeo que esta sendo enviada. Exemplo: em um envio de um arquivo de vídeo divido em 2 partes, o primeiro envio deve ter o valor current igual a 1 e o segundo envio igual a 2. (**obrigatório**).
- **total** *(int)* : corresponde ao número total de partes que serão enviadas. Exemplo: no caso do exemplo de um envio de um arquivo dividido em 2 partes, o número total de partes é igual a 2, então o total deve ser igual a 2 (**obrigatório**).

### Exemplo de requisição

Esta requisição carrega o arquivo de vídeo no equipamento. A requisição corresponde ao primeiro envio de um arquivo dividido em 2 partes.

```
$.ajax({
    url: "/send_video.fcgi?current=1&total=2&session=" + session 
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do vídeo enviado]
});
```

*Resposta*

- O retorno da requisição é um objeto json vazio.

## Habilitar ou desabilitar modo propaganda

Após enviar o vídeo, o usuário pode optar por habilitar ou desabilitar o modo propaganda via API.

### POST /set_custom_video.fcgi

*Parâmetros*

- **custom_video_enabled** *(int)* : Habilita (`1`) ou desabilita (`0`) o modo propaganda. Deve ser enviado como número JSON: um valor em string (por exemplo, `"1"`) é rejeitado com o erro `Invalid member 'custom_video_enabled' (int expected, got string)`. Em caso de habilitar modo sem nenhum vídeo carregado é retornado erro. (**obrigatório**)

*Resposta*

- O retorno da requisição é um objeto json vazio.

### Exemplo de requisição

```
$.ajax({
    url: "/set_custom_video.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "custom_video_enabled": 1
    })
});
```

## Esconder itens na tela inicial

Caso o usuário deseje ocultar os itens da tela inicial para exibir somente o vídeo carregado, é possível configurar via API.
Para exibir os itens ocultos basta clicar na tela uma vez.

Os parâmetros são:

*Parâmetros*

- **hide_control_bar** *(booleano)* : Esconde a barra de controle
- **hide_clock** *(booleano)* : Esconde o relógio
- **hide_menu_button** *(booleano)* : Esconde o botão do menu
- **hide_logo** *(booleano)* : Esconde o logotipo
- **hide_device_name** *(booleano)* : Esconde o nome do dispositivo em modo online
- **hide_call_buttons** *(booleano)* : Esconde os botões de ligação

*Resposta*

- O retorno da requisição é um objeto json vazio.

### Exemplo de requisição

Oculta todos os itens da tela inicial:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "video_player": {
                "custom_video_enabled": "1",
                "hide_clock": "1",
                "hide_menu_button": "1",
                "hide_logo": "1",
                "hide_device_name": "1",
                "hide_call_buttons": "1"
            }
        }
    )
});
```

## Remover vídeo customizado

Caso o usuário deseje remover o vídeo carregado no dispositivo, utiliza-se o seguinte comando:

### POST /remove_custom_video.fcgi

*Resposta*

- O retorno da requisição é um objeto json vazio.

### Exemplo de requisição

```
$.ajax({
    url: "/remove_custom_video.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
