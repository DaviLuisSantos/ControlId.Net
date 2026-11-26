<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/schema-objeto-customizado/ -->

# Schema de Objeto Customizado

Endpoints para gerenciar schemas de tabelas de objetos customizados: criar e remover tabelas, e adicionar, atualizar e remover colunas. Tabelas customizadas estendem o modelo de objetos padrão com estruturas definidas pelo usuário.

## Obter metadados de objetos

Retorna os metadados de schema de todas as tabelas de objetos (padrão e customizadas), incluindo tipos de campo, restrições e relacionamentos de join.

### POST /object_metadata.fcgi

*Parâmetros*

- **filter** *(string)*: Opcional. Filtra quais tabelas retornar. Valores: `"custom"` (apenas tabelas customizadas), `"default"` (apenas tabelas padrão). Se omitido, retorna todas.

*Resposta*

Objeto onde cada chave é o nome de uma tabela e cada valor é um objeto com:

- **fields** *(object)*: Mapa de nome do campo → descritor do campo:
  - **type** *(string)*: Tipo de dado (`"INTEGER"`, `"BIGINT"`, `"TEXT"`, `"BLOB"`).
  - **primary key** *(bool)*: Se o campo é chave primária.
  - **not null** *(bool)*: Se o campo possui restrição NOT NULL.
  - **default value** *(string)*: Valor padrão, se houver.
  - **unique** *(bool)*: Se o campo possui restrição UNIQUE.
  - **foreign key** *(object)*: Presente apenas se o campo for chave estrangeira. Contém:
    - **object** *(string)*: Nome da tabela referenciada.
    - **field** *(string)*: Nome do campo referenciado.
- **joins** *(object)*: Mapa de nome do join → array de referências `{ "object", "field" }`.

#### Exemplo de requisição

```
$.ajax({
    url: "/object_metadata.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "filter": "custom"
    })
});
```

#### Exemplo de resposta

```
{
    "custom_departments": {
        "fields": {
            "id": { "type": "INTEGER", "primary key": true, "not null": true, "default value": "", "unique": false },
            "name": { "type": "TEXT", "primary key": false, "not null": false, "default value": "", "unique": false }
        },
        "joins": {}
    }
}
```

## Adicionar tabela customizada

Cria uma nova tabela de objeto customizado com um conjunto inicial de colunas.

### POST /object_add.fcgi

*Parâmetros*

- **object** *(string)*: Obrigatório. Nome interno da tabela (usado como identificador e nome da tabela SQL).
- **name** *(string)*: Obrigatório. Label de exibição da tabela.
- **id** *(int)*: Opcional. ID personalizado para a tabela. Atribuído automaticamente se omitido.
- **fields** *(array de objetos)*: Obrigatório. Pelo menos um campo deve ser fornecido. Cada campo contém:
  - **column_name** *(string)*: Obrigatório. Nome interno da coluna.
  - **name** *(string)*: Obrigatório. Label de exibição da coluna.
  - **type** *(string)*: Obrigatório. Tipo de dado: `"INTEGER"`, `"BIGINT"`, `"TEXT"` ou `"BLOB"`.
  - **constraint** *(string)*: Opcional. Valores: `"NONE"` (padrão) ou `"NOT_NULL"`.
  - **unique** *(bool)*: Opcional. Padrão `false`.
  - **foreign_key** *(objeto)*: Opcional. Referência de chave estrangeira:
    - **object** *(string)*: Nome da tabela referenciada.
    - **field** *(string)*: Nome do campo referenciado.

*Resposta*

- **ids** *(array de int)*: Array contendo o ID da tabela criada.

#### Exemplo de requisição

```
$.ajax({
    url: "/object_add.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "object": "custom_departments",
        "name": "Departamentos",
        "fields": [
            { "column_name": "name", "name": "Nome", "type": "TEXT" },
            {
                "column_name": "user_id",
                "name": "Usuário",
                "type": "INTEGER",
                "constraint": "NOT_NULL",
                "foreign_key": { "object": "users", "field": "id" }
            }
        ]
    })
});
```

#### Exemplo de resposta

```
{
    "ids": [5]
}
```

## Remover tabelas customizadas

Remove uma ou mais tabelas customizadas pelos seus IDs.

### POST /object_remove.fcgi

*Parâmetros*

- **ids** *(array de int)*: Obrigatório. IDs das tabelas customizadas a remover.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/object_remove.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "ids": [5]
    })
});
```

## Adicionar campo a tabela customizada

Adiciona uma nova coluna a uma tabela customizada existente.

### POST /object_add_field.fcgi

*Parâmetros*

- **object** *(string)*: Obrigatório. Nome da tabela customizada à qual adicionar a coluna.
- **column_name** *(string)*: Obrigatório. Nome interno da coluna.
- **name** *(string)*: Obrigatório. Label de exibição da coluna.
- **type** *(string)*: Obrigatório. Tipo de dado: `"INTEGER"`, `"BIGINT"`, `"TEXT"` ou `"BLOB"`.
- **constraint** *(string)*: Opcional. Valores: `"NONE"` (padrão) ou `"NOT_NULL"`.
- **unique** *(bool)*: Opcional. Padrão `false`.
- **default_value** *(varia)*: Obrigatório quando `constraint` for `"NOT_NULL"`. Tipo deve corresponder ao tipo do campo.
- **foreign_key** *(objeto)*: Opcional. Referência de chave estrangeira:
  - **object** *(string)*: Nome da tabela referenciada.
  - **field** *(string)*: Nome do campo referenciado.

*Resposta*

- **ids** *(array de int)*: Array contendo o ID da coluna criada.

#### Exemplo de requisição

```
$.ajax({
    url: "/object_add_field.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "object": "custom_departments",
        "column_name": "description",
        "name": "Descrição",
        "type": "TEXT"
    })
});
```

#### Exemplo de resposta

```
{
    "ids": [12]
}
```

## Remover campos de tabela customizada

Remove uma ou mais colunas de uma tabela customizada pelos seus IDs.

### POST /object_remove_fields.fcgi

*Parâmetros*

- **ids** *(array de int)*: Obrigatório. IDs das colunas a remover.

*Resposta*

- **ids** *(array de int)*: IDs das colunas removidas.

#### Exemplo de requisição

```
$.ajax({
    url: "/object_remove_fields.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "ids": [12]
    })
});
```

## Atualizar campo em tabela customizada

Atualiza a definição de uma coluna existente em uma tabela customizada.

### POST /object_update_field.fcgi

*Parâmetros*

- **id** *(int)*: Obrigatório. ID da coluna a atualizar.
- **column_name** *(string)*: Obrigatório. Novo nome interno da coluna.
- **type** *(string)*: Obrigatório. Tipo de dado: `"INTEGER"`, `"BIGINT"`, `"TEXT"` ou `"BLOB"`.
- **constraint** *(string)*: Obrigatório. Valores: `"NONE"` ou `"NOT_NULL"`.
- **name** *(string)*: Opcional. Novo label de exibição.
- **unique** *(bool)*: Opcional. Define restrição unique.
- **default_value** *(varia)*: Obrigatório quando `unique` estiver sendo definido. Tipo deve corresponder ao tipo do campo.
- **foreign_key** *(objeto)*: Opcional. Referência de chave estrangeira:
  - **object** *(string)*: Nome da tabela referenciada.
  - **field** *(string)*: Nome do campo referenciado.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/object_update_field.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "id": 12,
        "column_name": "description",
        "type": "TEXT",
        "constraint": "NONE",
        "name": "Descrição Completa"
    })
});
```
