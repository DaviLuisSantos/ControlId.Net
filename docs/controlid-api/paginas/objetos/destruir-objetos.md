<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/destruir-objetos/ -->

# Destruir Objetos

Destrói objetos do tipo especificado, esta operação é similar ao comando **DELETE** de banco de dados. O método HTTP usado para o envio dos dados é o **POST**.

### POST /destroy_objects.fcgi

*Parâmetros*

- **object** *(string)* : Tipo do objeto a ser destruído. Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **where** *(objetos JSON)* : Por favor, consulte a referência de [load_objects](../objetos/carregar-objetos.md) para a descrição completa do parâmetro where. Apenas o mesmo objeto pode ser usado como filtro.

*Resposta*

- **changes** *(int)* : Número de mudanças efetuadas.

#### Exemplo de requisição

Esta requisição irá destruir todos os usuários com nome Walter White.

```
$.ajax({
  url: "/destroy_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "users",
    where: {
      users: {name: 'Walter White'}
    }
  })
});
```

#### Exemplo de resposta

```
{"changes":0}
```
