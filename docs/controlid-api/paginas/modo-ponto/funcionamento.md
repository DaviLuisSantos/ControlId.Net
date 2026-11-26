<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/modo-ponto/funcionamento/ -->

# Introdução ao Modo Ponto

O *Modo Ponto* é projetado para que o equipamento de controle de acesso seja utilizado para [controle de ponto](https://www.controlid.com.br/relogio-de-ponto/). Nesse modo, funções de controle de acesso ficam desabilitadas. Além disso, é possível selecionar o tipo de batida, como, por exemplo, entrada, saída, intervalo etc.

No *Modo Ponto*, as regras de acesso são desabilitadas. Nessa configuração, caso o usuário esteja cadastrado no banco de dados do equipamento, o mesmo terá sua batida registrada ao se identificar.

Esse modo de operação pode ser utilizado de duas maneiras diferentes:

- **Sem personalização** do tipo de batida de ponto.
- **Com personalização** do tipo de batida de ponto, em que o tipo de batida é totalmente customizável. Nesse caso, o usuário deve selecionar o tipo de batida no display do equipamento antes de se identificar.

## Configuração

Para configurar o modo ponto, os seguintes parâmetros são necessários:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **attendance_mode** | *int* | Habilita (1) ou desabilita (0) o modo ponto. |
| **log_type** | *int* | Determina se é possível (1) ou não (0) personalizar os tipos de batida. |

### Exemplos de requisição

Esta requisição configura o Modo Ponto **sem personalização** de tipo de batida.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "general": {
                "attendance_mode": "1"
            },
            "identifier" : {
                "log_type": "0"
            }
        }
    )
});
```

Esta requisição configura o Modo Ponto **com personalização** de tipo de batida.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "general": {
                "attendance_mode": "1"
            },
            "identifier" : {
                "log_type": "1"
            }
        }
    )
});
```

## Exportação do Relatório com os de Logs de Batida de Ponto

Este tópico apresenta um exemplo de requisição utilizada para exportar um relatório dos logs de batida de ponto, com os tipos de batida personalizados, utilizando a API de exportação de relatórios **/report_generate.fcgi**.

A requisição abaixo gera um relatório contendo os logs das batidas de ponto personalizadas. Para entender melhor os parâmetros utilizados nessa requisição, consulte a seção [Exportar Relatório](../objetos/exportar-relatorio.md), onde todos os campos são detalhados.

### Exemplo de requisição

Gerando o relatório:

```
$.ajax({
  url: "/report_generate.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
      "order": [
            "descending",
            "time"
        ],
        "where": {
            "access_logs": {
                "id": [
                    8,
                    7,
                    6,
                    5,
                    4,
                    3
                ]
            },
            "log_types": {},
            "users": {},
            "groups": {},
            "time_zones": {}
        },
        "object": "access_logs",
        "delimiter": ";",
        "line_break": "\\r\\n",
        "header": ";Data e Hora;ID de Batida;Tipo de Batida;Autorização;Tipo de Identificação;Código (Usuário);Nome (Usuário);Matrícula (Usuário);Nome (Portal)",
        "file_name": "",
        "join": "LEFT",
        "columns": [
            {
                "type": "object_field",
                "object": "access_logs",
                "field": "time",
                "format": {
                    "adjustment": 0,
                    "width": 0,
                    "fill": "0",
                    "format": "%d/%m/%Y %H:%M:%S"
                }
            },
            {
                "type": "object_field",
                "object": "log_types",
                "field": "id",
                "format": {
                    "adjustment": 2,
                    "width": 4,
                    "fill": "0",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "log_types",
                "field": "name",
                "format": {
                    "adjustment": 1,
                    "width": 50,
                    "fill": " ",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "access_logs",
                "field": "event",
                "format": {
                    "adjustment": 2,
                    "width": 2,
                    "fill": "0",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "access_logs",
                "field": "identifier_id",
                "format": {
                    "adjustment": 1,
                    "width": 1,
                    "fill": " ",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "users",
                "field": "id",
                "format": {
                    "adjustment": 2,
                    "width": 15,
                    "fill": "0",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "users",
                "field": "name",
                "format": {
                    "adjustment": 1,
                    "width": 50,
                    "fill": " ",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "users",
                "field": "registration",
                "format": {
                    "adjustment": 1,
                    "width": 50,
                    "fill": " ",
                    "format": ""
                }
            },
            {
                "type": "object_field",
                "object": "portals",
                "field": "name",
                "format": {
                    "adjustment": 1,
                    "width": 50,
                    "fill": " ",
                    "format": ""
                }
            }
        ]
  })
});
```

O relatório gerado por essa requisição inclui as seguintes colunas:

- **Data e Hora**: A data e hora em que o evento foi registrado no sistema.
- **ID de Batida**: Identificador único do tipo de batida.
- **Tipo de Batida**: O tipo de evento (ex: entrada, almoço, saída, etc.).
- **Autorização**: Indica se o acesso foi permitido ou negado.
- **Tipo de Identificação**: Método de autenticação utilizado (ex: cartão, biometria, facial, etc.).
- **Código (Usuário)**: Identificador único do usuário no sistema.
- **Nome (Usuário)**: Nome do usuário.
- **Matrícula (Usuário)**: Número de matrícula associado ao usuário.
- **Nome (Portal)**: Nome do portal ou local onde a batida foi registrada.
