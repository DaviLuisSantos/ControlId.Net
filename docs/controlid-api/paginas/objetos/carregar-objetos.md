<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/carregar-objetos/ -->

# Carregar Objetos

Carrega todos os objetos do tipo especificado. Esta operação é similar ao comando **SELECT** de banco de dados. O método HTTP usado para o envio dos dados é o **POST**.

### POST /load_objects.fcgi

*Parâmetros*

- **object** *(string)* : Tipo do objeto a ser carregado. Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **fields** *(array de strings)* : Campos a serem recebidos do objeto. Se esse parâmetro não estiver presente, todos os campos serão recebidos (**opcional**).
- **limit** *(int)* : Máximo número de objetos a adquirir (**opcional**).
- **offset** *(int)* : Pula os primeiros offset objetos que seriam adquiridos. Útil para implementar paginação (**opcional**). O parâmetro **limit** deve estar definido caso contrário, offset será ignorado.
- **order** *(array de strings)* : Quais campos devem ser usados para ordenar os objetos. Se algum dos elementos tem o valor especial ascending ou descending, a ordenação será ascendente ou descendente, respectivamente (**opcional**). Se forem definidos múltiplos valores ascending ou descending, apenas a última definição será levada em consideração.
  Caso o campo especificado deva ser de algum objeto diferente do que está sendo carregado, pode-se passar um objeto JSON com os seguintes parâmetros `{ object: 'object', field: 'field' }`, onde object e field são o objeto e campo pelo qual se quer ordenar.
- **group** *(array de strings)* : Este campo determina o agrupamento dos campos, muito similar ao group_by de um banco de dados (**opcional**). É um array que pode conter: o nome do campo que será agrupado ou o objeto caso o campo não pertença a tabela original. `{ object: 'object', field: 'field' }`, onde object e field são o objeto e campo pelo qual se quer ordenar.
- **join** *(string)* : Este campo determina como as tabelas serão agrupadas. Este campo é uma string que pode ter dois valores:"LEFT" ou "INNER" (**opcional**).
- **where** *(string)* : Quais objetos devem ser adquiridos (**opcional**). Através desse parâmetro, é possível especificar filtros para receber apenas alguns objetos. Cada chave deve ser o tipo do objeto a ser adquirido, e cada valor deve ser um objeto JSON. Por exemplo: `where: { users: {} }`. A chave de cada objeto JSON deve ser o campo do objeto, e o valor deve ser o filtro usado. Por exemplo: `where: { users: { id: 1 } }`. No exemplo acima, apenas users que tem um id igual a 1 (um) serão filtrados. Note que o tipo do atributo do objeto (inteiro, string, etc.) deve ser do mesmo tipo que está sendo usado como filtro. O filtro também pode especificar um dos seguintes operadores de comparação:
  O operador **igual a** ('=', '==', 'LIKE'), este é o operador padrão e não precisa ser especificado. O operador **diferente de** ('!=', '<>', 'NOT LIKE'). O operador **maior que** ('>'). O operador **menor que** ('<'). O operador **maior ou igual a** ('>=').
  O operador **menor ou igual a** ('<='). O operador **em** ('IN'). O operador **não em** ('NOT IN').
  Operadores podem ser especificados usando um objeto JSON cuja chave é o operador e o valor é o valor da restrição. Por exemplo: where: `{ users: { id: {"!=": 1} } }`.
  No exemplo acima, apenas users que possuem um id diferente de 1 (um) serão adquiridos.
  Também é possível especificar múltiplas restrições. Por exemplo: `where: { users: { id: {">=": 1, "<=": 2} } }` no exemplo acima, apenas users que possuem um id maior que 1 (um) e menor que 2 (dois) serão adquiridos.
  **Caso seja definida uma lista de ids como filtro no parâmetro *where*, deve-se tomar o cuidado de não listar mais de 999 elementos explicitamente. Do contrário, a requisição falhará.**

*Resposta*

- **NOME_DO_OBJETO** *(array de objetos JSON)* : Cada objeto JSON representa um objeto carregado.

#### Exemplo de requisição

Esta requisição irá carregar todos os usuários.

```
$.ajax({
  url: "/load_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "users"
  })
});
```

#### Exemplo de resposta

```
{
    "users":[{"id":1,"registration":"","name":"th0","password":"","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":2,"registration":"","name":"th1","password":"","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":3,"registration":"","name":"th2","password":"","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":4,"registration":"","name":"th3","password":"","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":5,"registration":"","name":"th4","password":"","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":6,"registration":"","name":"th5","password":"","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":7,"registration":"1234","name":"Walter White","password":"Heisenberg","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0},{"id":8,"registration":"0123","name":"Walter White","password":"Heisenberg","salt":"","expires":0,"user_type_id":0,"begin_time":0,"end_time":0}
}
```

#### Exemplo de requisição

Esta requisição exemplifica o uso do **where** para retornar logs de acesso que possuam o valor de id maior que 1 e o valor do evento igual a 7.

```
$.ajax({
      url: "/load_objects.fcgi?session=" + session,
      type: 'POST',
      contentType: 'application/json',
      data: JSON.stringify({
        "object": "access_logs",
        "where":[
          {
              "object": "access_logs",
              "field": "id",
              "operator": ">",
              "value": 1,
              "connector": ") AND ("            
          },
          {
              "object": "access_logs",
              "field": "event",
              "operator": "=",
              "value": 7          
          }
        ]
      })
});
```

## Carregamento de Grandes Bases de Usuários com Biometrias

Para realizar o carregamento adequado de grandes bases de usuário, um procedimento deve ser adotado para realizar a ação adequadamente. É necessário utilizar um processo de sincronismo de *templates* através de um mecanismo implementado na API.

O processo de sincronismo garante o carregamento adequado de grandes bases de dados. Primeiramente deve-se utilizar o *endpoint* *template_sync_init.fcgi*, responsável por desabilitar sincronização de *templates* carregados. Em seguida, deve-se carregar os *templates* com *create_objects.fcgi*. Após isso, deve-se reabilitar e realizar sincronização de templates carregados atraves de uma requisição ao *endpoint* *template_sync_end.fcgi*.

Os detalhes sobre a execução desses *endpoints* são mostrados abaixo.

### POST /template_sync_init.fcgi

*Parâmetros*

Esta chamada não possui parâmetros.

*Resposta*

Resposta vazia.

### POST /template_sync_end.fcgi

*Parâmetros*

Esta chamada não possui parâmetros.

*Resposta*

Resposta vazia.
