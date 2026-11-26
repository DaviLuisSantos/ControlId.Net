<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/configuracoes-de-rede/ -->

# Configurações de Rede

Altera as configurações de rede local e permite conectar o equipamento a outras redes com OpenVPN.

## Alterar configurações de rede

Altera as configurações de rede do equipamento. O método HTTP usado é o **POST**.

### POST /set_system_network.fcgi

*Parâmetros*

- **ip** *(string)* : Texto representando o endereço IP que o equipamento deve assumir. Exemplo: "192.168.0.33".
- **netmask** *(string)* : Texto representando a máscara de rede que o equipamento deve assumir. Exemplo: "255.255.255.0".
- **gateway** *(string)* : Endereço IP do gateway da rede. Exemplo: "192.168.0.1".
- **custom_hostname_enabled** *(bool)* : Habilita configuração para criar um hostname customizado para o dispositivo. Valores: "true" para habilitar e "false" para desabilitar.
- **device_hostname** *(string)* : Quando *custom_hostname_enabled* está habilitado, este parâmetro deve receber o hostname customizado pelo usuário. Exemplo: "ControlID". Quando *custom_hostname_enabled* está desabilitado, o hostname padrão do dispositivo será: "CID-" + número de série do dispositivo. Exemplo: "CID-0M0200-C0FF33".
- **web_server_port** *(int)* : Número da porta de comunicação do Acesso. Exemplo: 80.
- **ssl_enabled** *(bool)* : Habilita a [criptografia por SSL (HTTPS)](../sistema/configuracoes-de-rede.md#ssl) do servidor web. Valores: "true" para habilitar e "false" para desabilitar.
- **self_signed_certificate** *(bool)* : Habilita a configuração para usar certificado autoassinado. Valores: "true" para habilitar certificado autoassinado e "false" para habilitar SSL com certificado de terceiro.
- **primary_dns** *(string)*: Indica o DNS primário do equipamento. Padrão: "8.8.8.8".
- **secondary_dns** *(string)*: Indica o DNS secundário do equipamento. Padrão: "8.8.4.4".
- **interface** *(string)*: Seleciona a qual interface as configurações se aplicam. O valor "1" indica a interface cabeada; qualquer outro valor indica a interface sem fio. Quando omitido, a interface cabeada é utilizada. Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **dhcp_enabled** *(bool)*: Habilita o DHCP na interface selecionada. Quando habilitado, os campos de endereço acima são obtidos automaticamente. O padrão é a configuração atual da interface.
- **ten_mbps** *(bool)*: Força o link cabeado a negociar em 10 Mbps em vez de 100 Mbps. O padrão é a configuração atual do equipamento.
- **auto_dns_enabled** *(bool)*: Habilita a obtenção automática dos servidores DNS. O padrão é a configuração atual do equipamento. Disponível apenas na versão **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **wifi_source** *(string)*: Seleciona o adaptador Wi-Fi utilizado quando as configurações se aplicam à interface sem fio (**interface** diferente de "1"). Valores: `"internal"` (adaptador interno), `"usb"` (dongle Wi-Fi USB); qualquer outro valor desabilita o Wi-Fi. O padrão é a configuração atual do equipamento. Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

*Resposta*

- Esta chamada não possui retorno, exceto em **equipamentos da linha iDFace e iDFace Max com firmware [V8](../particularidade-dos-produtos/versoes-de-firmware.md)**, que retornam:
- **result** *(int)*: Código do resultado da aplicação das configurações: `5` (sucesso), `0` (IP inválido), `1` (máscara de rede inválida), `2` (gateway inválido), `3` (porta inválida), `4` (falha de configuração), `6` (falha de SSL) ou `7` (IP indisponível). Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

#### Exemplo de requisição

Alterar o endereço IP, a máscara de rede, o gateway e a porta do equipamento:

```
$.ajax({
    url: "/set_system_network.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        ip: "192.168.0.33",
        netmask: "255.255.255.0",
        gateway: "192.168.0.1",
        custom_hostname_enabled: true,
        device_hostname: "ControlID",
        web_server_port: 80,
        ssl_enabled: false,
        self_signed_certificate: true,
        primary_dns: "1.1.1.1",
        secondary_dns: "1.1.4.4"
    })
});
```

## SSL

SSL (Secure Sockets Layer) é um tipo de protocolo de segurança que estabelece uma conexão criptografada entre um servidor web e um navegador, garantindo que as informações transmitidas pela internet permaneçam privadas e seguras. A seguir, demonstra-se o processo de geração de certificados SSL e adição desses aos equipamentos da linha de controle de acesso da Control iD. Ao fim, apresenta-se a requisição HTTP **POST** que permite a realização desse mesmo processo.

### Configuração de certificado SSL

1. Faça o [download da versão Light](https://slproweb.com/products/Win32OpenSSL.html) do OpenSSL (opção “**Win64 OpenSSL v1.1.1t Light**“, para Windows 64 bits, ou “**Win32 OpenSSL v1.1.1t Light**“, para Windows 32 bits);
2. Execute o instalador, deixando o diretório padrão para finalização da instalação desta biblioteca;
3. Abra o "**Prompt de Comando**" como "**administrador**" e vá para o diretório padrão onde o OpenSSL foi instalado;  
   Exemplo (linha de comando): cd `C:\Program Files\OpenSSL-Win64\bin`
4. Execute o comando a seguir e preencha as informações solicitadas, conforme orientações passadas a seguir;  
   Exemplo (linha de comando): `openssl req -x509 -newkey rsa:1024 -nodes -keyout domain.key -out domain.pem`
   Preenchimento dos campos:
   - Insira uma sigla referente ao seu país no campo "**Country Name**", conforme apresentado no link a seguir (para Brasil, digite BR): [SSL Certificate Country Codes](https://www.digicert.com/kb/ssl-certificate-country-codes.htm;)
   - Insira uma sigla referente ao seu estado/província no campo "**State or Province Name**" (para São Paulo, digite SP);
   - Insira o nome da sua localidade/cidade no campo "**Locality name/City**";
   - Insira o nome da sua empresa no campo "**Organization Name**";
   - Insira o nome do setor ou departamento da sua organização no campo "**Organizational Unit Name**";
   - Insira o IP do seu equipamento no campo "**Common Name**";
   - Insira um endereço de e-mail no campo "**Email Address**".
5. No final, os dois arquivos (um .pem e um .key) gerados terão de ser combinados. Para isto, execute o comando a seguir;  
   Exemplo (linha de comando): `type domain.key,domain.pem > final.pem`
   (para ambiente Linux: `cat domain.key domain.pem > final.pem`)
6. Após isso, feche o "**Prompt de Comando**", acesse o diretório padrão do OpenSSL e abra a pasta `bin` (`C:\Program Files\OpenSSL-Win64\bin`);
7. Colete o arquivo chamado `final.pem`;
8. Com isso, acesse a interface Web do equipamento digitando o IP dele no seu browser;
9. Ao acessá-la, selecione a opção "**Configurações**", presente no menu à esquerda;
10. Procure pela opção "**Rede**", selecione-a e habilite a opção "**SSL**". No campo "**Certificado**", selecione o arquivo `final.pem` apresentado no **passo 7**, confirme e salve as configurações;
11. Depois de realizar os passos anteriores, basta recarregar a página e acessar o equipamento colocando o protocolo "**https**" na URL.

### GET /get_ssl_certificate.fcgi

Retorna o certificado SSL atualmente carregado no servidor web do equipamento.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Conteúdo do certificado SSL em formato de texto. Retorna erro se nenhum certificado tiver sido configurado.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_ssl_certificate.fcgi?session=" + session,
    type: 'GET'
});
```

### POST /ssl_certificate_change.fcgi

**Atenção**: deve-se habilitar a criptografia por SSL pela requisição para o endpoint [set_system_network.fcgi](../sistema/configuracoes-de-rede.md#post-set_system_networkfcgi) para que o certificado seja configurado. Além disso, o certificado deve estar no formato **PEM** e ser gerado conforme [instruções acima](../sistema/configuracoes-de-rede.md#configuracao-de-certificado-ssl).

*Parâmetros*

- Certificado, em formato binário, que será adicionado ao servidor web.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Adicionar certificado SSL ao servidor web do equipamento:

```javascript
$.ajax({
    url: "/ssl_certificate_change.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do certificado com extensão .pem]
});
```

## Wi-Fi NEW!

Disponível apenas na versão **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. Em breve estará disponível para os **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**, por meio de um dongle Wi-Fi USB.

O equipamento pode se conectar a uma rede sem fio pelo adaptador Wi-Fi interno ou por um
adaptador USB externo. O fluxo típico de configuração é: selecionar o adaptador com
[set_wifi_source](../sistema/configuracoes-de-rede.md#post-set_wifi_sourcefcgi), listar as redes ao alcance com
[wifi_scan](../sistema/configuracoes-de-rede.md#post-wifi_scanfcgi) e então conectar-se a uma delas com
[set_wifi_settings](../sistema/configuracoes-de-rede.md#post-set_wifi_settingsfcgi).

### Selecionar o adaptador Wi-Fi

#### POST /set_wifi_source.fcgi

*Parâmetros*

- **source** *(string)*: Adaptador a ser utilizado. Valores: `"internal"` para o adaptador
  interno, `"usb"` para um adaptador USB externo ou `"disabled"` para desligar o Wi-Fi.
  Qualquer outro valor é tratado como `"disabled"`. Padrão: `"disabled"`.

*Resposta*

- Esta chamada não possui retorno.

##### Exemplo de requisição

```
$.ajax({
    url: "/set_wifi_source.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ source: "internal" })
});
```

### Buscar redes Wi-Fi

Realiza uma varredura dos pontos de acesso ao alcance. A chamada é síncrona e pode levar até
20 segundos para ser concluída.

#### POST /wifi_scan.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **networks** *(array)*: Uma entrada por ponto de acesso encontrado, cada uma contendo:
  - **ssid** *(string)*: Nome da rede.
  - **bssid** *(string)*: Endereço MAC do ponto de acesso, no formato `xx:xx:xx:xx:xx:xx`.
  - **freq** *(int)*: Frequência de operação, em MHz.
  - **signal** *(int)*: Nível de sinal, em dBm (valores mais próximos de zero são mais fortes).

##### Exemplo de requisição

```
$.ajax({
    url: "/wifi_scan.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

##### Exemplo de resposta

```
{
    "networks": [
        { "ssid": "Corporativa", "bssid": "a1:b2:c3:d4:e5:f6", "freq": 5180, "signal": -45 },
        { "ssid": "Visitantes", "bssid": "a1:b2:c3:d4:e5:f7", "freq": 2412, "signal": -72 }
    ]
}
```

### Conectar a uma rede Wi-Fi

#### POST /set_wifi_settings.fcgi

*Parâmetros*

- **ssid** *(string)*: Nome da rede à qual se conectar.
- **psk** *(string)*: Senha da rede (pre-shared key). Envie string vazia para redes abertas.
- **bssid** *(string)*: Opcional. Endereço MAC de um ponto de acesso específico, para forçar a
  conexão àquele AP em vez de deixar o equipamento escolher entre os APs que compartilham o
  mesmo **ssid**.

*Resposta*

- **result** *(int)*: Resultado da tentativa de conexão:

| Valor | Descrição |
| --- | --- |
| `0` | Conectado com sucesso |
| `1` | Senha incorreta |
| `2` | Rede não encontrada |
| `3` | Tempo limite da tentativa de conexão esgotado |
| `4` | Erro desconhecido |

##### Exemplo de requisição

```
$.ajax({
    url: "/set_wifi_settings.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ ssid: "Corporativa", psk: "senha123" })
});
```

##### Exemplo de resposta

```
{
    "result": 0
}
```

### Obter informações de Wi-Fi

Retorna a configuração sem fio atual e o estado da conexão.

#### POST /get_wifi_data.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **available_adapters** *(array de strings)*: Adaptadores detectados no equipamento. Pode
  conter `"internal"` e/ou `"usb"`.
- **source** *(string)*: Adaptador atualmente selecionado. Valores: `"internal"`, `"usb"` ou
  `"disabled"`.
- **ssid** *(string)*: Nome da rede configurada.
- **psk** *(string)*: Senha da rede configurada.
- **status** *(string)*: Estado da conexão. Valores: `"connected"` ou `"not_connected"`.
- **connected_ssid** *(string)*: Nome da rede conectada no momento, ou string vazia quando não
  há conexão.
- **priority** *(string)*: Interface que possui prioridade de roteamento no momento, derivada das
  métricas das interfaces. Valores: `"wired"` ou `"wireless"`.

##### Exemplo de requisição

```
$.ajax({
    url: "/get_wifi_data.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

##### Exemplo de resposta

```
{
    "available_adapters": ["internal"],
    "ssid": "Corporativa",
    "psk": "senha123",
    "priority": "wired",
    "status": "connected",
    "connected_ssid": "Corporativa",
    "source": "internal"
}
```

### Esquecer a rede Wi-Fi conectada

Desconecta da rede sem fio atual e apaga as credenciais armazenadas.

#### POST /forget_wifi_connected_network.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

##### Exemplo de requisição

```
$.ajax({
    url: "/forget_wifi_connected_network.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

### Verificar conflito de IP entre interfaces

Verifica se um endereço IP a ser configurado em uma interface já está em uso pela outra
interface, já que o equipamento pode ter as interfaces cabeada e sem fio ativas ao mesmo tempo.

#### POST /check_ip_conflict.fcgi

*Parâmetros*

- **interface** *(string)*: Interface para a qual o endereço se destina. O valor `"1"` indica a
  interface cabeada; qualquer outro valor indica a interface sem fio.
- **ip** *(string)*: Endereço IP a ser verificado.

*Resposta*

- **conflict** *(bool)*: `true` se o endereço já está configurado na outra interface.

##### Exemplo de requisição

```
$.ajax({
    url: "/check_ip_conflict.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ interface: "1", ip: "192.168.0.129" })
});
```

##### Exemplo de resposta

```
{
    "conflict": false
}
```

## OpenVPN

Altera as configurações de rede para utilização do utilitário do OpenVPN através do endpoint **/set_vpn_information**. Para o funcionamento correto, é necessário o carregamento de um arquivo de configuração, no endpoint **/set_vpn_file**, em formato .conf, contendo nele os certificados, chaves e/ou credenciais necessárias para o acesso. O arquivo também pode ser carregado junto de um arquivo em formato .zip, contendo os certificados (.crt), chaves (.key) e/ou credenciais em arquivos distintos. Exemplos de parametrização do arquivo de configuração podem ser obtidos através do endpoint **/get_vpn_file**. Além disso, para controle de status e conexões existem três endpoints: **/get_vpn_information**, que retorna as configurações de conexão; **/get_vpn_status**, que verifica o estado da conexão, e **/has_vpn_file**, que verifica se já existe algum arquivo de configuração presente.

### Alterar configurações de OpenVPN

Altera as configurações de rede para utilização do utilitário do OpenVPN. O método HTTP usado é o **POST**.

### POST /set_vpn_information.fcgi

*Parâmetros*

- **enabled** *(bool)* : Indica se a VPN está habilitada. Padrão: "false".
- **login_enabled** *(bool)* : Indica se a VPN é acessada a partir de login e senha manuais. Padrão: "false".
- **login** *(string)* : Representa o usuário do login utilizado caso *login_enabled* esteja ativado. Exemplo: "Admin".
- **password** *(string)* : Representa a senha do login utilizado caso *login_enabled* esteja ativado. Exemplo: "Admin".

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Habilita o utilitário e permite atualizar usuário e senha manual:

```
$.ajax({
    url: "/set_vpn_information.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "enabled": true,
        "login_enabled": true,
        "login": "Admin",
        "password": "Admin"
    })
});
```

### Alterar os arquivos de acesso à VPN

Altera os arquivos de carregamento do utilitário, podendo ser um arquivo em formato *.conf* ou um arquivo em formato *.zip* contendo todos os dados necessários. O método HTTP usado é o **POST**.

### POST /set_vpn_file.fcgi

*Parâmetros*

- **file_type** *(string)* : Identificador do tipo de arquivo enviado. Valores: "zip" para arquivos .zip e "config" para arquivos de configuração .conf. **Esse parâmetro é passado na Query String.**

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Esta requisição irá definir o arquivo de configuração utilizado na VPN.

```
$.ajax({
    url: "/set_vpn_file.fcgi?file_type=zip&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo VPN]
});
```

### Receber os arquivos de exemplo

Recebe os arquivos de exemplo de parametrização do arquivo de configuração do utilitário. O método HTTP usado é o **GET**.

### GET /get_vpn_file.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Arquivo em formato **zip**.

#### Exemplo de requisição

Esta requisição retorna os arquivos de exemplo de configuração da VPN.

```
$.ajax({
    url: "/get_vpn_file.fcgi?&session=" + session,
    type: 'GET',
    contentType: 'application/octet-stream'
});
```

### Receber configurações de OpenVPN

Recebe as configurações de rede do utilitário do OpenVPN. O método HTTP usado é o **GET**.

### GET /get_vpn_information.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **enabled** *(bool)* : Indica se a VPN está habilitada. Padrão: "false".
- **login_enabled** *(bool)* : Indica se a VPN é acessada a partir de login e senha manuais. Padrão: "false".
- **login** *(string)* : Representa o usuário do login utilizado caso *login_enabled* esteja ativado. Exemplo: "Admin".
- **password** *(bool)* : Representa se há senha de login configurada caso *login_enabled* esteja ativado. Exemplo: "true"

#### Exemplo de requisição

Esta requisição retorna as configurações utilizadas no OpenVPN.

```
$.ajax({
    url: "/get_vpn_information.fcgi?&session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "enabled": true,
    "login_enabled": false,
    "login": "Admin",
    "password": true
}
```

### Receber status da conexão OpenVPN

Recebe o status de rede do utilitário do OpenVPN. O método HTTP usado é o **GET**.

### GET /get_vpn_status.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **status** *(int)* : Indica um dos estados do OpenVPN listados abaixo.
  > - **0** *(connected)* : Representa que o OpenVPN está conectado.
  > - **1** *(auth_failed)* : Representa que o servidor OpenVPN está rejeitando a conexão durante o estágio de autenticação.
  > - **2** *(ca_missing)* : Representa que está faltando o certificado da autoridade certificadora (CA) para aprovação do OpenVPN.
  > - **3** *(ca_failed)* : Representa que o servidor OpenVPN não conseguiu verificar a autoridade certificadora (CA).
  > - **4** *(crt_key_missing)* : Representa que pode estar faltando o certificado público, a chave privada ou ambos.
  > - **5** *(crt_failed)* : Representa que o certificado público não foi aprovado pelo servidor do OpenVPN.
  > - **6** *(key_failed)* : Representa uma falha na autenticação pelo servidor OpenVPN utilizando a chave privada.
  > - **7** *(tls_failed)* : Representa falha no protocolo de segurança TLS.
  > - **8** *(disconnected)* : Representa que o OpenVPN está desabilitado.
  > - **9** *(trying_to_connect)* : Representa que o OpenVPN está ativo, porém ainda não atingiu nenhum dos status mapeados.
  > - **10** *(no connection)* : Representa que a rede está desconectada.

#### Exemplo de requisição

Esta requisição retorna o status da conexão OpenVPN.

```
$.ajax({
    url: "/get_vpn_status.fcgi?&session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "status": "0"
}
```

### Confirmar a existência do arquivo de configuração

Recebe a confirmação da existência do arquivo de configuração no dispositivo para o utilitário do OpenVPN. O método HTTP usado é o **GET**.

### GET /has_vpn_file.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **has_file** *(bool)* : Indica se já existe um arquivo de configuração OpenVPN no dispositivo.

#### Exemplo de requisição

Esta requisição retorna o status do arquivo OpenVPN.

```
$.ajax({
    url: "/has_vpn_file.fcgi?&session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "has_file": true
}
```

### Obter IP da VPN

Retorna o endereço IP atribuído ao equipamento na rede VPN (interface `tun0`).

### GET /get_vpn_ip.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **ip** *(string)*: Endereço IP da conexão VPN. Retorna string vazia se a VPN não estiver conectada.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_vpn_ip.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "ip": "10.8.0.2"
}
```

## Habilitar 802.1X

É possível configurar o equipamento como suplicante e habilitar o protocolo 802.1X de autenticação de sua porta de rede. O método EAP externo é definido pelo parâmetro **eap_auth** (PEAP, TTLS ou TLS).

### POST /configure_802_1X.fcgi

*Parâmetros*

- **enabled** *(bool)*: Determina se a autenticação por 802.1X está ativada ou não;
- **login** *(string)*: Credencial de nome de usuário;
- **password** *(string)*: Credencial de senha de usuário;
- **inner_auth** *(int)*: Autenticação interna. 0: MS-CHAPv2. 1: MD5. 2: GTC.
- **eap_auth** *(int)*: Método EAP externo. Valores: `0` (PEAP), `1` (TTLS) ou `2` (TLS). O padrão é a configuração atual do equipamento.
- **uses_certificate** *(bool)*: Determina se a autenticação utiliza certificado de cliente. Envie o certificado pelos endpoints descritos em [Certificados EAP](../sistema/certificados-eap.md).
- **interface** *(string)*: Seleciona a qual interface o 802.1X se aplica. O valor "1" indica a interface cabeada; qualquer outro valor indica a interface sem fio. Quando omitido, a interface cabeada é utilizada.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Habilitar 802.1X com credenciais de login "testing" e senha "password", e autenticação interna por MS-CHAPv2:

```
$.ajax({
    url: "/configure_802_1X.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        enabled: true,
        login: "testing",
        password: "password",
        inner_auth: 0
    })
});
```

### Obter status do 802.1X

Retorna o status atual da conexão de autenticação 802.1X.

### GET /get_802_1X_status.fcgi

*Parâmetros*

- **interface** *(string)*: Seleciona sobre qual interface o status será retornado. Valores: `"wired"` (padrão) ou `"wireless"`. Note que este endpoint recebe a interface como nome, diferentemente de [configure_802_1X](../sistema/configuracoes-de-rede.md#post-configure_802_1xfcgi) acima, que a recebe como `"1"` para cabeada.

*Resposta*

- **status** *(int)*: Estado atual da autenticação 802.1X.

| Valor | Estado |
| --- | --- |
| `0` | INITIALIZE |
| `1` | DISCONNECTED |
| `2` | CONNECTING |
| `3` | AUTHENTICATING |
| `4` | AUTHENTICATED |
| `5` | ABORTING |
| `6` | HELD |
| `7` | FORCE_AUTH |
| `8` | FORCE_UNAUTH |
| `9` | RESTART |
| `10` | UNKNOWN |
| `11` | DISABLED |

#### Exemplo de requisição

```
$.ajax({
    url: "/get_802_1X_status.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "status": 4
}
```

## OSDP

O protocolo OSDP é um padrão de comunicação que oferece troca de dados bidirecional entre o dispositivo de controle de acesso (controlador) e o dispositivo de leitura (leitor). O OSDP suporta recursos avançados de segurança, como criptografia de ponta a ponta e autenticação mútua.

### Modo de instalação

Quando o modo de instalação está ativo, o equipamento fica disponível para negociar novas chaves de criptografia a fim de manter o canal de comunicação OSDP seguro.

#### GET /get_osdp_installation_mode.fcgi

Retorna se o modo de instalação OSDP está ativo.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **installation_mode** *(bool)*: `true` se o modo de instalação estiver ativo.

##### Exemplo de requisição

```
$.ajax({
    url: "/get_osdp_installation_mode.fcgi?session=" + session,
    type: 'GET'
});
```

##### Exemplo de resposta

```
{
    "installation_mode": false
}
```

#### POST /set_osdp_installation_mode.fcgi

Habilita ou desabilita o modo de instalação OSDP.

*Parâmetros*

- **enable** *(bool)*: Opcional. `true` para habilitar o modo de instalação, `false` para desabilitar. Padrão: `false`.

*Resposta*

- Esta chamada não possui retorno.

##### Exemplo de requisição

```
$.ajax({
    url: "/set_osdp_installation_mode.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "enable": true
    })
});
```

### Chave Base de Canal Seguro (SCBK)

Retorna ou define a Chave Base de Canal Seguro (SCBK) do OSDP armazenada na configuração do equipamento. Este endpoint opera em dois modos conforme a presença do campo `scbk` no corpo da requisição:

- **Sem `scbk`**: retorna a SCBK atual.
- **Com `scbk`**: salva a nova SCBK.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

#### POST /osdp_scbk.fcgi

*Parâmetros*

- **scbk** *(string)*: Opcional. Novo valor da SCBK a ser armazenado. Se omitido, o endpoint retorna o valor atual.

*Resposta (sem `scbk`)*

- **scbk** *(string)*: Valor atual da SCBK.

*Resposta (com `scbk`)*

- Esta chamada não possui retorno.

##### Exemplo de requisição — obter SCBK atual

```
$.ajax({
    url: "/osdp_scbk.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```

##### Exemplo de resposta

```
{
    "scbk": "0102030405060708090a0b0c0d0e0f10"
}
```

##### Exemplo de requisição — definir nova SCBK

```
$.ajax({
    url: "/osdp_scbk.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "scbk": "0102030405060708090a0b0c0d0e0f10"
    })
});
```

### Status de conexão

Retorna o status de conexão de todos os leitores registrados em um painel OSDP.

Disponível apenas em **equipamentos da linha iDAccess**.

#### POST /osdp_get_connection_status.fcgi

*Parâmetros*

- **panel_id** *(int)*: Obrigatório. ID do painel OSDP a ser consultado.

*Resposta*

- Objeto mapeando o endereço de cada leitor (como chave string) ao seu status de conexão *(int)*.

##### Exemplo de requisição

```
$.ajax({
    url: "/osdp_get_connection_status.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "panel_id": 1
    })
});
```

##### Exemplo de resposta

```
{
    "0": 1,
    "1": 0
}
```

### Capacidades de saída

Retorna as capacidades de saída de um leitor OSDP específico.

Disponível apenas em **equipamentos da linha iDAccess**.

#### POST /osdp_get_output_capabilities.fcgi

*Parâmetros*

- **address** *(int)*: Obrigatório. Endereço do leitor OSDP.

*Resposta*

- **nun_outputs** *(int)*: Número de saídas suportadas pelo leitor. Atenção: na resposta do equipamento, o nome desta chave é grafado exatamente assim (`nun_outputs`, e não `num_outputs`).
- **compliance_level** *(int)*: Nível de conformidade das capacidades de saída do leitor.

##### Exemplo de requisição

```
$.ajax({
    url: "/osdp_get_output_capabilities.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "address": 0
    })
});
```

##### Exemplo de resposta

```
{
    "nun_outputs": 2,
    "compliance_level": 1
}
```

### Definir chave segura

Define a SCBK (Secure Channel Base Key) para um leitor OSDP específico em um painel.

Disponível apenas em **equipamentos da linha iDAccess**.

#### POST /osdp_set_secure_key.fcgi

*Parâmetros*

- **panel_id** *(int)*: Obrigatório. ID do painel OSDP.
- **address** *(int)*: Obrigatório. Endereço do leitor OSDP.
- **scbk_hex** *(string)*: Obrigatório. Nova SCBK em formato de string hexadecimal.

*Resposta*

- Esta chamada não possui retorno.

##### Exemplo de requisição

```
$.ajax({
    url: "/osdp_set_secure_key.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "panel_id": 1,
        "address": 0,
        "scbk_hex": "0102030405060708090a0b0c0d0e0f10"
    })
});
```

### Definir tipo de canal seguro

Configura o tipo de canal seguro OSDP para um leitor específico em um painel.

Disponível apenas em **equipamentos da linha iDAccess**.

#### POST /osdp_set_secure_type.fcgi

*Parâmetros*

- **panel_id** *(int)*: Obrigatório. ID do painel OSDP.
- **address** *(int)*: Obrigatório. Endereço do leitor OSDP.
- **secure_type** *(int)*: Obrigatório. Modo do canal seguro.

| Valor | Modo |
| --- | --- |
| `0` | Inseguro |
| `1` | Modo de instalação |
| `2` | Somente seguro |

*Resposta*

- Esta chamada não possui retorno.

##### Exemplo de requisição

```
$.ajax({
    url: "/osdp_set_secure_type.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "panel_id": 1,
        "address": 0,
        "secure_type": 2
    })
});
```

### Atualizar dispositivos

Força a redescoberta dos dispositivos OSDP conectados.

Disponível apenas em **equipamentos da linha iDAccess**.

#### POST /osdp_refresh.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

##### Exemplo de requisição

```
$.ajax({
    url: "/osdp_refresh.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```
