<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/ferramentas-diagnostico/ -->

# Ferramentas de diagnóstico

Os equipamentos possuem testes para validação da conexão e da estabilidade da conexão com a internet, além de endpoints para obtenção de logs. O método HTTP usado é o **GET** para o teste de conexão e para a obtenção de logs, e **POST** para os testes de ping e nslookup. O **contentType** é **application/json**.

## Teste de conexão

Verifica se o equipamento possui acesso à internet.

### GET /connection_test.fcgi

#### Exemplo de requisição

```
$.ajax({
  url: "/connection_test.fcgi?session=" + session,
  type: 'GET',
  contentType: 'application/json',
  data: JSON.stringify({

  })
});
```

#### Exemplo de resposta

```
{
    "connection_result": false
}
```

## Teste de ping

O teste de ping indica o tempo necessário para que uma pequena quantidade de dados faça uma ida e volta entre o equipamento e o destino informado. Quanto menor o resultado, mais reativa e consistente é a conexão.

### POST /ping_test.fcgi

*Parâmetros*

- **dst** *(string)*: Endereço IP ou nome do host de destino do ping.

#### Exemplo de requisição

```
$.ajax({
  url: "/ping_test.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    "dst": "8.8.8.8"
  })
});
```

#### Exemplo de resposta

```
{
    "output": "PING 8.8.8.8 (8.8.8.8): 56 data bytes\n\n--- 8.8.8.8 ping statistics ---\n4 packets transmitted, 0 packets received, 100% packet loss\n"
}
```

## Teste de nslookup

O teste de nslookup é utilizado para consultar servidores de nomes (DNS), a fim de localizar informações sobre nós de rede, examinar o conteúdo de um banco de dados de servidor de nomes ou verificar a acessibilidade de servidores de nomes.

### POST /nslookup_test.fcgi

*Parâmetros*

- **dst** *(string)*: Nome do host a ser consultado.
- **dns** *(string)*: Endereço do servidor DNS a ser utilizado na consulta.

#### Exemplo de requisição

```
$.ajax({
  url: "/nslookup_test.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    "dst": "www.google.com",
    "dns": "8.8.8.8"
  })
});
```

#### Exemplo de resposta

```
{
  "output_ns": ";; connection timed out; no servers could be reached\n\n"
}
```

## Obter log do equipamento

Retorna o log de aplicação do equipamento em texto simples. Combina o log principal e o log de erros em uma única resposta. Se apenas o log de erros estiver disponível, retorna somente ele.

### GET /get_ac_log.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo de texto simples contendo as entradas do log do equipamento.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_ac_log.fcgi?session=" + session,
    type: 'GET'
});
```

## Obter log WPA

Retorna o log de autenticação WPA (Wi-Fi Protected Access) em texto simples. Dois endpoints cobrem versões de firmware diferentes com o mesmo propósito.

### GET /get_wpa_log.fcgi

Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo de texto simples contendo as entradas do log WPA.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_wpa_log.fcgi?session=" + session,
    type: 'GET'
});
```

### GET /get_wpasupplicant_log.fcgi

Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo de texto simples contendo as entradas do log do WPA supplicant.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_wpasupplicant_log.fcgi?session=" + session,
    type: 'GET'
});
```

## Obter log do OpenVPN NEW!

Retorna o log do cliente OpenVPN em texto puro. Útil para diagnosticar falhas de conexão
da VPN configurada em [Configurações de Rede](../sistema/configuracoes-de-rede.md#openvpn).

### GET /get_openvpn_log.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo de texto puro contendo as entradas do log do OpenVPN.

Caso ainda não exista arquivo de log no equipamento, a chamada retorna o erro
"There is no OpenVPN log file available".

#### Exemplo de requisição

```
$.ajax({
    url: "/get_openvpn_log.fcgi?session=" + session,
    type: 'GET'
});
```
