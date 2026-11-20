<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/fotos-e-logotipo/gerenciar-slideshow/ -->

# Slideshow

Para utilizar o Slideshow é possível configurar o Slideshow via interface WEB, pela interface do dispositivo ou via API. Para o caso deste último, deve-se usar os endpoints a seguir.

## Configurações do Slideshow

Configurações gerais do Slideshow

*Parâmetros*

- **slide_show_enabled** (*int*): Habilita / desabilita o slideshow. Valores: "0" para desabilitado e "1" para habilitado.
- **slide_show_interval** (*int*): Duração de exibição de cada imagem (em milissegundos).

### Exemplo de requisição

Essa requisição configura os parâmetros do Slideshow.

```js
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST', 
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "video_player": {
                "slide_show_enabled": "1",
                "slide_show_interval": "5000"
            }
        }
    )
});
```

## Listar imagens do Slideshow

Para saber quais são as imagens carregadas e poder gerenciá-las pode-se usar o seguinte comando:

### GET /get_slideshow_images_list.fcgi

Obtém uma lista de imagens para a apresentação de slides.

*Parâmetros*

- Essa requisição não possui parâmetros.

*Resposta*

- ***images*** (*array*): Um array com objetos contendo o `filename` de cada imagem.
- ***max_files*** (*int*): Indica o número máximo de imagens.

#### Exemplo de requisição

```js
$.ajax({
    url: "/get_slideshow_images_list.fcgi?session=" + session,
    type: 'GET'
});
```

*Exemplo de resposta*

```json
{
    "images": ["myimage"],
    "max_files": 10 
}
```

## Obter o arquivo de uma imagem do Slideshow

Caso o usuário deseje obter as imagens usadas no Slideshow, pode-se usar o seguinte comando:

### GET /get_slideshow_image.fcgi

Obtém o arquivo da imagem selecionada.

*Parâmetros*

- ***filename*** (*string*): Nome do arquivo.

*Resposta*

- O arquivo da imagem.

#### Exemplo de requisição

```js
$.ajax({
    url: "/get_slideshow_image.fcgi?filename=myimage&session=" + session,
    type: 'GET'
});
```

## Carregar imagens para o Slideshow

Caso deseje, o usuário pode enviar imagens para o Slideshow pela API. Essas imagens devem estar no formato JPEG/JPG. Para isso, pode-se usar o seguinte comando:

### POST /send_slideshow_images.fcgi

Carrega uma imagem para a apresentação de slides.

*Parâmetros*

- ***filename*** (*string*): Nome do arquivo.
- Imagem JPG/JPEG a ser carregada no dispositivo.

*Resposta*

- ***success*** (*boolean*): Indica o sucesso da operação.
- ***filename*** (*string*): Nome do arquivo enviado.

#### Exemplo de requisição

```js
$.ajax({
    url: "/send_slideshow_images.fcgi?filename=image.jpeg&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    processData: false,
    data: image
});
```

*Exemplo de resposta*

```json
{
    "success": true,
    "filename": "image.jpeg"
}
```

## Remover imagens do Slideshow

Caso o usuário deseje remover imagens específicas do Slideshow, é possível usar o seguinte comando:

### POST /delete_slideshow_image.fcgi

Deleta uma imagem do slideshow.

*Parâmetros*

- ***filename*** (*string*): Nome do arquivo da imagem a ser deletada.

*Resposta*

- ***success*** (boolean): Indica o sucesso da operação.

#### Exemplo de requisição

```js
$.ajax({
    url: "/delete_slideshow_image.fcgi?filename=image.jpeg&session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```

*Exemplo de resposta*

```json
{
    "success": true
}
```

## Remover todas as imagens do Slideshow

Caso o usuário deseje remover todas as imagens do Slideshow em uma única operação, é possível usar o seguinte comando:

### POST /remove_slideshow_images.fcgi

Deleta todas as imagens do slideshow.

*Parâmetros*

- Essa requisição não possui parâmetros.

*Resposta*

- ***success*** (boolean): Indica o sucesso da operação.

#### Exemplo de requisição

```js
$.ajax({
    url: "/remove_slideshow_images.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```

*Exemplo de resposta*

```json
{
    "success": true
}
```
