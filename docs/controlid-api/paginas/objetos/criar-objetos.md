<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/criar-objetos/ -->

# Criar Objetos

Cria objetos do tipo especificado, esta operação é similar ao comando **INSERT** de banco de dados. O método HTTP usado para o envio dos dados é o **POST**.

### POST /create_objects.fcgi

*Parâmetros*

- **object** *(string)* : Tipo do objeto a ser criado. Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **values** *(array de objetos JSON)* : Cada elemento deve ser um objeto JSON representando o objeto a ser criado. Cada chave deve ser um campo válido do objeto e os valores devem ter o tipo apropriado. Todos os objetos JSON devem ter os mesmos campos.

*Resposta*

- **ids** *(array de inteiros de 64 bits)* : ids dos objetos criados.

#### Exemplo de requisição

Esta requisição irá criar um usuário.

```
$.ajax({
  url: "/create_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "users",
    values: [{registration: '0123', name: 'Walter White', password: 'Heisenberg'}]
  })
});
```

#### Exemplo de resposta

```
{"ids":[8]}
```
