<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/certificados-eap/ -->

# Certificados EAP

Os endpoints a seguir gerenciam os certificados e chaves utilizados na autenticação 802.1X via EAP. Dois métodos de autenticação são suportados:

- **EAP-PEAP**: utiliza um certificado CA para validar a identidade do servidor de autenticação.
- **EAP-TLS**: utiliza autenticação TLS mútua, exigindo um certificado de cliente, uma chave privada e um fluxo de CSR (Certificate Signing Request).

## Enviar certificado CA para EAP-PEAP

Envia o certificado CA utilizado na autenticação EAP-PEAP (802.1X). O certificado valida a identidade do servidor de autenticação.

### POST /eap_802_1X_certificate_change.fcgi

*Parâmetros*

- Arquivo de certificado em formato binário, enviado no corpo da requisição.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/eap_802_1X_certificate_change.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de certificado CA]
});
```

## Enviar certificado CA EAP-PEAP para Wi-Fi NEW!

Envia o certificado CA usado na autenticação EAP/802.1X na interface **sem fio**.
É a contraparte Wi-Fi de [eap_802_1X_certificate_change](../sistema/certificados-eap.md#post-eap_802_1x_certificate_changefcgi),
que se aplica à interface cabeada.

Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. Em breve estará disponível para os **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**, por meio de um dongle Wi-Fi USB.

### POST /eap_802_1X_certificate_change_wifi.fcgi

*Parâmetros*

- O arquivo do certificado deve ser enviado no corpo da requisição, em formato binário,
  com `Content-Type: application/octet-stream`.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/eap_802_1X_certificate_change_wifi.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de certificado]
});
```

## Enviar certificado de cliente EAP-TLS

Envia o certificado de cliente utilizado na autenticação EAP-TLS (802.1X).

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /eap_tls_802_1X_certificate_change.fcgi

*Parâmetros*

- Arquivo de certificado de cliente em formato binário, enviado no corpo da requisição.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/eap_tls_802_1X_certificate_change.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de certificado de cliente]
});
```

## Enviar chave privada EAP-TLS

Envia a chave privada utilizada na autenticação EAP-TLS (802.1X). Use este endpoint para fornecer uma chave privada pré-existente. Para usar uma chave gerada pelo fluxo de CSR, utilize [Persistir chave privada EAP-TLS gerada](../sistema/certificados-eap.md#persistir-chave-privada-eap-tls-gerada).

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /eap_tls_802_1X_private_key_change.fcgi

*Parâmetros*

- Arquivo de chave privada em formato binário, enviado no corpo da requisição.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/eap_tls_802_1X_private_key_change.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de chave privada]
});
```

## Obter arquivo CSR EAP-TLS

Retorna o arquivo CSR (Certificate Signing Request) para EAP-TLS. Se o CSR ainda não tiver sido gerado, uma nova chave privada e um CSR são criados com a senha fornecida. Se o CSR já existir, o arquivo existente é retornado sem regeneração.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /get_eap_tls_csr_file.fcgi

*Parâmetros*

- **password** *(string)*: Senha utilizada para proteger a chave privada gerada. Necessária apenas na primeira chamada, quando o CSR ainda não existe.

*Resposta*

- Conteúdo do arquivo CSR.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_eap_tls_csr_file.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "minhasenha"
    })
});
```

## Persistir chave privada EAP-TLS gerada

Persiste a chave privada gerada durante a criação do CSR (via [Obter arquivo CSR EAP-TLS](../sistema/certificados-eap.md#obter-arquivo-csr-eap-tls)). Chame este endpoint após obter um certificado assinado por uma CA e antes de configurar a autenticação EAP-TLS.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /eap_tls_802_1X_private_key_persist.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/eap_tls_802_1X_private_key_persist.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
