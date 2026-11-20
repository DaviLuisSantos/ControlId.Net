<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/obter-informacoes-do-sistema/ -->

# Obter informações do sistema

Obtém as seguintes informações e estatísticas do sistema: tempo ligado, horário do equipamento, tamanho e utilização das memórias RAM e flash, licença, informações da rede (endereço IP, máscara de rede e gateway), número de série do equipamento, versão do firmware e modo de operação standalone ou online.

### POST /system_information.fcgi NEW!

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **uptime** *(objeto JSON)*: Indica há quanto tempo o equipamento está ligado. Contém as seguintes chaves:
  > - **days** *(int)*: Contém os dias do uptime.
  > - **hours** *(int)*: Contém as horas do uptime (valores entre 0 e 23).
  > - **minutes** *(int)*: Contém os minutos do uptime (valores entre 0 e 59).
  > - **seconds** *(int)*: Contém os segundos do uptime (valores entre 0 e 59).
- **time** *(int)*: Contém a data e hora do equipamento em [Unix Timestamp](http://pt.wikipedia.org/wiki/Era_Unix).
- **daylight_savings_time_active** *(bool)*: Indica se o horário de verão está ativo no momento.
- **memory** *(objeto JSON)*: Contém informações sobre a memória do equipamento. Contém as seguintes chaves:
  > - **disk** *(objeto JSON)*: Contém informações sobre a memória não volátil do equipamento. Contém as seguintes chaves:
  >   > - **free** *(int)*: Espaço livre da memória não volátil (em bytes).
  >   > - **total** *(int)*: Tamanho total da memória não volátil (em bytes).
  > - **ram** *(objeto JSON)*: Contém informações sobre a memória volátil do equipamento. Contém as seguintes chaves:
  >   > - **free** *(int)*: Espaço livre da memória volátil (em bytes).
  >   > - **total** *(int)*: Tamanho total da memória volátil (em bytes).
- **license** *(objeto JSON)*: Contém informações sobre a licença. Contém as seguintes chaves:
  > - **users** *(int)*: Contém o número máximo de usuários permitidos pela licença em uso.
  > - **device** *(int)*: Contém o número máximo de equipamentos controlados por este equipamento, de acordo com o limite imposto pela licença.
  > - **type** *(int)*: Contém o tipo da licença.
- **biometrics** *(objeto JSON)*: Contém informações sobre as biometrias do equipamento. Contém a seguinte chave:
  > - **max_num_records** *(int)*: Contém o número máximo de biometrias que podem ser armazenadas no aparelho.
  > - **max_possible_num_records** *(int)*: Contém o número máximo de faces que o equipamento pode suportar, considerando os upgrades de capacidade disponíveis. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **network** *(objeto JSON)*: Contém informações sobre a rede. Contém as seguintes chaves:
  > - **mac** *(string)*: Contém o endereço físico (MAC) do equipamento.
  > - **ip** *(string)*: Contém o endereço IP do equipamento.
  > - **netmask** *(string)*: Contém a máscara de rede (netmask) do equipamento.
  > - **gateway** *(string)*: Contém o endereço do roteador (gateway) do equipamento.
  > - **web_server_port** *(int)*: Contém a porta do servidor web em uso.
  > - **ssl_enabled** *(bool)*: Indica se o SSL está ativado (HTTPS).
  > - **dhcp_enabled** *(bool)*: Indica se o DHCP está ativado.
  > - **ten_mbps** *(bool)*: Indica se a conexão está limitada a uma velocidade de 10 Mbps.
  > - **primary_dns** *(string)*: Contém o DNS primário do equipamento.
  > - **secondary_dns** *(string)*: Contém o DNS secundário do equipamento.
  > - **custom_hostname_enabled** *(bool)*: Indica se o hostname customizado está habilitado.
  > - **device_hostname** *(string)*: Contém o hostname do equipamento.
  > - **self_signed_certificate** *(bool)*: Indica se o servidor web usa certificado SSL autoassinado.
  > - **auto_dns_enabled** *(bool)*: Indica se a obtenção automática dos servidores DNS está habilitada. Disponível apenas na versão **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**.
  > - **ip_wifi**, **netmask_wifi** e **gateway_wifi** *(string)*: Endereço IP, máscara de rede e gateway da interface Wi-Fi. Enviados apenas quando o Wi-Fi está habilitado. Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
  > - **dhcp_enabled_wifi** *(bool)*: Indica se o DHCP está ativado na interface Wi-Fi. Enviado apenas quando o Wi-Fi está habilitado. Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **auth802_1x** *(objeto JSON)*: Contém informações sobre a autenticação 802.1X da interface cabeada. Contém as seguintes chaves:
  > - **enabled** *(bool)*: Indica se a autenticação 802.1X está habilitada.
  > - **login** *(string)*: Contém o login (identidade) usado na autenticação 802.1X.
  > - **eap_auth** *(int)*: Contém o código do método de autenticação EAP configurado.
  > - **inner_auth** *(int)*: Contém o código do método de autenticação interna (fase 2) configurado.
  > - **has_certificate** *(bool)*: Indica se há certificado de CA carregado para o 802.1X.
  > - **uses_certificate** *(bool)*: Indica se a autenticação 802.1X usa certificado.
  > - **has_tls_private_key** *(bool)*: Indica se há chave privada EAP-TLS no equipamento. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
  > - **private_key** *(objeto JSON)*: Enviado apenas quando há chave privada EAP-TLS. Contém a chave **self-generated** *(bool)*, que indica se a chave foi gerada pelo próprio equipamento. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
  > - **has_tls_certificate** *(bool)*: Indica se há certificado de cliente EAP-TLS no equipamento. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **auth802_1x_wifi** *(objeto JSON)*: Mesmas chaves de **auth802_1x**, referentes à interface Wi-Fi. Enviado apenas quando o Wi-Fi está habilitado. Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **serial** *(string)*: Contém o número de série do equipamento.
- **version** *(string)*: Contém a versão do firmware em uso.
- **device_id** *(string)*: Contém o número de identificação do equipamento.
- **secbox_version** *(string)*: Contém a versão de firmware da secbox em uso.
- **iDCloud_code** *(string)*: Contém o código responsável pela autenticação do equipamento no [iDCloud](https://idcloud.com.br/).
- **online** *(bool)*: Indica se o equipamento está funcionando em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md).
- **online_available** *(bool)*: Indica se o equipamento pode operar em modo online. Será *false* em iDFlex e iDAccess Nano antes do upgrade Enterprise (através de [licença](../particularidade-dos-produtos/update-idflex-idaccess-nano.md)). Em todos os demais casos será *true*.
- **device_name** *(string)*: Contém o primeiro nome do equipamento.
- **device_two_names** *(string)*: Contém os dois primeiros nomes do equipamento.
- **ubi0_stats**, **ubi1_stats** e **ubi2_stats** *(int)*: Contêm o número máximo de erros registrados em cada partição UBI da memória flash (NAND). Disponível apenas em **equipamentos da linha iDAccess**.

## Código OEM

O código OEM é um valor string que identifica a marca OEM do equipamento. Pode ser lido livremente, mas só pode ser alterado com uma chave de licença válida fornecida pela Control iD.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /get_oem_code.fcgi

Retorna o código OEM atual do equipamento.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **oem_code** *(string)*: Valor atual do código OEM.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_oem_code.fcgi?session=" + session,
    type: 'GET'
});
```

#### Exemplo de resposta

```
{
    "oem_code": "CID"
}
```

### POST /set_oem_code.fcgi

Define o código OEM do equipamento. Requer uma chave de licença válida.

*Parâmetros*

- **oem_code** *(string)*: **Obrigatório.** Novo valor do código OEM.
- **license** *(string)*: **Obrigatório.** Chave de licença que autoriza a alteração, fornecida pela Control iD.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_oem_code.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        oem_code: "MYOEM",
        license: "ABCDE12345"
    })
});
```
