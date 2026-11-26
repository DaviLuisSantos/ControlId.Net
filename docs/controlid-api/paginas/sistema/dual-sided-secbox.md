<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/dual-sided-secbox/ -->

# Dual sided do SecBox NEW!

O dual sided é usado quando dois equipamentos controlam a mesma porta, um de cada lado, através do SecBox padrão (porta 1). Quando a porta é aberta sem um comando deste equipamento, ele consulta o equipamento par para saber se a abertura foi comandada pelo outro lado, antes de decidir pelo alarme de arrombamento.

A comunicação entre os pares é autenticada com HMAC-SHA256, usando um segredo compartilhado de 32 bytes gravado nos dois equipamentos.

Disponível apenas em **equipamentos da linha iDAccess**.

## Configuração

O dual sided é configurado pelo módulo `dual_sided`, através de [Modificar Configurações](../configuracoes/modificar-configuracoes.md):

| Parâmetro | Tipo | Descrição |
| --- | --- | --- |
| **enabled** | *string* | `"1"` habilita o dual sided; qualquer outro valor o mantém desabilitado. Padrão: `"0"`. |
| **confirmation_timeout** | *string* | Prazo, em milissegundos, para o par responder à consulta. Valores entre `500` e `5000`. Padrão: `"2000"`. |
| **response_grace** | *string* | Tolerância, em milissegundos, somada ao fim de uma abertura comandada por este equipamento quando ele responde à consulta do par. Valores entre `0` e `5000`. Padrão: `"2000"`. |
| **door1_peer_address** | *string* | Endereço do equipamento par, no formato `http://<IPv4>[:porta]` ou `https://<IPv4>[:porta]`. Padrão: vazio. |

O segredo compartilhado não faz parte da configuração: ele é gravado pelo comando [set_dual_sided_secret](../sistema/dual-sided-secbox.md#post-set_dual_sided_secretfcgi).

## Gravar o segredo compartilhado

### POST /set_dual_sided_secret.fcgi

Grava o segredo compartilhado usado para autenticar a comunicação com o par. O mesmo segredo deve ser gravado nos dois equipamentos.

Requer uma sessão de **administrador**. Com outro nível de acesso, a chamada retorna o status HTTP 401 e o erro "Invalid access level for command set_dual_sided_secret".

O segredo nunca é devolvido pela API e não é gravado no log de auditoria.

*Parâmetros*

- **secret** *(string)*: **Obrigatório.** Segredo compartilhado, com exatamente 64 caracteres hexadecimais (32 bytes). Maiúsculas e minúsculas são aceitas. Caso contrário, a chamada retorna o erro "Invalid secret".

*Resposta*

- Esta chamada não possui retorno.

Se o segredo não puder ser gravado, a chamada retorna o erro "Unable to store secret".

#### Exemplo de requisição

```
$.ajax({
    url: "/set_dual_sided_secret.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        secret: "3f1c9a7be2d04856a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718"
    })
});
```

## Obter o estado do dual sided

### POST /get_dual_sided_status.fcgi

Informa se o segredo compartilhado está gravado e se o endereço do par é válido. O segredo nunca é devolvido.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **secret_configured** *(bool)*: Indica se há um segredo compartilhado gravado no equipamento.
- **door1_peer_address_valid** *(bool)*: Indica se o parâmetro **door1_peer_address** contém um endereço válido.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_dual_sided_status.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "secret_configured": true,
    "door1_peer_address_valid": true
}
```

## Consulta entre os pares

### POST /dual_sided_query.fcgi

Rota usada por um equipamento para consultar o seu par. Não é destinada a integrações: a requisição precisa ser assinada com o segredo compartilhado, e o próprio equipamento a monta e envia. Ela é descrita aqui apenas para que a rota seja reconhecida, por exemplo em regras de firewall.

Esta rota **não exige sessão**. A taxa é limitada a 20 requisições por segundo e o corpo, a 1024 bytes. Qualquer falha (dual sided desabilitado, segredo não gravado, requisição inválida ou assinatura incorreta) retorna o status HTTP 400 com o corpo `{"error":"Invalid request","code":1}`, sem indicar o motivo. As consultas não são gravadas no log de auditoria.

*Parâmetros*

- **version** *(int)*: Versão do protocolo. Atualmente `1`.
- **device_id** *(string)*: Identificador do equipamento que consulta, em decimal.
- **door** *(int)*: Porta lógica consultada no par. Atualmente apenas `1`.
- **age_ms** *(int)*: Tempo, em milissegundos, desde a abertura da porta até o envio da consulta. Valores entre `0` e `60000`.
- **nonce** *(string)*: Valor aleatório de 32 caracteres hexadecimais minúsculos.
- **mac** *(string)*: Assinatura HMAC-SHA256 da requisição, em 64 caracteres hexadecimais minúsculos.

*Resposta*

- **version** *(int)*: Versão do protocolo.
- **nonce** *(string)*: O mesmo **nonce** da requisição.
- **door** *(int)*: A mesma porta da requisição.
- **commanded** *(bool)*: Indica se o par confirma ter comandado a abertura da porta naquele momento. Com o modo de emergência ativo, o par sempre responde `true`.
- **cause** *(int)*: Motivo da abertura comandada; `0` quando **commanded** é `false`.
- **window_id** *(int)*: Identificador da abertura comandada; `0` quando **commanded** é `false`.
- **boot_id** *(string)*: Identificador aleatório da execução atual do software de acesso do par, em 16 caracteres hexadecimais.
- **device_id** *(string)*: Identificador do equipamento que responde, em decimal.
- **mac** *(string)*: Assinatura HMAC-SHA256 da resposta.

#### Exemplo de resposta

```
{
    "version": 1,
    "nonce": "9f2c4e1a7b3d5f6081a2b3c4d5e6f708",
    "door": 1,
    "commanded": true,
    "cause": 1,
    "window_id": 12,
    "boot_id": "5a1f3c9e7b2d4086",
    "device_id": "478435",
    "mac": "c0ffee00112233445566778899aabbccddeeff00112233445566778899aabbcc"
}
```
