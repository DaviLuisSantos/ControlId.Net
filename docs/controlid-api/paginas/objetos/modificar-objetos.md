<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/modificar-objetos/ -->

# Modificar Objetos

Modifica objetos do tipo especificado, esta operação é similar ao comando **UPDATE** de banco de dados. O método HTTP usado para o envio dos dados é o **POST**.

### POST /modify_objects.fcgi

*Parâmetros*

- **object** *(string)* : Tipo do objeto a ser modificado. Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **values** *(array de objetos JSON)* : Cada elemento deve ser um objeto JSON representando o objeto a ser modificado. Cada chave deve ser um campo válido do objeto e os valores devem ter o tipo apropriado.
- **where** *(objetos JSON)* : Por favor, consulte a referência de [load_objects](../objetos/carregar-objetos.md) para a descrição completa do parâmetro where. Apenas o mesmo objeto pode ser usado como filtro.

*Resposta*

- **changes** *(int)* : Número de mudanças efetuadas.

#### Exemplo de requisição

Esta requisição irá modifica todos os usuários com nome Walter White.

```
$.ajax({
  url: "/modify_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "users",
    values: {name: 'Walter Hartwell White'},
    where: {
      users: {name: 'Walter White'}
    }
  })
});
```

#### Exemplo de resposta

```
{"changes":2}
```
