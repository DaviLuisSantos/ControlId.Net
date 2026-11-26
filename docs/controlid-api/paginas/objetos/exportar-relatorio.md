<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/exportar-relatorio/ -->

# Exportar Relatório

O relatório será gerado conforme as configurações dos campos definidos. Os parâmetros de busca são os mesmos do método [load_objects](../objetos/carregar-objetos.md). O método HTTP usado é o POST.

### POST /report_generate.fcgi

*Parâmetros*

- **object** *(string)* : Este campo é obrigatório e define o tipo de objeto que irá compor o relatório que será gerado, por exemplo, "users". Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **file_name** *(string)* : Este campo define o nome do arquivo de saída (**opcional**).
- **delimiter** *(string)* : Este campo define o delimitador de campo, similar à estrutura de um arquivo .csv. Por exemplo: ";" ou "," (**opcional**).
- **header** *(string)* : Este campo define o cabeçalho do relatório (**opcional**).
- **line_break** *(string)* : Este campo define a quebra de linha, como "\n" ou "\r\n" (**opcional**).
- **columns** *(array de objetos JSON)* : Este campo define as colunas que serão mostradas no relatório, semelhante ao campo `fields` do método `load_objects`. Esse campo é um array de objetos, que possuem os seguintes campos:

> - **field** *(string)* : Este campo pertence aos objetos do array `columns` e representa o nome do campo que será gerado.
> - **type** *(string)* : Este campo pertence aos objetos do array `columns` e determina o tipo da coluna. Pode ser: "object_field" (Campo de coluna normal), "counter" (Contador de registros) ou "fixed" (Campo fixo):
>   > - **object_field** *(string)* : Este é um atributo de coluna normal. Ele possui dois campos: `field` e `object`, ambos do tipo string, que representam, respectivamente, a coluna e a tabela.
>   > - **counter** *(string)* : Este atributo possui dois campos opcionais do tipo inteiro: `offset` (representa o valor inicial do contador, padrão: 0) e `step` (representa o passo do contador, padrão: 1).
>   > - **fixed** *(string)* : Este atributo contém um campo obrigatório do tipo string que representa o valor fixo da coluna.
> - **format** *(objeto JSON)* : Este campo pertence aos objetos do array `columns` e define como a coluna será formatada e apresentada (**opcional**). Esse objeto possui os seguintes campos:
>   > - **adjustment** *(int)* : Este campo define o alinhamento da coluna: "1" (à esquerda), "2" (à direita) ou "0" (sem ajuste) (**opcional**).
>   > - **fill** *(string)* : Este campo define o valor de preenchimento dos espaços vazios. Por exemplo: "0" ou " " (espaço) (**opcional**).
>   > - **format** *(string)* : Este campo define a formatação da coluna, por exemplo: "%d" para dia, "%m" para mês, "%Y" para ano, "%H" para horas, "%M" para minutos, e "%S" para segundos (**opcional**).
>   > - **width** *(int)* : Este campo define a largura da coluna em número de caracteres (**opcional**).

#### Exemplo de requisição

Gerando relatório:

```
$.ajax({
  url: "/report_generate.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
      order:["ascending","name"],
        "where":{
              "users":{},
              "groups":{},
              "time_zones":{}
          },
          object:"users",
          delimiter:";",
          line_break:"\\r\\n",
          header:"Nome (Usuário);Código (Usuário)",
          file_name:"",
          join:"LEFT",
          columns:[
            {
                type:"object_field",
                object:"users",
                field:"name",
                format:{
                    adjustment:1,
                    width:50,
                    fill:" ",
                    format:"%s"
                }
              },
              {
                type:"object_field",
                object:"users",
                field:"id",
                format:{
                    adjustment:2,
                    width:5,
                    fill:"0",
                    format:"%s"
                }
              }
          ]
  })
});
```

### POST /export_afd.fcgi NEW!

Permite a extração de um relatório em formato AFD. O relatório pode ser utilizado como ferramenta para controle de ponto, sendo fornecidos nele todos os registros de acesso **autorizados** que se encontram armazenados no aparelho. O relatório AFD também pode ser gerado a partir da *interface web* e da *GUI* do equipamento.

É possível filtrar a geração do relatório em formato AFD a partir de uma data inicial ou através de um NSR inicial.

O retorno pode seguir tanto o formato legado das Portarias anteriores (MTE 1510/2009, Inmetro 595/2013) quanto o formato definido pela Portaria 671. Caso a requisição contenha o parâmetro mode na query_string ou no *body* com o valor 671, o retorno seguirá o formato definido pela Portaria 671. Para qualquer outro valor, ou na ausência do parâmetro mode, o AFD retornado seguirá o formato da Portaria 595.

*Parâmetros*

- **initial_date** *(objeto JSON)* : Contém informações sobre a data inicial a partir da qual o relatório em formato AFD será gerado.
  > - **day** *(int)* : Valor numérico representando o dia.
  > - **month** *(int)* : Valor numérico representando o mês.
  > - **year** *(int)* : Valor numérico representando o ano.
- **initial_nsr** *(int)* : NSR inicial aplicado como filtro.
- **limit** *(int)*: Número máximo de registros a retornar. Padrão: 1500. A paginação só é aplicada quando **limit** ou **offset** é enviado.
- **offset** *(int)*: Número de registros a ignorar antes de retornar os resultados, para paginação. Padrão: 0.
- **mode** *(int)* : Modo de retorno do AFD. Com o valor 671, o retorno segue o formato da Portaria 671; para qualquer outro valor, ou na ausência do parâmetro, segue o formato da Portaria 595.

*Resposta*

- Documento com extensão *.txt* contendo o relatório em formato AFD. O nome do documento respeita a norma para AFD, sendo construído com AFD + número de série do dispositivo.

#### Exemplo de requisição

Gerando relatório integral com formato 595:

```
$.ajax({
  url: "/export_afd.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({})
});
```

Gerando relatório integral com formato 671:

```
$.ajax({
  url: "/export_afd.fcgi?session=" + session + "&mode=671",
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({})
});
```

Gerando relatório com filtro de data inicial:

```
$.ajax({
    url: "/export_afd.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "initial_date":{
          "day":11,
          "month":7,
          "year":2022
        }
    })
});
```

Gerando relatório com filtro de NSR:

```
$.ajax({
    url: "/export_afd.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "initial_nsr":1
    })
});
```

Gerando relatório paginado (segunda página, com 100 registros por página):

```
$.ajax({
    url: "/export_afd.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "limit":100,
        "offset":100
    })
});
```

### POST /export_object.fcgi

Exporta uma tabela de objetos como arquivo CSV. Opcionalmente restringe a saída a colunas específicas.

*Parâmetros*

- **object** *(string)*: Obrigatório. Nome da tabela a exportar. Consulte [Lista de Objetos](../objetos/lista-de-objetos.md) para os nomes disponíveis.
- **columns** *(array de strings)*: Opcional. Nomes das colunas a incluir. Se omitido ou vazio, todas as colunas são exportadas.

*Resposta*

- Arquivo CSV. O nome do arquivo é definido como `<objeto>.csv` pelo cabeçalho `Content-Disposition`.

#### Exemplo de requisição

```
$.ajax({
    url: "/export_object.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "object": "users",
        "columns": ["id", "name", "registration"]
    })
});
```

### POST /export_objects.fcgi

Exporta múltiplas tabelas de objetos em um único arquivo CSV. O arquivo começa com a seção de metadados de tabelas customizadas, seguida pelos dados de cada tabela solicitada.

*Parâmetros*

- **objects** *(array de objetos)*: Obrigatório. Lista de tabelas a exportar. Cada item contém:
  - **object** *(string)*: Obrigatório. Nome da tabela.
  - **columns** *(array de strings)*: Opcional. Nomes das colunas a incluir. Se omitido ou vazio, todas as colunas são exportadas.

*Resposta*

- Arquivo CSV chamado `exported.csv`. Começa com o bloco de metadados de tabelas customizadas, seguido pelos dados de cada tabela solicitada na ordem indicada.

#### Exemplo de requisição

```
$.ajax({
    url: "/export_objects.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "objects": [
            { "object": "users", "columns": ["id", "name"] },
            { "object": "access_rules" }
        ]
    })
});
```

### GET /export_custom_tables_metadata.fcgi

Exporta os metadados de schema de todas as tabelas de objetos customizados (definidos pelo usuário) como arquivo CSV. Não exporta os dados das linhas — apenas definições de tabelas e colunas.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo CSV (`exported.csv`). O arquivo começa com uma linha de cabeçalho `cid_metadata`, e para cada tabela customizada:
  - Uma linha com: `table_name`, `label`, `id`
  - Uma linha de cabeçalho de colunas: `column_name`, `default_value`, `type`, `constraint`, `unique`, `name`, `foreign_key_object`, `foreign_key_field`
  - Uma linha por coluna com os valores correspondentes

#### Exemplo de requisição

```
$.ajax({
    url: "/export_custom_tables_metadata.fcgi?session=" + session,
    type: 'GET'
});
```

### POST /export_audit_logs.fcgi NEW!

Permite a extração de Logs de Auditoria. O relatório pode ser utilizado como ferramenta para controle do dispositivo e verificação dos seus eventos, sendo fornecidos nele os últimos 25000 registros, que é a quantidade máxima armazenada (com rotação) no banco de dados do equipamento.

É possível filtrar a geração do relatório a partir de 9 categorias de log de eventos.

#### Categorias de log de eventos

- **config** : Log de auditoria de configurações. Registra todas as mudanças nas configurações do sistema.
- **api** : Log de auditoria da API. Registra as requisições das sessões ao acessar a API.
- **usb** : Log de auditoria de pendrive USB. Registra todos os eventos que fazem uso de pendrive USB.
- **network** : Log de auditoria da Rede. Registra todas as mudanças referentes à rede do dispositivo.
- **time** : Log de auditoria do tempo. Registra as mudanças no horário do dispositivo.
- **online** : Log de auditoria do modo online. Registra as conexões e as respostas do servidor.
- **menu** : Log de auditoria do menu do dispositivo. Registra o acesso ao menu em modos offline e online.
- **boot** : Log de auditoria de inicialização. Registra as inicializações do equipamento.
- **push** : Log de auditoria do servidor push. Registra a comunicação com o servidor do modo push.

*Parâmetros*

- **config** *(int)* : Habilita o filtro para os logs do evento de configuração. Para desabilitar, "0"; para habilitar, "1".
- **api** *(int)* : Habilita o filtro para os logs do evento de API. Para desabilitar, "0"; para habilitar, "1".
- **usb** *(int)* : Habilita o filtro para os logs do evento de pendrive USB. Para desabilitar, "0"; para habilitar, "1".
- **network** *(int)* : Habilita o filtro para os logs do evento de rede. Para desabilitar, "0"; para habilitar, "1".
- **time** *(int)* : Habilita o filtro para os logs do evento de horário do dispositivo. Para desabilitar, "0"; para habilitar, "1".
- **online** *(int)* : Habilita o filtro para os logs do evento de modo online. Para desabilitar, "0"; para habilitar, "1".
- **menu** *(int)* : Habilita o filtro para os logs do evento de menu. Para desabilitar, "0"; para habilitar, "1".
- **boot** *(int)* : Habilita o filtro para os logs do evento de inicialização. Para desabilitar, "0"; para habilitar, "1".
- **push** *(int)* : Habilita o filtro para os logs do evento do servidor push. Para desabilitar, "0"; para habilitar, "1".

*Resposta*

- Documento contendo os 25000 logs de auditoria mais recentes das categorias selecionadas, em inglês, independentemente do idioma em que o equipamento esteja configurado.

#### Exemplo de requisição

Gerando relatório com todos os logs de auditoria:

```
$.ajax({
    url: "/export_audit_logs.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "config": 1,
        "api": 1,
        "usb": 1,
        "network": 1,
        "time": 1,
        "online": 1,
        "menu": 1,
        "boot": 1,
        "push": 1
    })
});
```
