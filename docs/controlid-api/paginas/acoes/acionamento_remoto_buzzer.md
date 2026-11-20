<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/acionamento_remoto_buzzer/ -->

# Acionamento Remoto Buzzer

Este tópico descreve como acionar remotamente o buzzer dos dispositivos de acesso.

### POST /buzzer_buzz.fcgi

*Parâmetros*

- **frequency** *(int)* : Frequência do sinal em hertz (Hz).
- **duty_cycle** *(int)* : Ciclo de trabalho, ou seja, a fração de tempo em que o sinal de acionamento do *buzzer* ficará ativo.
- **timeout** *(int)* : Tempo de acionamento do buzzer em milissegundos (max. 3000 por requisição).

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo:

```
$.ajax({
    url: "/buzzer_buzz.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ frequency: 4000, duty_cycle: 50, timeout: 1000 })
});
```
