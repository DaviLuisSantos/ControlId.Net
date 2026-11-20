# Objetos

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Alarm logs

### Carregar logs do alarm

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "alarm_logs"
}
```

## Regras de acesso

### Criar regras de acesso

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "access_rules",
	"values": [
		{
			"name": "Happy Hour",
			"type": 1,
			"priority": 0
		}
	]
}
```

### Carregar regras de acesso

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "access_rules"
}
```

## Regras de acesso time zones

### Criar regra de acesso time zones

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "access_rule_time_zones",
	"values": [
		{
			"access_rule_id": 3,
			"time_zone_id": 2
		}
	]
}
```

### Carregar regras de acesso time zones

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "access_rule_time_zones"
}
```

## Ações

### Carregar ações

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "actions"
}
```

## Logs de acesso

### Carregar logs de acesso

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "access_logs"
}
```

## Cartões

### Criar cartões

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "cards",
	"values": [
		{
			"value": 0253490750,
			"user_id": 1
		}
	]
}
```

### Carregar cartões

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "cards",
	"where": {
		"cards": {
			"value": 64424543611
		}
	}
}
```

### Deletar cartões

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "cards",
	"where": {
		"cards": {
			"value": 64424543611
		}
	}
}
```

## QR Codes

### Criar qrcodes

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "qrcodes",
	"values": [
		{
			"value": "abc123",
			"user_id": 1
		}
	]
}
```

### Carregar qrcodes

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "qrcodes",
	"where": {
		"qrcodes": {
			"value": "abc123"
		}
	}
}
```

### Deletar qrcodes

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "qrcodes",
	"where": {
		"qrcodes": {
			"value": "abc123"
		}
	}
}
```

## Modificar logs

### Carregar logs modificados

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "change_logs"
}
```

## Tabelas customizadas

### Criar tabelas customizadas

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "custom_tables",
	"values": [
		{
			"table_name": "my_custom_table",
			"name": "My Custom Table"
		}
	]
}
```

### Carregar tabelas customizadas

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "custom_tables"
}
```

## Colunas customizadas

### Criar colunas customizadas

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "custom_columns",
	"values": [
       {
            "custom_table_id": 1,
            "column_name": "my_custom_column",
            "name": "My Custom Column"
        }
	]
}
```

### Carregar colunas customizadas

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "custom_columns"
}
```

## Dispositivos

### Criar dispositivos

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "devices",
	"values": [
		{
			"name": "Node Server",
			"ip": "http://192.168.6.217:3000",
			"public_key": ""
		}
	]
}
```

### Atulaizar dispositivos

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "devices",
	"values": {
		"ip": "192.168.127.216:5000"
	},
	"where": {
		"devices": {
			"id": 34356
		}
	}
}
```

### Carregar dispositivos

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "devices"
}
```

## Regras de acesso para grupos

### Criar regras de acesso para grupos

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "group_access_rules",
	"values": [
		{
			"group_id": 2,
			"access_rule_id": 3
		}
	]
}
```

### Atualizar regras de acesso para grupos

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "group_access_rules",
	"values": {
		"access_rule_id": 4
	},
	"where": {
		"group_access_rules": {
			"group_id": 1
		}
	}
}
```

### Carregar regras de acesso para grupos

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "group_access_rules"
}
```

## Regras de acesso para liberações agendadas

### Criar regras de acesso para liberações agendadas

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "scheduled_unlock_access_rules",
	"values": [
		{
			"scheduled_unlock_id": 2,
			"access_rule_id": 3
		}
	]
}
```

### Atualizar regras de acesso para liberações agendadas

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "scheduled_unlock_access_rules",
	"values": {
		"access_rule_id": 4
	},
	"where": {
		"scheduled_unlock_access_rules": {
			"scheduled_unlock_id": 1
		}
	}
}
```

### Carregar regras de acesso para liberações agendadas

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "scheduled_unlock_access_rules"
}
```

## Grupos

### Criar grupos

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "groups",
	"values": [
		{
			"name": "Grupo Teste"
		}
	]
}
```

### Carregar grupos

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "groups"
}
```

### Deletar grupos

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "groups",
	"where": {
		"groups": {
			"id": 3
		}
	}
}
```

## Liberações agendadas

### Criar liberações agendadas

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "scheduled_unlocks",
	"values": [
		{
			"name": "Liberação Agendada Teste",
			"message": "Mensagem de Liberação Agendada Teste"
		}
	]
}
```

### Carregar liberações agendadas

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "scheduled_unlocks"
}
```

### Deletar liberações agendadas

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "scheduled_unlocks",
	"where": {
		"scheduled_unlocks": {
			"id": 3
		}
	}
}
```

## Feriados

### Carregar feriados

`POST {{host}}/load_objects.fcgi?session={{session}}&user_id=10`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "holidays"
}
```

## Portal access rules

### Create portal access rules

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "portal_access_rules",
	"values": [
		{
			"portal_id": 1,
			"access_rule_id": 4
		}
	]
}
```

### Load portal access rules

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "portal_access_rules"
}
```

## Regras de acesso ao portal

### Carregar ações do portal

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "portal_actions"
}
```

### Atualizar ações do portal

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "portal_actions",
	"values": {
		"action_id": 3
	},
	"where": {
		"portal_actions": {
			"action_id": 4
		}
	}
}
```

## Portals

### Carregar portals

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "portals"
}
```

## Templates

### Criar templates

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "templates",
	"values": [
		{
			"id": 1,
			"finger_type": 0,
			"template": "base64",
			"user_id": 6
		}
	]
}
```

### Carregar templates

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "templates"
}
```

### Deletar templates

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "templates"
}
```

## Time spans

### Criar time spans

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "time_spans",
	"values": [
		{
			"time_zone_id": 2,
			"start": 72000,
			"end": 86400,
			"sun": 0,
			"mon": 0,
			"tue": 0,
			"wed": 0,
			"thu": 0,
			"fri": 1,
			"sat": 0,
			"hol1": 0,
            "hol2": 0,
            "hol3": 0
		}
	]
}
```

### Atualizar time spans

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "time_spans",
	"values": {
		"start": 64800,
		"end": 82800
	},
	"where": {
		"time_spans": {
			"id": 2
		}
	}
}
```

### Carregar time spans

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "time_spans"
}
```

## Time zones

### Criar time zones

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "time_zones",
	"values": [
		{
			"name": "Fim de Semana"
		}
	]
}
```

### Carregar time zones

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "time_zones"
}
```

## Regras de acesso para usuário

### Criar regras de acesso para usuário

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "user_access_rules",
	"values": [
		{
			"user_id": 3,
			"access_rule_id": 3
		}
	]
}
```

### Carregar regras de acesso do usuário

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "user_access_rules"
}
```

## Grupos de usuários

### Criar grupo de usuários

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "user_groups",
	"fields": ["user_id", "group_id"],
	"values": [
		{
			"user_id": 1,
			"group_id": 1
		}
	]
}
```

### Carregar grupo de usuários

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "user_groups"
}
```

### Deletar grupo de usuários

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "user_groups"
}
```

## Usuários

### Criar usuários

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "users",
	"values": [
		{
			"name": "Douglas Miguel",
			"registration": "",
			"password": "",
			"salt": ""
		}
	]
}
```

### Criar ou modificar usuários

`POST {{host}}/create_or_modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "users",
	"values": [
		{
			"id": 1,
            "name": "Valter Branco",
			"registration": "",
			"password": "",
			"salt": ""
		}
	]
}
```

### Atualizar usuários

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "users",
	"values": {
		"begin_time": 0,
		"end_time": 1717690651
	},
	"where": {
		"users": {
			"id": 7
		}
	}
}
```

### Carregar usuários

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ 
	"object": "users"
}
```

### Lista de usuários sem foto cadastrada

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Corpo:**

```json
{ 
	"object": "users",
    "where": [
        {
			"object" : "users",
			"field" : "image_timestamp",
			"operator" : "IS NULL",
			"value" : "",
			"connector" : "OR"
        },
        {
			"object" : "users",
			"field" : "image_timestamp",
			"operator" : "=",
			"value" : 0
        }
    ]
}
```

### Lista de usuários com foto cadastrada

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Corpo:**

```json
{ 
	"object": "users",
    "where": [
        {
			"object" : "users",
			"field" : "image_timestamp",
			"operator" : "!=",
			"value" : 0
        }
    ]
}
```

### Deletar usuários

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "users"
}
```

## Relatórios

### Relatório AFD

`POST {{host}}/export_afd.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Exemplo de resposta — Relatório AFD extraido com sucesso** (200 OK):

```
0000000001100000000000000000000000000ControliD                                                                                                                                             0061998765598336122102021081120210811202110042EE4
0000000013221020211555000000000001BF30
00000000232210202116030000000000023C34
00000000332210202116140000000000038506
000000004322102021170900000000000675FA
00000000532910202114440000000000064164
000000006329102021155500000000000540EC
00000000732910202115560000000000057C11
0000000083291020211638000000000005F566
0000000093031120211055000000000006261A
0000000103031120211056000000000006E7D3
0000000113031120211056000000000006182B
00000001230311202110590000000000065734
0000000133031120211059000000000005A98C
0000000143031120211059000000000000D5A2
0000000153031120211100000000000006E403
00000001630311202111040000000000062006
00000001730311202111070000000000061CFB
000000018303112021110900000000000692C9
0000000193031120211111000000000006E42E
0000000203041120211352000000000006B3F9
00000002130511202112180000000000061E10
000000022308112021081000000000000648D7
00000002330811202108140000000000053261
00000002430811202108140000000000054D8F
0000000253081120210814000000000005B277
0000000263081120210823000000000005B674
00000002730811202109030000000000068A0E
9999999990000000000000000270000000000000000000000000009
0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
```

**Exemplo de resposta — Falha de extração do relatório AFD** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

### Relatório AFD com formato 671

`POST {{host}}/export_afd.fcgi?session={{session}}&mode=671`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "initial_date":{
        "day":11,
        "month":7,
        "year":2022
    },
	"initial_nsr": 1,
	"mode": 671
}
```

**Exemplo de resposta — Relatório AFD extraido com sucesso** (200 OK):

```
0000000001100000000000000000000000000ControliD                                                                                                                                             0061998765598336122102021081120210811202110042EE4
0000000013221020211555000000000001BF30
00000000232210202116030000000000023C34
00000000332210202116140000000000038506
000000004322102021170900000000000675FA
00000000532910202114440000000000064164
000000006329102021155500000000000540EC
00000000732910202115560000000000057C11
0000000083291020211638000000000005F566
0000000093031120211055000000000006261A
0000000103031120211056000000000006E7D3
0000000113031120211056000000000006182B
00000001230311202110590000000000065734
0000000133031120211059000000000005A98C
0000000143031120211059000000000000D5A2
0000000153031120211100000000000006E403
00000001630311202111040000000000062006
00000001730311202111070000000000061CFB
000000018303112021110900000000000692C9
0000000193031120211111000000000006E42E
0000000203041120211352000000000006B3F9
00000002130511202112180000000000061E10
000000022308112021081000000000000648D7
00000002330811202108140000000000053261
00000002430811202108140000000000054D8F
0000000253081120210814000000000005B277
0000000263081120210823000000000005B674
00000002730811202109030000000000068A0E
9999999990000000000000000270000000000000000000000000009
0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
```

**Exemplo de resposta — Falha de extração do relatório AFD** (401 Unauthorized):

```json
{
    "error": "Invalid session",
    "code": 1
}
```

### Gerar Relatório

`POST {{host}}/report_generate.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "order":["ascending","name"],
    "where":{
            "users":{},
            "groups":{},
            "time_zones":{}
    },
    "object":"users",
    "delimiter":";",
    "line_break":"\\r\\n",
    "header":"Nome (Usuário);Código (Usuário)",
    "file_name":"",
    "join":"LEFT",
    "columns":[
        {
        "type":"object_field",
        "object":"users",
        "field":"name",
        "format":{
            "adjustment":1,
            "width":50,
            "fill":" ",
            "format":"%s"
            }
        },
        {
        "type":"object_field",
        "object":"users",
        "field":"id",
        "format":{
            "adjustment":2,
            "width":5,
            "fill":"0",
            "format":"%s"
            }
        }
    ]
}
```

### Relatório AFD com filtro de data inicial

`POST {{host}}/export_afd.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "initial_date":{
        "day":11,
        "month":7,
        "year":2022
    }
}
```

### Relatório AFD com filtro NSR

`POST {{host}}/export_afd.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "initial_nsr" : 1
}
```

## User roles

### Criar copia de usuário

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"object": "users",
	"values": [
		{
			"name": "Douglas Miguel",
			"registration": "",
			"password": "",
			"salt": ""
		}
	]
}
```

## Regras de intertravamento via rede

### Criar regra

`POST {{host}}/create_objects.fcgi?session={{session}}`

**Corpo:**

```json
{
	"object": "network_interlocking_rules",
	"values": [
        {
		    "ip": "192.168.0.129",
		    "login": "login",
		    "password": "password",
		    "portal_name": "Portão Serviço",
		    "enabled": 1
	    }
    ]
}
```

### Carregar regras

`POST {{host}}/load_objects.fcgi?session={{session}}`

**Corpo:**

```json
{ 
	"object": "network_interlocking_rules"
}
```

### Modificar regra

`POST {{host}}/modify_objects.fcgi?session={{session}}`

**Corpo:**

```json
{
	"object": "network_interlocking_rules",
	"values": {
		"ip": "192.168.0.129",
		"login": "login",
		"password": "password",
		"portal_name": "Portão Serviço",
		"enabled": 1
	},
	"where": {
		"network_interlocking_rules": {
			"id": 1
		}
	}
}
```

### Remover regra

`POST {{host}}/destroy_objects.fcgi?session={{session}}`

**Corpo:**

```json
{
	"object": "network_interlocking_rules",
	"where": {
		"network_interlocking_rules": {
			"id": 1
		}
	}
}
```
