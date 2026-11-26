<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/slideshow/ -->

# Slideshow

O recurso de slideshow permite exibir uma sequência de imagens na tela do equipamento. As imagens são armazenadas no equipamento e devem estar nos formatos `.jpg` ou `.jpeg`, com limite de 10 imagens. Após cada upload ou exclusão, o vídeo do slideshow é regenerado automaticamente.

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

## Listar imagens do slideshow

Retorna a lista de arquivos de imagem atualmente armazenados no equipamento.

### GET /get_slideshow_images_list.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **images** *(array)*: Lista de objetos de imagem. Cada objeto contém:
  - **filename** *(string)*: Nome do arquivo de imagem.
- **max_files** *(int)*: Número máximo de imagens permitidas no equipamento (sempre `10`).

#### Exemplo de requisição

```
$.ajax({
    url: "/get_slideshow_images_list.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "images": [
        { "filename": "slide1.jpg" },
        { "filename": "slide2.jpeg" }
    ],
    "max_files": 10
}
```

## Obter imagem do slideshow

Baixa uma imagem do slideshow armazenada no equipamento.

### GET /get_slideshow_image.fcgi

*Parâmetros*

- **filename** *(string)*: **Obrigatório.** Nome do arquivo de imagem a ser obtido. Passado como parâmetro na query string.

*Resposta*

- Arquivo de imagem em formato binário com Content-Type `.jpg` ou `.jpeg`.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_slideshow_image.fcgi?filename=slide1.jpg&session=" + session,
    type: 'GET'
});
```

## Enviar imagem para o slideshow

Envia uma imagem para o slideshow do equipamento. O corpo da requisição deve conter o conteúdo binário bruto da imagem. Apenas arquivos `.jpg` e `.jpeg` são aceitos. Retorna erro se o limite de 10 imagens já tiver sido atingido ou se houver menos de 10 MB de espaço livre no armazenamento. Após o upload, o vídeo do slideshow é regenerado automaticamente.

### POST /send_slideshow_images.fcgi

*Parâmetros*

- **filename** *(string)*: **Obrigatório.** Nome a ser atribuído ao arquivo de imagem. Passado como parâmetro na query string. Deve ter extensão `.jpg` ou `.jpeg`.

*Resposta*

- **success** *(bool)*: `true` se o arquivo foi salvo com sucesso.
- **filename** *(string)*: Nome do arquivo de imagem salvo.

#### Exemplo de requisição

```
$.ajax({
    url: "/send_slideshow_images.fcgi?filename=slide1.jpg&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de imagem]
});
```

#### Exemplo de resposta

```
{
    "success": true,
    "filename": "slide1.jpg"
}
```

## Excluir imagem do slideshow

Exclui uma imagem específica do slideshow do equipamento. O vídeo do slideshow é regenerado automaticamente após a exclusão.

### POST /delete_slideshow_image.fcgi

*Parâmetros*

- **filename** *(string)*: **Obrigatório.** Nome do arquivo de imagem a ser excluído. Passado como parâmetro na query string.

*Resposta*

- **success** *(bool)*: `true` se o arquivo foi excluído com sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/delete_slideshow_image.fcgi?filename=slide1.jpg&session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "success": true
}
```

## Remover todas as imagens do slideshow

Remove todas as imagens do slideshow, o arquivo de configuração e o vídeo gerado do equipamento. Também notifica o player de vídeo sobre a alteração do conteúdo.

### POST /remove_slideshow_images.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **success** *(bool)*: `true` se todo o conteúdo foi removido com sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/remove_slideshow_images.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "success": true
}
```
