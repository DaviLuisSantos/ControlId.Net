<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/fotos-e-logotipo/gerenciar-logotipo/ -->

# Gerenciar Logotipo

Utilize as funções descritas abaixo para obter, alterar e remover os logotipos do terminal de acesso.

Os dispositivos podem armazear até 8 logos em slots diferentes e mostram exclusivamente um por vez.

## Obter logotipo

Retorna a imagem de logotipo que está gravada no equipamento, o **content_type** é **image/png**. Quando um logotipo é carregado no equipamento, ele é exibida constantemente na tela principal se estiver habilitado.

### POST /logo.fcgi

Esta requisição demanda ao dispositivo uma resposta com o logo armazenado no slot especificado. Caso nenhum slot seja especificado, a resposta será referente ao logo armazenado no primeiro slot.

*Parâmetros*

- id: inteiro (de 1 a 8) (opcional)
  - Indica o índice do slot do qual recuperar o logotipo

*Resposta*

- Imagem de logotipo previamente carregada no equipamento.

*Exemplo*

```
$.ajax({
    url: "/logo.fcgi?session=" + session + "&id=5",
    type: 'POST',
    contentType: 'image/png',
});
```

Irá recuperar logo na 6ª posição ou uma mensagem caso não haja nenhum.

## Alterar logotipo

Altera a imagem de logotipo do equipamento. O corpo da requisição deverá conter o PNG da imagem e o **content-type** é **application/octet-stream**. Como parâmetro devemos indicar o *id* também. Caso nenhum seja indicado, a modificação será no primeiro logo.

É importante notar que a imagem enviada não deve ultrapassar 1MB ou uma resolução de 1000x1000. Qualquer outra resolução enviada será convertida em escala para 272x240 para caber na tela de menu.

### POST /logo_change.fcgi

*Parâmetros*

- id: inteiro (de 1 a 8) (opcional)
  - Indica o índice do slot do qual recuperar o logotipo
- Imagem (binário) de logotipo PNG que deverá ser carregado no equipamento.

*Resposta*

- O retorno da requisição é um objeto json vazio.

*Exemplo*

```
$.ajax({
    url: "/logo_change.fcgi?session=" + session + "&id=5",
    type: 'POST',
    contentType: 'application/octet-stream',
    processData: false,
    data: image
});
```

Irá modificar o logo na 6ª posição pela imagem passada. Caso nenhum índice seja indicado, o logo na primeira posição será modificado.

## Remover logotipo

Remove a imagem de logotipo no slot especificado do equipamento. Caso nenhum slot seja especificado, remove o logo do primeiro slot e define o dispositivo para não exibir nenhum logo.

### POST /logo_destroy.fcgi

*Parâmetros*

- id: inteiro (de 1 a 8) (opcional)
  - Indica o índice do slot do qual recuperar o logotipo

*Resposta*

- O retorno da requisição é um objeto json vazio.

*Exemplo*

```
$.ajax({
    url: "/logo_destroy.fcgi?session=" + session + "&id=5",
    type: 'POST'
});
```

Irá deletar a imagem na 6ª posição. Caso o parâmetro *id* não seja especificado, a imagem deletada será a da primeira posição e o dispositivo não exibirá nenhum logo (*show_logo = 0*).

## Mostrar logotipo

Mostra a imagem de logotipo na tela do equipamento.

### POST /set_configuration.fcgi

*Parâmetros*

- ***show_logo***: string (de 0 a 8)
  - Caso 0: Nenhum logo será exibido
  - Caso 1-8: O logo correspondente será exibido

*Resposta*

- O retorno da requisição é um objeto json vazio.

*Exemplo*

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "general": {
            "show_logo": "5"
        }
    })
});
```

Irá habilitar o logo da 5ª posição como visível e desabilitar os outros.
