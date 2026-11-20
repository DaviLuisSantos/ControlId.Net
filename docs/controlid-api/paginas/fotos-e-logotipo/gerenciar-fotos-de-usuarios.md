<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/fotos-e-logotipo/gerenciar-fotos-de-usuarios/ -->

# Gerenciar fotos de usuários

Utilize as funções descritas abaixo para listar, inserir e apagar fotos de usuários no terminal de acesso. Note que estas ações são desnecessárias ao operar em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md), exceto para usuários com acesso em modo de contingência.

## Obter foto do usuário

Obtém a foto de um usuário especificado pelo seu id. O método HTTP usado é o GET. O **contentType** é **image/jpeg**. Todos os parâmetros são enviados através da query string.

### GET /user_get_image.fcgi

*Parâmetros*

- **user_id** *(int 64)* : Identificador do usuário cuja foto será obtida.

*Resposta*

- Imagem do usuário em **jpg**.

#### Exemplo de requisição

Esta requisição retornar a foto do usuário especificado.

```
$.ajax({
  url: "/user_get_image.fcgi?user_id=123&session=" + session,
  type: 'GET',
  contentType: 'image/jpeg',
});
```

## Obter lista de fotos

Obtém a lista de ids dos usuários que possuem fotos, implementado a partir da versão do firmware 3.10.0. O método HTTP usado é o POST.

### GET /user_list_images.fcgi

*Parâmetros*

- **get_timestamp** *(int)*: Se positivo, a resposta traz, no lugar de **user_ids**, a lista **image_info** com o timestamp de cadastro de cada foto. Padrão: 0. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

*Resposta*

- **user_ids** *(array de int 64)* : ids dos usuários que possui fotos.
- **image_info** *(array de objetos JSON)*: Retornado no lugar de **user_ids** quando *get_timestamp* é positivo. Cada objeto contém **user_id** *(int64)*, o identificador do usuário, e **timestamp** *(int)*, o timestamp de cadastro da foto no padrão Unix Timestamp.

#### Exemplo de requisição

Esta requisição irá listar os ids dos usuários que possuem foto.

```
$.ajax({
  url: "/user_list_images.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{"user_ids":[1,2,3,4,5,6]}
```

## Inserir foto de usuário

Salva a foto de um usuário especificado pelo seu id. Diferentemente da grande maioria dos comandos desta API, neste comando, o Content-Type necessariamente é application/octet-stream. Portanto, a imagem é passada no Content do método POST, e o id do usuário é passado na Query String. O tamanho da foto deve ser menor que 1MB.

### POST /user_set_image.fcgi

*Parâmetros*

- **user_id** *(int 64)* : Identificador do usuário cuja foto será atribuída. **Esse parâmetro é passado na Query String.**

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Esta requisição irá definir uma nova foto para o usuário

```
$.ajax({
  url: "/user_set_image.fcgi?user_id=123&session=" + session,
  type: 'POST',
  contentType: 'application/octet-stream',
  data: [bytes da imagem enviada]
});
```

## Inserir lista de fotos

Este endpoint insere fotos a usuários em massa. Lembrando que o acesso tem um limite de 1MB por request. Recebe um array de ids e imagens.

### POST /user_set_image_list.fcgi

*Parâmetros*

- **user_images** *(array)* : Este parâmetro deverá ser representado como um objeto JSON contendo os membros **user_id** e **image** (a imagem em base 64).

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Esta requisição irá atribuir fotos ao usuários X e Y.

```
$.ajax({
  url: "/user_set_image_list.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: {
    user_images: 
    [
      {user_id: <id usuário X>, image: <imagem do usuário em base 64>},
      {user_id: <id usuário Y>, image: <imagem do usuário em base 64>}
    ]
  }
});
```

## Excluir foto de usuário

Exclui a foto de um usuário especificado pelo seu id, um array de ids ou uma flag "all" para remover todos . O método HTTP usado é o POST.

### POST /user_destroy_image.fcgi

*Parâmetros*

- **user_id** *(int 64)* : Identificador do usuário cuja foto será destruída.
- **user_ids** *(array int 64)* : Array de id de usuários cuja as fotos serão removidas.
- **dangling** *(bool)* : Se for setado como 'true' remove as fotos não vinculadas a usuários.
- **all** *(bool)* : Se for setado como 'true' todos os usuários terão as fotos excluídas.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Remove a foto do usuário com id 123.

```
$.ajax({
  url: "/user_destroy_image.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    user_id: "123"
  })
});
```
