<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/criar-ou-modificar-objetos/ -->

# Criar ou Modificar Objetos

Cria objetos do tipo especificado ou, caso o "id" especificado já exista, modifica objeto já criado sem necessitar da função carregar objetos. Se um "id" não for fornecido, ocorre uma inserção.

### POST /create_or_modify_objects.fcgi

*Parâmetros*

- **object** *(string)* : Tipo do objeto a ser criado ou modificado. Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **values** *(array de objetos JSON)* : Cada elemento deve ser um objeto JSON representando o objeto a ser criado ou modificado. Cada chave deve ser um campo válido do objeto e os valores devem ter o tipo apropriado.

*Resposta*

- **changes** (*inteiro de 64 bits*) : quantidade de objetos criados ou modificados.

#### Exemplo de requisição

Esta requisição irá criar um usuário.

```
$.ajax({
  url: "/create_or_modify_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "users",
    values: [{"id":1, registration: '1234', name: 'Walter H. White', password: 'Heisenberg'}]
  })
});
```

#### Exemplo de resposta

```
{"changes": 1}
```
