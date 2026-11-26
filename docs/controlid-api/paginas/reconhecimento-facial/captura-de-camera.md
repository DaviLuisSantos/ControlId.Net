<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/captura-de-camera/ -->

# Captura de câmera via API

A função descrita abaixo deve ser usada para realizar captura de imagens nos terminais de controle de acesso da Control iD com **reconhecimento facial**.

A captura de câmera obtém a imagem num ângulo de abertura maior do que o exibido no display do equipamento. O método HTTP usado para o envio dos dados é o **POST**. O retorno é a imagem capturada, conforme os parâmetros **frame_type**, **camera** e **format_type** da requisição.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /save_screenshot.fcgi NEW!

*Parâmetros*

- **frame_type** *(string)*: Origem da captura. Valores: *"camera"* (padrão, a imagem da câmera), *"screen"* (a tela do equipamento) ou *"display"* (o buffer de exibição).
- **camera** *(string)*: Define se a captura da câmera é feita no formato RGB (*"rgb"*) ou infravermelho (*"ir"*).
- **format_type** *(string)*: Formato de saída. Valores: *"png"* (padrão) retorna a imagem binária, ou *"base64"* retorna um objeto JSON com a imagem no campo **image_base64**.

*Resposta*

- Com **format_type** *"png"* (padrão): imagem capturada em formato **PNG**.
- Com **format_type** *"base64"*: objeto JSON com a imagem codificada em Base64 no campo **image_base64**.

#### Exemplo de requisição

```
$.ajax({
    url: "/save_screenshot.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "frame_type": "camera",
        "camera": "rgb"
    })
});
```

## Obter último snapshot

Retorna a última foto capturada pela câmera do equipamento, codificada em Base64. A imagem vem do snapshot mais recente armazenado no arquivo temporário do equipamento — não aciona uma nova captura.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /get_last_snapshot.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **image_base64** *(string)*: Última imagem capturada codificada em Base64.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_last_snapshot.fcgi?session=" + session,
    type: 'GET'
});
```

#### Exemplo de resposta

```
{
    "image_base64": "/9j/4AAQSkZJRgABAQEASABIAAD..."
}
```
