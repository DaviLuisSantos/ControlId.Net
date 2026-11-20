<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/servidor-ntp/ -->

# Servidor NTP

O Network Time Protocol (NTP) permite que os dispositivos sincronizem seus relógios internos com servidores de tempo na rede. Os endpoints a seguir permitem configurar e verificar o status do servidor NTP utilizado pelo equipamento.

## Obter servidor NTP

Retorna os endereços dos servidores NTP atualmente configurados.

### GET /get_ntp_server.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **server1** *(string)*: Endereço do servidor NTP primário.
- **server2** *(string)*: Endereço do servidor NTP secundário. Presente apenas se um segundo servidor estiver configurado.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_ntp_server.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "server1": "pool.ntp.org",
    "server2": "time.google.com"
}
```

## Definir servidor NTP

Configura os endereços dos servidores NTP utilizados para sincronização de horário.

### POST /set_ntp_server.fcgi

*Parâmetros*

- **server1** *(string)*: **Obrigatório.** Endereço do servidor NTP primário.
- **server2** *(string)*: Endereço do servidor NTP secundário. Opcional.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_ntp_server.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        server1: "pool.ntp.org",
        server2: "time.google.com"
    })
});
```

## Obter status do servidor NTP

Testa a conectividade com o servidor NTP configurado e retorna o resultado. Esta é uma chamada bloqueante — a resposta só é retornada após a tentativa de conexão ser concluída, com timeout máximo de 4 segundos.

### GET /get_ntp_server_status.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **server_status** *(bool)*: `true` se a conexão com o servidor NTP foi bem-sucedida, `false` caso contrário.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_ntp_server_status.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "server_status": true
}
```
