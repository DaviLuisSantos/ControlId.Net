<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/verificar-disponibilidade-de-porta/ -->

# Verificar Disponibilidade de Porta

Verifica se um intervalo de portas conflita com alguma porta de serviço de rede atualmente configurada no equipamento. Útil ao configurar VPN ou outros serviços de rede para evitar conflitos de porta.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /check_port_availability.fcgi

*Parâmetros*

- **initial_port** *(int)*: Início do intervalo de portas a verificar.
- **final_port** *(int)*: Fim do intervalo de portas a verificar.

*Resposta*

- **status** *(string)*: Resultado da verificação. Retorna `"OK"` se não houver conflito, ou o nome do serviço em conflito:

| Valor | Serviço em conflito |
| --- | --- |
| `"OK"` | Nenhum conflito |
| `"WEB"` | Porta do servidor web |
| `"RTSP"` | Porta de streaming RTSP |
| `"SIP"` | Porta de sinalização SIP |
| `"SIP Outbound"` | Intervalo de portas SIP de saída |
| `"ONVIF"` | Porta ONVIF (incluindo a porta 8080) |

#### Exemplo de requisição

```
$.ajax({
    url: "/check_port_availability.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        initial_port: 5000,
        final_port: 5100
    })
});
```

#### Exemplo de resposta

```
{
    "status": "OK"
}
```
