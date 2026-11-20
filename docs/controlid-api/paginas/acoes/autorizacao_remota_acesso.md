<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/autorizacao_remota_acesso/ -->

# Autorização Remota de Acesso

Este tópico descreve como autorizar remotamente o acesso de um usuário em tempo real e com interação com o usuário, ou seja, o nome do usuário será exibido na tela além de uma foto caso desejado.

### POST /remote_user_authorization.fcgi

*Parâmetros*

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **event** | *int* | Tipo do evento, pode ser:  1. Equipamento inválido 2. Parâmetros de identificação inválidos 3. Não identificado 4. Identificação pendente 5. Tempo de identificação esgotado 6. Acesso negado 7. Acesso concedido 8. Acesso pendente (usado quando o acesso depende de mais de uma pessoa) 9. Usuário não é administrador (usado quando um usuário tenta acessar o menu mas não é administrador) 10. Acesso não identificado (quando o portal é aberto através da API e o motivo não é informado) 11. Acesso por botoeira 12. Acesso pela interface web 13. Desistência de entrada (*exclusivo para iDBlock*) 14. Sem resposta (nenhuma ação é tomada) 15. Acesso pela interfonia (*exclusivo para iDFace*)   Valores exclusivos dos **equipamentos da linha iDAccess**:  - 16: Acesso via OSDP (a partir da versão V5.9.4) - 17: Acesso negado por intertravamento (a partir da versão V5.16.0) - 18: Liberação pelo lado oposto (a partir da versão V5.20.0) |
| **user_id** | *int* | ID do usuário, em caso de identificação. |
| **user_name** | *string* | Nome do usuário, em caso de identificação. |
| **user_image** | *bool* | Usuário identificado possui ou não foto. |
| **portal_id** | *string* | ID do portal correspondente. |
| **actions** | *Array de Objetos JSON* | Ações que devem ser executas pelo equipamento. Exemplo: [ {"action":"door", "parameters":"door=1"}, {"action":"door", "parameters":"door=2"} ] |

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo iDAccess/iDFit/iDBox:

```
$.ajax({
    url: "/remote_user_authorization.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            event:7,
            user_id:6,
            user_name:"Brian Cox",
            user_image:false,
            portal_id:1,
            actions:[ {action: "door", parameters: "door=1"} ]
        }
    )
});
```

#### Exemplo iDFlex/iDAccess Pro/iDAccess Nano:

```
$.ajax({
    url: "/remote_user_authorization.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            event:7,
            user_id:6,
            user_name:"Brian Greene",
            user_image:false,
            portal_id:1,
            actions:[ {action: "sec_box", parameters: "id=65793, reason=1"} ]
        }
    )
});
```

#### Exemplo catraca iDBlock:

```
$.ajax({
    url: "/remote_user_authorization.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            event:7,
            user_id:6,
            user_name:"Neil deGrasse Tyson",
            user_image:false,
            portal_id:1,
            actions:[ {action: "catra", parameters: "allow=clockwise"} ]
        }
    )
});
```
