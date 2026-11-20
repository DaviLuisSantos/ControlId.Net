<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/modos-de-operacao/eventos-de-identificacao-online/ -->

# Eventos de Identificação Online

Quando uma tentativa de identificação ocorre, os eventos descritos abaixo são enviados automaticamente pelo equipamento conforme o modo de operação online em que ele estiver: [Modo Pro](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-pro) ou [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise). Cabe ao servidor externo tratar esses eventos.

O passo a passo para configurar o equipamento no modo de operação online pode ser acessado em [Configurar Modo Online](../modos-de-operacao/configurar-modo-online.md).

## Evento imagem biométrica

Tentativa de identificação por biometria.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

O método HTTP usado é o POST. O **contentType** é **application/octet-stream**. Todos os parâmetros são enviados através da query string, exceto o binário da imagem.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**, para a IHM a configuração [*ihm_enterprise_mode*](../configuracoes/parametros-configuracao.md#general) também deve ser **ativada**, e apenas se a configuração [*extract_template*](../configuracoes/parametros-configuracao.md#online_client) estiver **desativada**.

### POST /new_biometric_image.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (wiegand, RFID, biometria), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **width** *(int)* : Largura, em pixels, da imagem enviada.
- **height** *(int)* : Altura, em pixels, da imagem enviada.
- **session** *(string)* : Número da sessão.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.
- **uuid** *(string)* : Identificador Único Universal.
- **variance** *(int)* : Parâmetro de qualidade na identificação biométrica.
- **imagem** *(binário (octet-stream))* : Imagem da digital em formato binário. É enviado 1 byte por pixel, em escala de cinza (**Este é o único parâmetro enviado no corpo da mensagem**).

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento template biométrica

Tentativa de identificação por biometria.

O método HTTP usado é o POST. O **contentType** é **application/octet-stream**. Todos os parâmetros são enviados através da query string, exceto o binário do template.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**, para a IHM a configuração [*ihm_enterprise_mode*](../configuracoes/parametros-configuracao.md#general) também deve ser **ativada**, e apenas se a configuração [*extract_template*](../configuracoes/parametros-configuracao.md#online_client) estiver **ativada**.

### POST /new_biometric_template.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (wiegand, RFID, biometria), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **session** *(string)* : Número da sessão.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.
- **uuid** *(string)* : Identificador Único Universal.
- **variance** *(int)* : Parâmetro de qualidade na identificação biométrica.
- **Template** *(binário (octet-stream))* : Template biométrico no formato [Innovatrics](http://www.innovatrics.com/) (**Este é o único parâmetro enviado no corpo da mensagem**).

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento template facial

Tentativa de identificação por reconhecimento facial.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são enviados através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**. O equipamento detecta a face, extrai o template facial e o envia ao servidor para identificação.

### POST /new_face_template.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador, vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **uuid** *(string)* : Identificador Único Universal.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **template** *(string)* : Template facial extraído pelo equipamento, codificado em Base64. Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- **templateFF**, **templateMF** e **templateMM** *(string)* : Templates faciais extraídos pelo equipamento, codificados em Base64. Quando o usuário está sem máscara, são preenchidos **templateFF** e **templateMF** e **templateMM** é enviado vazio; quando o usuário está de máscara, apenas **templateMM** é preenchido. Disponível apenas na versão **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento cartão de proximidade

Tentativa de identificação por cartão de proximidade.

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**, para a IHM a configuração [*ihm_enterprise_mode*](../configuracoes/parametros-configuracao.md#general) também deve ser **ativada**.

### POST /new_card.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (wiegand, RFID, biometria), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **card_value** *(int 64)* : Número do cartão.
- **panic** *(int)* : Indica se é cartão de pânico (1) ou não (0).
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.
- **uuid** *(string)* : Identificador Único Universal.
- **block_read_error** *(int 64)* : Quando diferente de 0, indica que um erro de leitura do bloco ocorreu. Só será preenchido quando [mifare->read_block](../configuracoes/parametros-configuracao.md#mifare) for **não** vazio.
- **block_read_data** *(string)* : Dados lidos do bloco em hexadecimal (Não é possível incluir base64 em uma URL sem escapar alguns caracteres). Só será preenchido quando [mifare->read_block](../configuracoes/parametros-configuracao.md#mifare) for **não** vazio.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento QR Code

Tentativa de identificação por QR Code.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**, para a IHM a configuração [*ihm_enterprise_mode*](../configuracoes/parametros-configuracao.md#general) também deve ser **ativada**.

**Observação:** O equipamento irá enviar o evento de QR Code quando o parâmetro `qrcode_legacy_mode_enabled` (do módulo `barras` para linha V5 e `face_id` da linha V6) estiver configurado em **0**. Caso `qrcode_legacy_mode_enabled` esteja definido em **1**, leituras de QR Code serão interpretadas da mesma forma como leituras de cartão.

### POST /new_qrcode.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (Wiegand, RFID, biometria, etc), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **qrcode_value** *(string)* : Número do QR Code.
- **uuid** *(string)* : Identificador Único Universal.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento UHF tag

Tentativa de identificação por UHF tag.

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**.

### POST /new_uhf_tag.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (Wiegand, RFID, biometria, etc), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **uhf_tag** *(string)* : Valor lido pela tag UHF
- **uuid** *(string)* : Identificador Único Universal.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento id e senha

Tentativa de identificação por id e senha.

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**, para a IHM a configuração [*ihm_enterprise_mode*](../configuracoes/parametros-configuracao.md#general) também deve ser **ativada**.

### POST /new_user_id_and_password.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (wiegand, RFID, biometria), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **user_id** *(int)* : ID informado pelo usuário.
- **password** *(string)* : Senha informada pelo usuário.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.
- **uuid** *(string)* : Identificador Único Universal.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento PIN

Tentativa de identificação por PIN.

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são enviados através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **desativada**.

### POST /new_pin.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador, vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **component_id** *(int)* : ID do componente que realizou a identificação (exclusivo para [**iDBlock Next**](../particularidade-dos-produtos/idblock-next.md#eventos-de-identificacao-no-modo-online)).
- **pin** *(string)* : PIN informado pelo usuário.
- **uuid** *(string)* : Identificador Único Universal.
- **time** *(int)* : Momento do acesso em Unix Timestamp.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento usuário identificado

Quando o usuário se identifica no equipamento, este executa a identificação de forma local e envia o ID do usuário identificado ao servidor. Então, o servidor deve processar suas regras de acesso e conceder ou não autorização.

Repare que, por este método, o servidor precisa sempre manter atualizados os usuários e seus dados biométricos no equipamento, estando sujeito à limitação de registros existente nele. Esta limitação é descrita no tópico [Limitação do número de templates](../particularidade-dos-produtos/particulariade-terminais-control-id.md#limitacao-do-numero-de-templates).

O método HTTP usado é o POST. O **contentType** é **application/x-www-form-urlencoded**. Todos os parâmetros são através do corpo da requisição como um formulário codificado em URL.

Este evento é enviado quando o equipamento opera em [Modo Pro](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-pro), ou seja, quando a configuração [*local_identification*](../configuracoes/parametros-configuracao.md#general) estiver **ativada**.

### POST /new_user_identified.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **identifier_id** *(int)* : ID do identificador (wiegand, RFID, biometria), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **component_id** *(int)* : ID do componente que realizou a identificação (exclusivo para [**iDBlock Next**](../particularidade-dos-produtos/idblock-next.md#eventos-de-identificacao-no-modo-online)), vide [Formatação *identifier_id*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id).
- **event** *(int)* : Evento do resultado da identificação, (e.g.: 3 para não identificado).
- **user_id** *(int)* : ID do usuário.
- **duress** *(int)* : Esse parâmetro retorna um inteiro que indica se é dedo do pânico ou uma simples identificação (1 ser for dedo de pânico ou 0 para identificação comum).
- **face_mask** *(bool)* : Esse parâmetro retorna "true" se o usuário identificado está usando máscara ou retorna "false" caso contrário.
- **time** *(int)* : Momento do acesso em Unix Timestamp.
- **portal_id** *(int)* : Identificador do portal.
- **uuid** *(string)* : Identificador Único Universal.
- **block_read_data** *(string)* : Valor lido do bloco indicado do cartão (apenas para MIFARE, quando habilitado).
- **block_read_error** *(int)* : Indica se houve erro na leitura do bloco do cartão (apenas para MIFARE, quando habilitado).
- **card_value** *(int)* : Valor do cartão utilizado.
- **qrcode_value** *(string)* : Valor do QR Code utilizado.
- **remote_interlock_state** *(string)* : Indica o estado das portas dos dispositivos remotos cadastrados no intertravamento remoto.
- **uhf_tag** *(string)* : Valor lido pela tag UHF
- **pin_value** *(string)* : Valor do PIN utilizado.
- **user_has_image** *(int)* : Indica se o usuário possui imagem (1) ou não (2).
- **user_name** *(string)* : Nome do usuário
- **password** *(string)* : Senha informada pelo usuário.
- **confidence** *(int)* : Parâmetro *confidence* da identificação facial.
- **log_type_id** *(int)* : Envia um identificador de tipo de batida sempre que houver um evento de identificação online.

*Resposta*

- **result** *(Objeto JSON)* : Descrito em [Mensagem de Retorno](../modos-de-operacao/eventos-de-identificacao-online.md#mensagem-de-retorno).

## Evento acesso por botoeira

Indica um acesso por meio de acionamento de botoeira.

O método HTTP usado é o POST. O **contentType** é **application/json**. Todos os parâmetros enviados estão disponíveis no corpo da mensagem (JSON).

Este evento é enviado quando o equipamento opera em Modo Online, ou seja, [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise) ou [Modo Pro](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-pro).

### POST /new_rex_log.fcgi

*Parâmetros*

- **device_id** *(int 64)* : ID único do equipamento.
- **rex_log** *(Objeto JSON)* : Objeto com os parâmetros abaixo:
- - **time** *(int)* : Horário da ocorrência em [Unix Timestamp](https://pt.wikipedia.org/wiki/Era_Unix).
- - **event** *(int)* : Evento da abertura (11 para acesso através de botoeira).
- - **user_id** *(int 64)* : ID do usuário.
- - **portal_id** *(int 64)* : ID do portal correspondente.

*Resposta*

> É esperada uma resposta vazia (HTTP Status code OK).

## Eventos de cadastro

Além dos eventos de identificação, o equipamento em modo online envia ao servidor os resultados de cadastros remotos assíncronos (veja [Cadastro Remoto Biometria, Facial, Cartão, PIN ou Senha](../acoes/cadastro-remoto-biometria-facial-cartao.md)) e de autocadastros realizados no próprio equipamento, pelas rotas descritas abaixo. Em todos os casos é esperada uma resposta HTTP Status code OK.

### POST /template_create.fcgi

Enviado quando ocorre o cadastro remoto de biometria e a configuração [*extract_template*](../configuracoes/parametros-configuracao.md#online_client) está **ativada** (caso contrário, é enviado o *fingerprint_create.fcgi*). O método HTTP usado é o POST. O **contentType** é **application/octet-stream**. Todos os parâmetros são enviados através da query string, exceto o binário do template.

*Parâmetros*

- **user_id** *(int 64)* : ID do usuário.
- **finger_type** *(int)* : 1 se for dedo de pânico ou 0 caso contrário.
- **card_id** *(string)* : Matrícula do usuário (pode ser vazio).
- **temp_num** *(int)* : Número de templates enviados (sempre 1).
- **size0** *(int)* : Tamanho, em bytes, do template enviado.
- **device_id** *(int 64)* : ID único do equipamento.
- **template** *(binário (octet-stream))* : Template biométrico extraído pelo equipamento (**Este é o único parâmetro enviado no corpo da mensagem**).

### POST /face_template_create.fcgi

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Enviado quando ocorre o cadastro remoto de face e a configuração **return_face_template** do módulo **enroller** está definida como **1** (caso contrário, é enviado o *face_create.fcgi*). O método HTTP usado é o POST. O **contentType** é **application/json**.

*Parâmetros*

- **user_id** *(string)* : ID do usuário.
- **registration** *(string)* : Matrícula do usuário (pode ser vazio).
- **device_id** *(string)* : ID único do equipamento.
- **face_template** *(string)* : Template facial codificado em Base64.

### POST /tag_create.fcgi

Disponível apenas em **equipamentos da linha iDAccess**.

Enviado quando ocorre o cadastro remoto de tag. O método HTTP usado é o POST. O **contentType** é **application/json**.

*Parâmetros*

- **user_id** *(int 64)* : ID do usuário.
- **tag_value** *(string)* : Valor da tag.
- **device_id** *(int 64)* : ID único do equipamento.

### POST /new_user_enrolled.fcgi

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Enviado quando um usuário conclui o autocadastro facial no próprio equipamento (modo quiosque) e o servidor deve ser notificado do novo cadastro. O método HTTP usado é o POST. O **contentType** é **application/json**.

*Parâmetros*

- **user_id** *(int 64)* : ID do usuário cadastrado.
- **device_id** *(int 64)* : ID único do equipamento.
- **user_image** *(string)* : Foto do usuário codificada em Base64.
- **face_template** *(string)* : Template facial do usuário codificado em Base64.
- **image_timestamp** *(int)* : Momento de cadastro da foto em Unix Timestamp.

## Formatação *identifier_id*

O campo **identifier_id** está presente nas mensagens de evento de identificação enviadas pelo terminal de acesso. Ele contém um id que representa o mecanismo identificador (wiegand, RFID, biometria, etc) utilizado, e deve ser interpretado da seguinte forma:

O tipo do dado é inteiro de 32 bits. Os três bytes mais significativos devem ser interpretados como caracteres ASCII enquanto o último byte deve ser interpretado como valor inteiro.

Exemplo:

- "w" = 0x77 (ASCII)
- "i" = 0x69 (ASCII)
- "n" = 0x6E (ASCII)
- "0" = 0x00 (Binário)

O valor "win0" representa Wiegand zero. Sua conversão para o formato descrito acima produz o valor 0x77696E00 em hexadecimal e se torna 2003398144 em base decimal.

## Solicitar imagem de usuário ao servidor

O equipamento irá solicitar a foto do usuário para o servidor sempre que na resposta dos eventos de identificação o parâmetro **user_image** for **true** (indicando que o usuário possui foto).

O método HTTP usado é o GET. O **contentType** é **application/octet-stream**. Todos os parâmetros são enviados através da query string.

### GET /user_get_image.fcgi

*Parâmetros*

- **user_id** *(int)* : ID do usuário.

*Resposta*

- **imagem** *(octet-stream)* : Foto do usuário requisitado nos formatos **BMP**, **JPG/JPEG** ou **PNG**;

## Verifica disponibilidade do servidor

Se o equipamento não conseguir se comunicar com o servidor por três tentativas seguidas, ele entrará em **modo de contingência**. O número de tentativas é um parâmetro configurável.

Neste modo, todas as identificações passam a ser feitas utilizando unicamente os registros locais do equipamento, que, para tanto, devem estar atualizados.

A cada minuto, o equipamento envia uma nova requisição **POST/device_is_alive** ao servidor com o número de logs disponíveis no corpo da mensagem (JSON). Assim que obtiver uma resposta (HTTP status code OK), ele voltará a seu modo de operação original.

O método HTTP usado é o POST. O **contentType** é **application/json**.

### POST /device_is_alive.fcgi

*Parâmetros*

- **access_logs** *(int)* : Número de logs disponíveis.

*Resposta*

> É esperada uma resposta vazia (HTTP Status code OK).

## Mensagem de Retorno

Mensagem de retorno do servidor para o equipamento após um evento de tentativa de identificação.

### *result*

Resultado da análise da tentativa de identificação.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **event** | *int* | Tipo do evento, pode ser:  1. Equipamento inválido 2. Parâmetros de identificação inválidos 3. Não identificado 4. Identificação pendente 5. Tempo de identificação esgotado 6. Acesso negado 7. Acesso concedido 8. Acesso pendente (usado quando o acesso depende de mais de uma pessoa) 9. Usuário não é administrador (usado quando um usuário tenta acessar o menu mas não é administrador) 10. Acesso não identificado (quando o portal é aberto através da API e o motivo não é informado) 11. Acesso por botoeira 12. Acesso pela interface web 13. Desistência de entrada (*exclusivo para iDBlock*) 14. Sem resposta (nenhuma ação é tomada) 15. Acesso pela interfonia (*exclusivo para iDFace*)   Valores exclusivos dos **equipamentos da linha iDAccess**:  - 16: Acesso via OSDP (a partir da versão V5.9.4) - 17: Acesso negado por intertravamento (a partir da versão V5.16.0) - 18: Liberação pelo lado oposto (a partir da versão V5.20.0) |
| **user_id** | *int* | ID do usuário, em caso de identificação. |
| **user_name** | *string* | Nome do usuário, em caso de identificação. |
| **user_image** | *bool* | Usuário identificado possui ou não foto. |
| **user_image_hash** | *string* | Caso o usuário identificado possua imagem, envia o hash (SHA1) da mesma. |
| **portal_id** | *string* | ID do portal correspondente. |
| **actions** | *Array de Objetos JSON* | Ações que devem ser executas pelo equipamento. Exemplo: [ {"action":"door", "parameters":"door=1"}, {"action":"door", "parameters":"door=2"} ] |
| **duress** | *int* | Indica se é dedo do pânico ou uma simples identificação (1 ser for dedo de pânico ou 0 para identificação comum). |
| **message** | *string* | Mensagem a ser exibida no display no momento do acesso. |

## Exemplos de respostas

### Resposta para os dispositivos iDAccess, iDFit e iDBox

Autoriza um acesso:

```
{
    "result": {
        "event": 7,
        "user_id": 6,
        "user_name": "Neal Caffrey",
        "user_image": false,
        "portal_id": 1,
        "actions":[
            {"action": "door", "parameters": "door=1"},
            {"action": "door", "parameters": "door=2"}
        ]
    }
}
```

### Resposta para os dispositivos iDFlex, iDAccess Pro e iDAccess Nano

Autoriza um acesso:

```
{
    "result": {
        "event": 7,
        "user_id": 6,
        "user_name": "Neal Caffrey",
        "user_image": false,
        "portal_id": 1,
        "actions": [
            {"action": "sec_box", "parameters": "id=65793, reason=1"}
        ]
    }
}
```

**Nota**: O parâmetro **reason**, define o motivo de abertura (0 = Desconhecido, 1 = Autorizado, 2 = Botoeira e 3 = Comando WEB).

### Resposta específica para a catraca iDBlock

Autoriza um acesso:

```
{
    "result": {
        "event": 7,
        "user_id": 6,
        "user_name": "Danny Boy",
        "user_image": false,
        "portal_id": 1,
        "actions": [
            {"action": "catra", "parameters": "allow=clockwise"}
        ]
    }
}
```

**Nota**: O parâmetro **"clockwise"** abre a catraca no sentido horário. Outros valores possíveis são **"anticlockwise"** ou **"both"** que abrem a catraca no sentido anti-horário ou ambos, respectivamente.
