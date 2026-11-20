<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/abertura-remota-porta-e-catraca/ -->

# Abertura Remota Porta e Catraca

Para abrir remotamente uma porta, ou permitir o giro de uma catraca, será necessário utilizar o *endpoint* descrito abaixo "execute actions".

## Executar ação

Executa uma ação no equipamento. O método HTTP usado é o POST. Verificar no exemplo no fim desta página como usá-lo.

### POST /execute_actions.fcgi

*Parâmetros*

- **actions** *(array de objetos JSON)* : Cada elemento deste array possui 2 chaves, ambas strings: action e parameters. action é uma ação a ser executada e parameters são os parâmetros dessa ação.
  > - **action** *(string)* : Ação a ser executada, as ações disponíveis são:
  >   > - *door* : Ação para abrir portas.
  >   > - *sec_box* : Ação para abrir portas em dispositivos que utilizam o módulo de acionamento externo (SecBox).
  >   > - *open_collector* : Ação para abrir o coletor (urna) da catraca iDBlock.
  >   > - *catra* : Ação para liberar a catraca para um dos lados ou ambos.
  > - **parameters** *(string)* : Parâmetros das ações acima, maiores detalhes nos exemplos abaixo.

*Resposta*

- Se o intertravamento via rede estiver habilitado, a chamada retornará informando se a ação foi permitida e executada. Caso contrário, não haverá retorno para esta chamada.

*Exemplos de resposta:*

Dispositivos conectados à SecBox, quando todas as portas de dispositivos remotos cadastrados no intertravamento via rede estiverem fechadas:

```
"actions": [
                {
                    "action": "sec_box",
                    "status": "allowed"
                }
            ]
```

Dispositivos conectados à SecBox, quando qualquer porta de um dispositivo remoto cadastrado no intertravamento via rede estiver aberta:

```
"actions": [
                {
                    "action": "sec_box",
                    "status": "denied"
                }
            ]
```

#### Exemplo Abrir Relé iDAccess/iDFit/iDBox/iDUHF:

```
$.ajax({
    url: "/execute_actions.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        actions: [ { action: "door", parameters: "door=1" } ]
    })
});
```

#### Exemplo Abrir Relé iDFlex/iDAccess Pro/iDAccess Nano/iDUHF:

```
$.ajax({
    url: "/execute_actions.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        actions: [ { action: "sec_box", parameters: "id=65793, reason=3" } ]
    })
});
```

#### Exemplo Abrir Coletor (urna) Catraca

Abre o coletor (urna) da iDBlock, não recebe nenhum parâmetro:

```
$.ajax({
    url: "/execute_actions.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        actions: [ { action: "open_collector", parameters: "" } ]
    })
});
```

#### Libera a catraca para um lado

Esta ação recebe um parâmetro (allow="direction"), onde "direction" deve ser "anticlockwise", "clockwise" ou "both". A catraca será liberada para o sentido anti-horário, horário ou ambos respectivamente. O exemplo abaixo libera a catraca para o sentido horário:

```
$.ajax({
    url: "/execute_actions.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        actions: [ { action: "catra", parameters: "allow=clockwise" } ]
    })
});
```

#### Ativa os relés da catraca

Esta ação recebe um parâmetro "relay", que deve ser igual à "1" ou "2".

```
$.ajax({
    url: "/execute_actions.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        actions: [ { action: "catra", parameters: "relay=1" } ]
    })
});
```

## Verificar o estado das portas do equipamento (aberta / fechada)

Obtém o estado das portas do equipamento (aberta / fechada). O método HTTP usado é o POST.

### POST /doors_state.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Exemplo de Respostas:*

*Dispositivos com SecBox conectados:*

```
"sec_boxes": [
                {
                    "id": 122641794705017745,
                    "open": false
                },
                {
                    "id": 65793,
                    "open": false
                }
             ]
```

*Dispositivos com relés conectados:*

```
"doors": [
            {
                "id": 1,
                "open": false
            },
            {
                "id": 2,
                "open": false
            }
         ]
```

*Dispositivo sem relé conectado:*

```
"error" : "Sec box count message parameter is invalid",
"code" : 1
```

Nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**, quando não há SecBox nem porta detectados, a mensagem retornada é `"Fail to retrieve doors states"`. Também pode ser retornada a mensagem `"Door count message parameter is invalid"`, em caso de falha ao obter a quantidade de portas.

#### Exemplo de requisição

```
$.ajax({
    url: "/doors_state.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```
