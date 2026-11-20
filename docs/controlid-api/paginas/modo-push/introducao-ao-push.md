<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/modo-push/introducao-ao-push/ -->

# Introdução ao *Push*

Na comunicação através do *Push*, o equipamento busca proativamente por comandos a executar através de requisições HTTP enviadas periodicamente ao servidor externo para o qual esteja configurado.

Quando não há nenhum comando a ser executado pelo equipamento, o servidor deve enviar uma resposta vazia. Caso contrário, o servidor deve responder o comando a ser realizado e os parâmetros correspondentes.

Após o equipamento executar o comando lido do servidor, ele envia nova requisição contendo o resultado da operação realizada. A esta requisição é esperada uma resposta vazia, que encerra a transação de *Push*. Após cada comando executado, o equipamento já realizará nova requisição de /push, possibilitando a execução de múltiplos comandos em sequência.

Para utilizar o *Push* é necessário definir alguns parâmetros de configuração descritos em [*push_server*](../configuracoes/parametros-configuracao.md#push_server).

## Endpoints

Um *endpoint* de um web server é a URL pela qual um de seus serviços pode ser acessado por uma aplicação cliente. Portanto, *endpoints* são interfaces entre a API e a aplicação consumidora.

A URL final para a qual os eventos serão envidados pelo equipamento será:

```
hostname:port/endpoint
```

**Exemplos de Endpoints:**

- http://meuservidor.com.br/push
- 192.168.110.200:80/push

## Evento: busca de comando no servidor

O equipamento faz requisições periódicas ao servidor para verificar se há algum *push* do servidor.

O método HTTP usado é o **GET** e todos os parâmetros são enviados através da **query string**.

### GET /push

*Parâmetros*

- **deviceId** *(int 64)* : Id do equipamento que está fazendo a request.
- **uuid** *(string)* : Identificador Único Universal.

*Respostas*

## Retorno de comando para o equipamento

Mensagem de retorno do servidor para o equipamento após um evento de tentativa de *push*.

Resultado da análise da tentativa de *push*.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **verb** | *string* | Especifica o método HTTP **(GET ou POST)** que será utilizado para executar o comando no equipamento. O valor padrão é **POST**. (Opcional) |
| **endpoint** | *string* | Especifica o comando que será executado no equipamento. Consulte as seções de API para obter os comandos possíveis. (Opcional) |
| **body** | *string* | Envia os parâmetros de execução do comando no equipamento. Consulte a documentação do comando para obter os parâmetros necessários.(Opcional) |
| **contentType** | *string* | Especifica o tipo de dados que será enviado no corpo da requisição. O valor padrão é **application/json**. Caso o **contentType** seja um **application/octet-stream**, o conteúdo do **body** deve ser enviado na forma de uma única string que represente esse conteúdo em **base64**. (Opcional) |
| **queryString** | *string* | Envia os parâmetros de *query string* necessários para executar o comando. (Opcional) |

#### Exemplo da resposta à requisição /push

```
{
   verb: "POST",
   endpoint: "load_objects",
   body: { object: "users" },
   contentType: "application/json"
}
```

*Uma resposta vazia significa que não há nenhum push para ser executado.*

## Evento: resultado de comando

Envia o resultado da execução do *push* para o servidor. Essa requisição contém o resultado do comando executado no equipamento.

O método HTTP utilizado é o **POST**.

### POST /result

*Parâmetros*

- **deviceId** *(int)* : Id do equipamento que está fazendo a request. (Este parâmetro é enviado na *query string*)
- **uuid** *(string)* : Identificador Único Universal, fazendo referência ao mesmo uuid da chamada *push*.
- **endpoint** *(string)* : Comando executado pela chamada *push*.
- **response** *(string)* : Contém a resposta dada pela API com a execução do push. Consulte a documentação do comando para informações do formato da resposta. *Note que este parâmetro estará presente somente se não ocorrer erro durante a operação*
- **error** *(string)* : Especifica o erro que ocorreu durante a execução do *push*. *Note que este parâmetro estará presente somente se ocorrer algum erro.*

#### Exemplo de requisição

Requisição com o resultado de listar *usuários*.

```
{
    "response": {
        "users": [
            {
                "id":1,
                "registration": "Teste",
                "name": "Walter White",
                "password": "Heisenberg",
                "salt": "",
                "expires": 0,
                "user_type_id": 0,
                "begin_time": 0,
                "end_time": 0
            }
        ]
    }
}
```

#### Exemplo de resposta:

A resposta para essa requisição é vazia.

## Envio de comandos em lotes

Há também a possibilidade de enviar múltiplos comandos em uma única mensagem após um evento de tentativa de *push*. Para isso, utiliza-se o objeto **transactions**, que é uma lista de comandos. Cada elemento desta lista deve apresentar os seguintes parâmetros:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **transactionid** | *int* | Especifica a posição do comando na lista, começando em 1 |
| **verb** | *string* | Especifica o método HTTP **(GET ou POST)** que será utilizado para executar o comando no equipamento. O valor padrão é **POST**. (Opcional) |
| **endpoint** | *string* | Especifica o comando que será executado no equipamento. Consulte as seções de API para obter os comandos possíveis. (Opcional) |
| **body** | *string* | Envia os parâmetros de execução do comando no equipamento. Consulte a documentação do comando para obter os parâmetros necessários. (Opcional) |
| **contentType** | *string* | Especifica o tipo de dados que será enviado no corpo da requisição. O valor padrão é **application/json**. Caso o **contentType** seja um **application/octet-stream**, o conteúdo do **body** deve ser enviado na forma de uma única string que represente esse conteúdo em **base64**. (Opcional) |
| **queryString** | *string* | Envia os parâmetros de *query string* necessários para executar o comando. (Opcional) |

#### Exemplo do objeto transactions

```
 {
    "transactions": [
        {
            "transactionid" : "1",
            "verb": "POST",
            "endpoint": "set_configuration",
            "body": {
                "general": {
                "language": "en_US"
                        }
                    },
            "contentType": "application/json"
        },
        {
            "transactionid" : "2",
            "verb": "POST",
            "endpoint": "message_to_screen",
            "body": {
                "message": "hello world!",
                "timeout": 5000
                },
            "contentType": "application/json"
        }
    ]
}
```

## Resultado para comandos em lotes

A resposta aos comandos em lotes contém o objeto **transactions_results**, que é uma lista com todas as respostas dos comandos enviados, onde cada elemento apresenta os parâmetros:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **transactionid** | *int* | Especifica a posição do comando na lista, começando em 1, e deve corresponder ao **transactionid** do comando enviado. |
| **success** | *boolean* | Especifica brevemente se o comando foi bem-sucedido ou se houve um erro. |
| **response** | *string* | Quando **success == false**, especifica o erro que ocorreu durante a execução do push. Quando não há erro, contém a resposta dada pela API com a execução do push. Para certos comandos, uma resposta bem-sucedida pode ser vazia. |

#### Exemplo do objeto transactions_results

```
{
    transactions_results: [
        { transactionid: 1, success: true, response: '{}' },
        { transactionid: 2, success: true, response: '{}' }
    ]
}
```
