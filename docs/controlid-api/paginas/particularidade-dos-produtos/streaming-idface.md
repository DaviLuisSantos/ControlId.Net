<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/streaming-idface/ -->

# Streaming iDFace

## Streaming RTSP

O controlador de acesso iDFace é capaz de transmitir o streaming de vídeo de sua câmera através do protocolo RTSP. Uma vez habilitado o recurso, para ter acesso à transmissão é necessário configurar um cliente RTSP (como o VLC Media Player ou o Windows Media Player) para acessar o equipamento no endpoint ***/main_stream***. É importante destacar que o recurso vem, por padrão de fábrica, desabilitado e sem credenciais. Para habilitar deve-se seguir as instruções de configuração que serão mostradas na sequência.

#### URL para acesso ao serviço

Para acessar o streaming de vídeo da câmera do iDFace, caso nenhum usuário e senha estejam registrados nas credenciais RTSP, é necessário utilizar uma URL tal como a apresentada abaixo:

```
rtsp://endereço.ip.do.equipamento:porta/main_stream
```

Caso um usuário e uma senha tenham sido registrados nas credenciais RTSP, a URL de acesso seguirá o seguinte padrão:

```
rtsp://usuário:senha@endereço.ip.do.equipamento:porta/main_stream
```

## Configurações dos parâmetros do Streaming RTSP

***Atenção: todas as mudanças de configuração listadas abaixo só serão efetivadas após reinício do equipamento***

Caso o usuário deseje utilizar o streaming de vídeo de sua câmera, é preciso habilitar na GUI, interface WEB ou via API. O parâmetro que permite habilitar (1) ou desabilitar (0) é o `rtsp_enabled` do módulo `onvif`.

#### Exemplo de requisição

Esta requisição habilita a transmissão de vídeo via RTSP.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "rtsp_enabled": "1"
            }
        }
    )
});
```

### Configuração da porta

É possível configurar também a porta utilizada para a transmissão. A porta padrão utilizada é a "554". Para alterá-la deve-se utilizar o parâmetro `rtsp_port` do módulo `onvif`.

#### Exemplo de requisição

Esta requisição altera a porta da transmissão de vídeo via RTSP.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "rtsp_port": "556"
            }
        }
    )
});
```

### Configuração de credenciais

Da mesma forma, é possível registrar um nome de usuário e senha para que a transmissão só seja habilitada a partir do preenchimento do usuário e senhas corretos. Para isso deve-se utilizar os parâmetros `rtsp_username` e `rtsp_password` do módulo `onvif`. Por padrão, nenhum usuário e senha estão registrados nesses parâmetros.

#### Exemplo de requisição

Esta requisição insere um usuário e senha para ativar a transmissão de vídeo via RTSP.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "rtsp_username": "admin",
                "rtsp_password": "admin",
            }
        }
    )
});
```

### Configuração de câmera

Além disso, é possível alterar a câmera utilizada para fazer a transmissão de vídeo. A câmera padrão é a RGB, mas via requisição API é possível alterá-la para câmera infravermelha (IR). O parâmetro responsável por essa configuração é o `rtsp_rgb` do módulo `onvif`, e são duas as entradas válidas para ele: "0" (IR) ou "1" (RGB).

#### Exemplo de requisição

Esta requisição altera a câmera da transmissão de vídeo para a câmera IR.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "rtsp_rgb": "0"
            }
        }
    )
});
```

### Configuração de codec

Também é possível alterar o codec de vídeo utilizado na transmissão. O codec padrão é MJPEG, mas via requisição API é possível alterá-lo para H.264. O parâmetro responsável por essa configuração é o `rtsp_codec` do módulo `onvif`, e são duas as entradas válidas para ele: "mjpeg" (MJPEG) ou "h264" (H.264).

#### Exemplo de requisição

Esta requisição altera o codec de transmissão para H.264.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "rtsp_codec": "h264"
            }
        }
    )
});
```

## Streaming ONVIF

O controlador de acesso iDFace também é capaz de transmitir o streaming de vídeo de sua câmera através do padrão ONVIF (Open Network Video Interface Forum), reconhecido e utilizado mundialmente por diversos fabricantes de câmeras de vigilância e gravadores.

## Configurações dos parâmetros do Streaming ONVIF

***Atenção: todas as mudanças de configuração listadas abaixo só serão efetivadas após reinício do equipamento***

Primeiramente, devemos nos atentar que as transmissões seguindo padrão ONVIF são realizadas utilizando em seu *background* o protocolo RTSP, logo, lembre-se de [**configurar devidamente o RTSP**](../particularidade-dos-produtos/streaming-idface.md#configuracoes-dos-parametros-do-streaming-rtsp) e **habilitá-lo** para garantir que a transmissão ONVIF ocorra sem erros. A transmissão ONVIF só irá funcionar se o RTSP estiver **ativo** e **operando corretamente**.

Além disso, é importante saber que o ONVIF utiliza as suas credenciais padrões, ou seja, o valor de *"admin"* tanto para o ***login*** quanto para ***senha***.

Caso o usuário deseje utilizar a funcionalidade ONVIF, é preciso habilitar na GUI, interface WEB ou via API. O parâmetro que permite habilitar (1) ou desabilitar (0) é o `onvif_enabled` do módulo `onvif`.

#### Exemplo de requisição

Esta requisição habilita a transmissão de vídeo via ONVIF.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "onvif_enabled": "1"
            }
        }
    )
});
```

### Configuração da porta

É possível configurar também a porta utilizada para a transmissão. A porta padrão utilizada é a "8000". Para alterá-la deve-se utilizar o parâmetro `onvif_port` do módulo `onvif`.

#### Exemplo de requisição

Esta requisição altera a porta da transmissão de vídeo via ONVIF.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "onvif_port": "8001"
            }
        }
    )
});
```

## Parametrizações de visualização

O controlador de acesso iDFace também é capaz de transmitir o vídeo de sua câmera com resolução de 360x640, com a possibilidade de configurar o espelhamento da imagem a fim de melhorar a visualização do local.

### Configurações dos parâmetros de transmissão de vídeo

Além disso, é possível alterar a orientação da transmissão de vídeo, podendo ser original ou espelhada. O parâmetro responsável por essa configuração é o `rtsp_flipped` do módulo `onvif`, e são duas as entradas válidas para ele: "0" para original ou "1" tela espelhada.

### Marca d'água e Logo

É possível habilitar ou desabilitar de forma independente a marca d'água e o logo com os parâmetros `rtsp_watermark` e `rtsp_watermark_logo_enabled` (módulo `video_stream`) respectivamente, fazer upload de um novo logo customizado e habilitá-lo ou não com o parâmetro `rtsp_watermark_custom_logo_enabled`.
A proporção recomendada das imagens dos logos é de 5:1. A imagem deve ter, no máximo, 1MB de tamanho. Altura e largura devem ser menores que 1000 pixels.

Há duas formas de fazer o upload da imagem com o logo customizado:

- via web: o usuário seleciona um arquivo de seu computador (Configurações -> Streaming de vídeo)
- via GUI: o usuário insere um pendrive no dispositivo e seleciona o arquivo a partir de sua GUI (Menu -> Config. Facial -> Câmeras -> Streaming de vídeo)

### Exemplo de requisição

Esta requisição altera para a tela espelhada.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "onvif": {
                "rtsp_flipped": "1"
            }
        }
    )
});
```

## Logotipo do streaming

Permite gerenciar um logotipo PNG personalizado sobreposto ao stream de vídeo.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /get_streaming_logo.fcgi

Retorna o logotipo personalizado atual do streaming como imagem PNG. Retorna erro se nenhum logotipo personalizado foi definido.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Imagem PNG do logotipo de streaming.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_streaming_logo.fcgi?session=" + session,
    type: 'GET'
});
```

### POST /set_streaming_logo.fcgi

Envia um logotipo PNG personalizado para sobrepor ao stream de vídeo.

*Parâmetros*

- Imagem PNG em formato binário, enviada no corpo da requisição (`application/octet-stream`).

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_streaming_logo.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes da imagem PNG]
});
```

### POST /remove_streaming_logo.fcgi

Remove o logotipo personalizado do streaming.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/remove_streaming_logo.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Vídeo customizado

Permite fazer upload e gerenciar um vídeo MP4 customizado para ser exibido na tela do equipamento no lugar do streaming de identificação.

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /send_video.fcgi

Envia um arquivo de vídeo MP4 customizado para o equipamento em blocos. O arquivo deve ser enviado em blocos sequenciais utilizando os parâmetros de query string `current` e `total`. O tamanho máximo total do arquivo é 20 MB por sequência de blocos.

*Parâmetros*

Passados como parâmetros na query string:

- **current** *(int)*: **Obrigatório.** Índice do bloco atual sendo enviado (começando em `1`).
- **total** *(int)*: **Obrigatório.** Número total de blocos no upload.

Corpo: dados binários brutos do MP4 para o bloco atual (`application/octet-stream`).

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/send_video.fcgi?current=1&total=2&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do bloco MP4]
});
```

### POST /set_custom_video.fcgi

Configura se o vídeo customizado enviado deve estar ativo e opcionalmente atribui um nome de exibição. O vídeo enviado via `/send_video.fcgi` é convertido para o formato de reprodução do equipamento em segundo plano.

*Parâmetros*

- **custom_video_enabled** *(int)*: Obrigatório. `1` para ativar o vídeo customizado, `0` para desativá-lo.
- **video_name** *(string)*: Opcional. Nome de exibição do vídeo.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_custom_video.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        custom_video_enabled: 1,
        video_name: "boas-vindas.mp4"
    })
});
```

### POST /remove_custom_video.fcgi

Remove o vídeo customizado e reverte o equipamento ao comportamento de exibição padrão.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/remove_custom_video.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Configurações do bitrate da transmissão de vídeo

É possível alterar o bitrate máximo da transmissão de vídeo. O parâmetro responsável por essa configuração é `max_bitrate` do módulo `video_stream` e as entradas válidas são todos os números inteiros entre 600 e 6000, inclusive.

### Exemplo de requisição

Essa requisição altera o bitrate máximo da transmissão de vídeo para 1000 Kb/s.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "video_stream": {
                "max_bitrate": "1000"
            }
        }
    )
});
```
