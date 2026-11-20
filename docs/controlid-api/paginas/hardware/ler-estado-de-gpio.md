<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/hardware/ler-estado-de-gpio/ -->

# Ler estado de GPIO

Lê o estado de um pino de [GPIO](http://pt.wikipedia.org/wiki/General_Purpose_Input/Output) especificado pelo parâmetro *gpio*. O método HTTP usado é o POST.

### POST /gpio_state.fcgi

*Parâmetros*

- **gpio** *(int)* : Número do pino de propósito geral (GPIO) cujo estado deseja-se ler.

  Para iDFit/iDAccess:

  > - pino 0 - Zona de alarme 1
  > - pino 1 - Zona de alarme 2
  > - pino 2 - Zona de alarme 3
  > - pino 3 - Zona de alarme 4
  > - pino 4 - Zona de alarme 5
  > - pino 5 - Sensor de porta 1
  > - pino 6 - Sensor de porta 2
  > - pino 7 - Botoeira 1
  > - pino 8 - Botoeira 2
  > - pino 9 - Saída de alarme
  > - pino 10 - LED da biometria
  > - pino 11 - Relé 1
  > - pino 12 - Relé 2
  > - pino 13 - Sensor de violação
  > - pino 20 - Saída Wiegand 1
  > - pino 21 - Saída Wiegand 0
  > - pino 22 - Entrada Wiegand 1
  > - pino 23 - Entrada Wiegand 0

  Para iDFlex/iDAccess Pro/iDAccess Nano:

  > - pino 0 - Sensor de violação

  Para iDFace:

  > - pino 0 - Sensor de violação
  > - pino 1 - Botoeira para ligação SIP

*Resposta*

- **enabled** *(int)* : Indica se o GPIO está habilitado (1) ou desabilitado (0). Uma tentativa de leitura ou escrita em um GPIO desabilitado possui efeito indefinido.
- **in** *(int)* : Indica se o GPIO está configurado como pino de entrada (1) ou saída (0).
- **pin** *(string)* : Contém o nome do GPIO.
- **idle** *(int)* : Contém o valor do GPIO quando ocioso.
- **pullup** *(int)* : Indica se o pullup do GPIO está habilitado (1) ou desabilitado (0).
- **value** *(int)* : Contém o valor atual do GPIO.
- **notify** *(int)* : Indica se o software de acesso será avisado (1) de alterações nesse GPIO ou não (0).
- **notified** *(int)* : Indica quantas vezes o GPIO notificou o software de acesso de uma alteração.

#### Exemplo de requisição

Lendo o estado do GPIO 1:

```
$.ajax({
    url: "/gpio_state.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        gpio: 1
    })
});
```
