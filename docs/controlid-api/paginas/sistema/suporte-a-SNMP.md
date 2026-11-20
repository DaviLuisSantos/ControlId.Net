<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/suporte-a-SNMP/ -->

# Suporte a SNMP

O protocolo SNMP (*Simple Network Management Protocol*) é utilizado para gerenciar redes, monitorando e administrando dispositivos conectados por meio de seus endereços IP. Ele permite a observação do desempenho da rede, identificação de erros, e coleta de informações para análise.

Um dispositivo habilitado para SNMP é chamado de *agente*, e ele contém informações sobre seu status, chamadas de objetos. Cada um desses objetos tem um identificador exclusivo chamado OID (*Object Identifier*), que segue uma hierarquia numérica, organizada de forma semelhante à estrutura de um endereço IP, mas com uma função diferente. Os OIDs e suas descrições estão armazenados em um arquivo MIB (*Management Information Base*), que define os objetos que podem ser gerenciados via SNMP.

## MIB file

As informações dos identificadores OID (*Object Identifier*) a serem coletados de um equipamento são descritas de forma hierárquica dentro de um MIB file. Essa hierarquia é representada por uma sequência de números, onde cada ponto (.) separa os diferentes níveis da estrutura hierárquica, e cada número identifica uma ramificação ou nó específico.

Por padrão SNMP, o OID segue a estrutura iniciada com 1.3.6.1.4.1, que corresponde à sequência *iso.org.dod.internet.private.enterprises*. Dentro dessa estrutura, organizações podem ter seu próprio domínio específico, atribuído pela IANA. A ControlID, por exemplo, possui o domínio 49617, que se ramifica para .1 (*accessControl*). A partir dessa ramificação, outras subdivisões podem ser definidas conforme a necessidade, totalizando 10 subníveis adicionais que descrevem funcionalidades ou dados específicos dos dispositivos gerenciados:

| Nome | OID | Descrição |
| --- | --- | --- |
| cidSystem | .1 | [Sistema](../sistema/suporte-a-SNMP.md#sistema) |
| cidOperationMode | .2 | [Modo de Operação](../sistema/suporte-a-SNMP.md#modo-de-operacao) |
| cidAntipassback | .3 | [Anti PassBack](../sistema/suporte-a-SNMP.md#anti-passback) |
| cidNetwork | .4 | [Network](../sistema/suporte-a-SNMP.md#network) |
| cidBuzzer | .5 | [Buzzer](../sistema/suporte-a-SNMP.md#buzzer) |
| cidSip | .6 | [SIP](../sistema/suporte-a-SNMP.md#sip) |
| cidApplication | .7 | [Aplicação](../sistema/suporte-a-SNMP.md#aplicacao) |
| cidStreaming | .8 | [Streaming](../sistema/suporte-a-SNMP.md#streaming) |
| cidAlarm | .9 | [Alarme](../sistema/suporte-a-SNMP.md#alarme) |
| cidSecbox | .10 | [Secbox](../sistema/suporte-a-SNMP.md#secbox) |

Os parâmetros possíveis de serem monitorados são:

### Sistema

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| firmwareVersion | .1 | OCTET STRING | Versão de Firmware do dispositivo |
| serialNumber | .2 | OCTET STRING | Número de Serial do dispositivo |
| hasProVersion | .3 | INTEGER   1: true   2: false | Verifica se o dispositivo possui licença Pro |
| loadAverage | .4 | OCTET STRING | Média de carregamento do sistema |
| cpuUsage | .5 | OCTET STRING | Uso de CPU do sistema\* |
| cpuTemperature | .6 | Unsigned32 | Temperatura da CPU   Unidade: T \* 1000 °C |
| dateAndTime | .7 | OCTET STRING | Data e Hora do dispositivo |
| ntpEnabled | .8 | INTEGER   1: True   2: False | NTP Habilitado |
| ntpServer | .9 | OCTET STRING | Servidor(es) NTP |

### Modo de Operação

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| isDeviceOnline | .1 | INTEGER   1: True   2: False | Verifica se o dispositivo está online |
| devicePort | .2 | Unsigned32 | Porta do dispositivo |

### Anti PassBack

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| antipassbackEnabled | .1 | INTEGER   1: True   2: False | Verifica se o antipassback está ativado |
| antipassbackTimeout | .2 | Unsigned32 | Valor do timeout do Antipassback   Unidade: Segundos |
| antipassbackMode | .3 | OCTET STRING | Modo de operação do Antipassback |

### Network

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| dhcpEnabled | .1 | INTEGER   1: True   2: False | Verifica se o DHCP está habilitado |
| duplexMode | .2 | OCTET STRING | Modo duplex do dispositivo   Opções:   (Half, Full) |

### Buzzer

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| audioNotIdentified | .1 | OCTET STRING | Status de aúdio para: Não identificado   Opções:   (custom, default, disabled) |
| audioAuthorized | .2 | OCTET STRING | Status de aúdio para: Autorizado   Opções:   (custom, default, disabled) |
| audioNotAuthorized | .3 | OCTET STRING | Status de aúdio para: Não autorizado   Opções:   (custom, default, disabled) |
| audioMaskPresent | .4 | OCTET STRING | Status de aúdio para: Máscara presente   Opções:   (custom, default, disabled) |
| audioVolume | .5 | Unsigned32 | Volume das mensagens de aúdio |

### SIP

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| sidEnabled | .1 | INTEGER   1: True   2: False | Verificar se o SIP está ativado |
| sipOperationMode | .2 | INTEGER  **1: p2p   2: client** | Modo de operação do SIP |
| sipDialingMode | .3 | INTEGER | Discagem do modo SIP |
| sipBranch | .4 | OCTET STRING | Branch do SIP |
| sipServer | .5 | OCTET STRING | Endereço de servidor de SIP |
| sipAutoAnswerEnabled | .6 | INTEGER   1: True   2: False | Verifica se a resposta automática do SIP está habilitada |
| sipCallButtonEnabled | .7 | INTEGER   1: True   2: False | Verifica se o botão de ligação do SIP está habilitado |
| sipAutoCallId | .8 | OCTET STRING | ID da ligação automática do SIP |
| sipMaxCallTime | .9 | Unsigned32 | Tempo máxima de ligação SIP   Unidade: Segundos |
| sipHasCustomAudio | .10 | INTEGER   1: True   2: False | Verifica se o SIP usa aúdio customizado |
| sipKeepAliveInterval | .11 | Unsigned32 | Intervalo do Keepalive do SIP   Unidade: Segundos |
| sipMicVolume | .12 | Unsigned32 | Volume do microfone SIP |
| sipSpeakerVolume | .13 | Unsigned32 | Volume do auto-falante do SIP |
| sipOpenDoorEnabled | .14 | INTEGER   1: True   2: True | Verifica se a porta aberta do SIP está habilitada |
| sipInCall | .15 | INTEGER   1: True   2: False | Verifica se o usuário está em uma chamada |
| sipRegisterTime | .16 | Unsigned32 | Valor do timeout até estabelecer conexão ao servidor |
| sipRegisterStatus | .17 | DisplayString | Status de conexão do servidor SIP |

### Aplicação

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| appNumberOfUsers | .1 | Unsigned32 | Número de usuários registrados em aplicação |
| appHasCompanyLogo | .2 | INTEGER   1: True   2: False | Verifica se o dispositivo está usando uma logo customizada |
| appQrCodeType | .3 | INTEGER   1: Alfanumérico   2: Numérico | Verifica o tipo de QR code usado no dispositivo |
| appFacialIdentification | .4 | INTEGER   1: True   2: False | Verifica se a identificação facial está habilitada |
| appBiometricIdentification | .5 | INTEGER   1: True   2: False | Verifica se a identificação biométrica está habilitada |
| appCardIdentification | .6 | INTEGER   1: True   2: False | Verifica se o cartão de identificação está habilitado |
| appQrCodeIdentification | .7 | INTEGER   1: True   2: False | Verifica se a identificação por QR Code está habilitada |
| appPinIdentification | .8 | INTEGER   1: True   2: False | Verifica se a identificação por PIN está habilitada |

### Streaming

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| onvifEnabled | .1 | INTEGER   1: True   2: False | Verifica se o ONVIF está habilitado |
| onvifPort | .2 | Unsigned32 | Porta do ONVIF |
| rtspEnabled | .3 | INTEGER   1: True   2: False | Verifica se o RTSP está habilitado |
| rtspPort | .4 | Unsigend32 | Porta RTSP |
| rtspRgbIr | .5 | INTEGER   1: rgb   2: infrared | Verifica qual câmera está sendo usada |
| rtspCodec | .6 | OCTET STRING | Codec usado no RTSP |
| rtspResolution | .7 | OCTET STRING | Resolução usada no RTSP |

### Alarme

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| alarmDoorSensorEnabled | .1 | INTEGER   1: True   2: False | Verifica se o sensor de alarma da porta está habilitado |
| alarmDoorSensorDelay | .2 | Unsigned32 | Atraso do sensor de alarme da porta   Unidade: Segundos |
| alarmForcedAccess | .3 | INTEGER   1: True   2: False | Verifica se houve uma tentativa de acesso forçada |
| alarmPanicFingerEnabled | .4 | INTEGER   1: True   2: False | Verifica se o botão do pânico está habilitado |
| alarmPanicFingerDelay | .5 | Unsigned32 | Atraso do botão do pânico   Unidade: Segundos |
| alarmPanicCardEnabled | .6 | INTEGER   1: True   2: False | Verifica se cartão do pânico está habilitado |
| alarmDeviceViolationEnabled | .7 | INTEGER   1: True   2: False | Verifica se houve uma detecção de violação do dispositivo |
| alarmBuzzerEnabled | .8 | INTEGER   1: True   2: False | Verifica se o alarme do buzzer está habilitado |
| alarmPlayingTimeout | .9 | Unsigned32 | Timeout para o alarme terminar de tocar   Unidade: Segundos |

### Secbox

| Nome | OID | Retorno | Descrição |
| --- | --- | --- | --- |
| secboxCount | .1 | Unsigned32 | Número de secboxes |
| secboxExceptionMode | .2 | OCTET STRING | Verifica se existe um modo de excessão |
| secboxId | .3 | Unsigned32 | ID da Secbox |
| secboxEnabled | .4 | INTEGER   1: True   2: False | Verifica se a secbox está habilitada |
| secboxVersion | .5 | OCTET STRING | Versão de firmware da Secbox |
| secboxRelayTimeout | .6 | Unsigned32 | TImeout do relé da Secbox   Unidade: Milisegundos |
| secboxDoorEnabled | .7 | INTEGER   1: True   2: False | Verifica se o sensor de porta da Secbox está habilitada |
| secboxDoorNormallyOpen | .8 | INTEGER   1: Normalmente Aberto   2: Normalmente Fechado | Verifica se a porta está no modo Normalmente Aberto ou Normalmente Fechado   Opções: |
| secboxAutoCloseEnabled | .9 | INTEGER   1: True   2: False | Verifica se o fechamento automático está habilitado |

Todos estes dados obtidos podem ser adquiridos por meio do arquivo [CONTROLID-MIB.txt](https://www.controlid.com.br/CONTROLID-MIB.txt), disponibilizado pela Control iD.

Nota-se que, por se tratar de um arquivo MIB, não é possível realizar requests SNMP pelo *(Postman)*. Ou seja, é necessário instalar uma ferramenta de gerenciamento de dispositivos via SNMP, como o [MIB Browser](https://www.manageengine.com/products/mibbrowser-free-tool/download.html), por exemplo. Dentro da ferramenta de gerenciamento via SNMP, basta executar o comando *(snmpwalk)* sobre o parâmetro escolhido para ser retornado. Esses parâmetros podem ser encontrados nas ramificações criadas de forma hierárquica.

### Exemplos de equivalência de OID

Como mencionado anteriormente, todos os dados OID conterão no início a sequência **1.3.6.1.4.1.49617.1**, equivalente a *iso.org.dod.internet.private.enterprises.controlId.accessControl*, e no fim o número **0**, equivalente ao *scalar* do OID.

Nos dois exemplos a seguir, a numeração em negrito se refere ao dado de interesse.

#### firmwareVersion

- 1.3.6.1.4.1.49617.1.**1.1**.0

*cidSystem > firmwareVersion*.

#### appHasCompanyLogo

- 1.3.6.1.4.1.49617.1.**7.2**.0

*cidApplication > appHasCompanyLogo*.

## Habilitar/Desabilitar SNMP via API

A função descrita abaixo deve ser usada para realizar a ativação/desativação do SNMP nos terminais de controle de acesso da Control iD.

### POST /set_configuration.fcgi

*Parâmetros*

- **snmp_enabled** *(int)*: Define se o ativa ou desabilita o protocolo SNMP

#### Exemplo de requisição para ativar o protocolo SNMP

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "snmp_agent": {
            snmp_enabled: "1"
        }"
    })
});
```

#### Exemplo de requisição para desativar o protocolo SNMP

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "snmp_agent": {
            snmp_enabled: "0"
        }"
    })
});
```
