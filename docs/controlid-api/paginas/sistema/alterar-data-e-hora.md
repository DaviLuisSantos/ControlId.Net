<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/alterar-data-e-hora/ -->

# Alterar data e hora

Altera a data e a hora do sistema. O método HTTP usado é o **POST**.

### POST /set_system_time.fcgi

*Parâmetros*

- **day** *(int)* : Valor numérico representando o dia do mês.
- **month** *(int)* : Valor numérico representando o mês. Valores válidos entre 1 e 12.
- **year** *(int)* : Valor numérico representando o ano. Exemplo: 2014.
- **hour** *(int)* : Valor numérico representando a hora. Valores válidos entre 0 e 23.
- **minute** *(int)* : Valor numérico representando os minutos. Valores válidos entre 0 e 59.
- **second** *(int)* : Valor numérico representando os segundos. Valores válidos entre 0 e 59.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Alterar a data e a hora para 21:30 de 10/12/1983:

```
$.ajax({
    url: "/set_system_time.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        day: 10,
        month: 12,
        year: 1983,
        hour: 21,
        minute: 30,
        second: 00
    })
});
```

### Suporte a NTP

O Network Time Protocol (NTP) é um protocolo utilizado para sincronizar relógios de computadores em uma rede. Ele permite que computadores obtenham a hora exata de servidores NTP, ajustando automaticamente seus relógios para minimizar a discrepância de tempo entre eles.

A funcionalidade implementada no equipamento permite a importação do "time-zone data" a fim de obter referências locais de fusos horários. Além disso, é possível inserir manualmente o horário de verão (para os países que adotaram a medida).

O módulo responsável por essa configuração é o `ntp`. Ele pode ser configurado via interface WEB, API ou GUI. Nele, é possível alterar dois parâmetros: `enabled` que habilita o protocolo (valores "0" para desabilitado e "1" para habilitado) e `timezone` que define o horário local (que recebe valores de "UTC-12" a "UTC+12") em relação ao Tempo Universal Coordenado (UTC).

Vale ressaltar que o protocolo NTP possui suporte a todos os dispositivos da linha de acesso, ou seja, dispositivos com firmware V5 e V6.

### Exemplo de requisição

Esta requisição habilita o protocolo ntp com UTC+3.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "ntp": {
                "enabled": "1",
                "timezone": "UTC+3"
            }
        }
    )
});
```
