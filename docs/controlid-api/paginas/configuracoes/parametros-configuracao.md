<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/configuracoes/parametros-configuracao/ -->

# Parâmetros de Configuração

Descrição dos parâmetros de configuração dos equipamentos:

### *general*

Configurações gerais do equipamento.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **auto_reboot_hour** | *string* | Hora em que o equipamento reinicia automaticamente. Valores entre "0" e "23". É necessário reiniciar o equipamento para que alterações nesse parâmetro entrem em vigor. |
| **auto_reboot_minute** | *string* | Minuto em que o equipamento realiza o reinício automático. Valores entre "0" e "59". É necessário reiniciar o equipamento para que alterações nesse parâmetro entrem em vigor. |
| **auto_reboot** | *int* | Chave geral do reinício automático. O reinício agendado só é executado quando este parâmetro é "1"; com "0" o agendamento continua configurado, mas nenhum reinício é realizado. Valores: "0" ou "1". |
| **auto_reboot_days** | *string* | Dias da semana em que o reinício automático é executado, na forma de uma máscara de 7 caracteres "0" e "1", na ordem domingo, segunda, terça, quarta, quinta, sexta e sábado. Por exemplo, "0111110" reinicia de segunda a sexta. A máscara "0000000" é tratada como todos os dias, e não como desativado — para desativar o reinício automático use **auto_reboot** = "0". Uma máscara com tamanho diferente ou com caracteres que não sejam "0" e "1" é rejeitada e o agendamento volta para 01:00 todos os dias. Padrão: "1111111". |
| **clear_expired_users** | *string* | Parâmetro que configura o grupo de pessoas expiradas a ter seus dados limpos após o reinício diário do equipamento. Valores: "all" (usuários e visitantes), "visitors" (somente visitantes) ou "disable" (desabilitado). É necessário reiniciar o equipamento para que alterações nesse parâmetro entrem em vigor. |
| **url_reboot_enabled** | *string* | Habilita a possibilidade de reinício imediato do dispositivo a partir do endpoint "/reboot". Para desabilitar "0", e para habilitar "1". |
| **keep_user_image** | *int* | Parâmetro responsável por manter ou não a foto do usuário após o seu cadastro. Valores: "1", padrão, a foto do usuário é mantida; ou "0", a foto do usuário é removida imediatamente após o cadastro. |
| **beep_enabled** | *string* | Ativa o som de beep do equipamento. Valores: "0" ou "1". |
| **ssh_enabled** | *string* | Ativa a conexão com o dispositivo via SSH, indicando que pode ser acessado remotamente para diagnósticos de engenharia. Para desabilitar "0" e para habilitar "1". |
| **relayN_enabled** | *string* | Habilita o uso do relé N, onde N vai de 1 a 4 indicando o relé (o número máximo de relés depende do produto). Para desabilitar "0" e para habilitar "1". |
| **relayN_timeout** | *string* | Tempo de ativação do relé em milissegundos, onde N vai de 1 a 4 indicando o relé (o número máximo de relés depende do produto). Exemplo: "3000". |
| **relayN_auto_close** | *int* | A configuração do relé pode ser feita de duas formas. Neste caso, N é o número da porta em questão (No caso do iDAccess pode ser 0 ou 1, no caso da catraca é sempre 0 enquanto a iDBox vai de 0 a 3) e a sua configuração pode ser "0" (funcionamento normal) ou "1" (quando o sensor de porta abrir, o relé fechará) - Evita que a porta fique aberta mais tempo do que o necessário. |
| **door_sensorN_enabled** | *string* | Habilita o uso do sensor de porta N, onde N vai de 1 a 4 indicando a porta (o número máximo depende do produto). Para desabilitar "0" e para habilitar "1". |
| **door_sensorN_idle** | *string* | Indica qual o nível lógico do sensor da porta N quando ela estiver fechada, onde N vai de 1 a 4 indicando a porta (o número máximo depende do produto). Valores: "0" ou "1". |
| **doorN_interlock** | *string* | Números das portas que estão intertravadas, separados por vírgula (ex: "1,3"), onde N vai de 1 a 4 indicando a porta (o número máximo depende do produto). |
| **bell_enabled** | *string* | Habilita ou desabilita a campainha. |
| **bell_relay** | *string* | Relé no qual a campainha está conectada, o padrão é "2". |
| **catra_timeout** | *string* | Tempo que a catraca fica liberada para um giro em ms. timeout igual a zero significa tempo infinito, ou seja, liberada até o próximo giro. Exemplo: "3000". **Atenção: método aplicável somente quando o dispositivo for catraca**. |
| **online** | *string* | Esse parâmetro determina se o equipamento deve operar online ([Modo Pro](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-pro) ou [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise)), ou seja, com liberação de acesso processada em servidor externo. Os valores possíveis são "0" ou "1". |
| **local_identification** | *string* | Quando o equipamento opera online, esse parâmetro define se ele está no [Modo Pro](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-pro), em que a identificação é feita no próprio equipamento, ou no [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), em que a identificação é feita no servidor externo. Os valores possíveis são "0" ou "1". |
| **ihm_enterprise_mode** | *string* | Quando a IHM opera online, esse parâmetro define se a ihm está no [Modo Pro](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-pro), em que a identificação é feita no próprio equipamento, ou no [Modo Enterprise](../modos-de-operacao/introducao-aos-modos-de-operacao.md#modo-enterprise), em que a identificação é feita no servidor externo. Os valores possíveis são "0" ou "1". Está função presente apenas para a IHM. |
| **exception_mode** | *string* | Esse parâmetro deve ser definido quando o equipamento precisa permanecer com a entrada sempre liberada ou sempre bloqueada, as opções são **emergency** e **lock_down**. Para sair destes modos, basta definir esta configuração com qualquer outro valor, o padrão é **none**. Quando **emergency** está habilitado, o equipamento permanece com a entrada sempre liberada e a tela e as identificações permanecem travadas. Quando **lock_down** está habilitado, o equipamento permanece com a entrada sempre bloqueada e a tela e as identificações permanecem travadas. |
| **doorN_exception_mode** | *string* | Esse parâmetro - exclusivo para a linha de acesso V3 - configura o comportamento da porta N, onde N varia de 1 a 4, caso o **exception_mode** não seja **emergency** e nem **lock_down**. As opções possíveis são as mesmas do **exception_mode** (**emergency** e **lock_down**) - ambas travam as identificações, mas não travam a tela - sendo que o valor padrão também é **none**. Caso o valor seja **emergency**, a porta permanece liberada. Quando **lock_down** está habilitado, a porta permanece sempre bloqueada. Por fim, para qualquer outro valor, a porta volta a operar normalmente. |
| **language** | *string* | Usado para alterar o idioma do equipamento, os valores possíveis são: **pt_BR** (Português), **spa_SPA** (Espanhol) e **en_US** (Inglês). Após a alteração do idioma do equipamento é necessário reiniciá-lo. |
| **daylight_savings_time_start** | *int* | A data de início do horário de verão (Unix timestamp). |
| **daylight_savings_time_end** | *int* | A data de fim do horário de verão (Unix timestamp). |
| **password_only** | *string* | Quando habilitado e no modo online, o equipamento pedirá somente a senha em vez de pedir o ID e senha para identificação do usuário através da interface gráfica do dispositivo. O request [*new_user_id_and_password*](../modos-de-operacao/eventos-de-identificacao-online.md#evento-id-e-senha) enviado pelo dispositivo não muda, o user_id virá zerado. Utilize os valores "1" ou "0" para habilitar ou desabilitar respectivamente. |
| **hide_password_only** | *string* | Quando é atribuído o valor "0" a este parâmetro e **password_only** (descrito acima) está habilitado, o equipamento não esconderá a senha que é digitada. O padrão é "1". |
| **password_only_tip** | *string* | Esta configuração muda o texto da dica que é mostrada quando **password_only** está habilitado. Quando vazio, mostra a mensagem padrão. O valor padrão é vazio. |
| **hide_name_on_identification** | *string* | Ocultar nome no acesso. Uma vez ativado, o nome do usuário não será exibido na badge de identificação, tanto para acesso autorizado quanto não autorizado. Para desabilitar "0", e para habilitar "1". |
| **denied_transaction_code** | *int* | Código de transação negada. Define o código que será enviado pela Wiegand caso **send_code_when_not_identified** e/ou **send_code_when_not_authorized** estejam habilitados. Se o parâmetro **out_mode** da *sec_box* tiver o valor "RELAY_CARD", o código de transação negada é ignorado. |
| **send_code_when_not_identified** | *int* | Habilita o envio de código de transação negada quando um evento de usuário não identificado ocorre. |
| **send_code_when_not_authorized** | *int* | Habilita o envio de código de transação negada quando um evento de usuário não autorizado ocorre. |
| **screen_always_on** | *int* | No iDFace, mantém a tela sempre ligada quando "1" e desliga a tela quando o equipamento está ocioso quando o seu valor é "0". |
| **web_server_enabled** | *string* | Habilita a interface Web. A API continua completamente funcional, apenas a interface Web é alterada. Para desabilitar "0" e para habilitar "1". |

### *catra*

Configurações específicas da catraca.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **anti_passback** | *string* | Habilita ou desabilita o controle de anti-dupla entrada. |
| **daily_reset** | *string* | "0" ou "1". Habilita o *reset* de logs para o controle de anti-dupla entrada. Os acessos serão apagados todos os dias à meia-noite. |
| **gateway** | *string* | Sentido da entrada. Deve ser "clockwise" ou "anticlockwise" (horário ou anti-horário respectivamente). |
| **operation_mode** | *string* | Modo de operação da catraca. Controla quais sentidos da catraca serão controlados ou liberados. Deve ser "blocked", "entrance_open", "exit_open", "both_open" (Ambas controladas, entrada liberada, saída liberada e ambas liberadas respectivamente). |
| **relay_timeout** | *int* | Tempo de acionamento, em milissegundos, do relé da catraca. O valor "0" desabilita o acionamento do relé. |

### *opening_times*

Essa configuração permite definir qual o tempo de abertura da porta em milissegundos.

**Atenção:** *opening_times* não é um módulo de configuração, e sim uma tabela do banco de dados do equipamento. Seus registros não são alterados por *set_configuration*; devem ser manipulados através das funções de manipulação de objetos (veja a [Lista de Objetos](../objetos/lista-de-objetos.md)).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único da regra de tempo de abertura especial. |
| **user_id** | *int 64* | Inteiro indicando o usuário (referência à tabela users). |
| **door_id** | *int 64* | Inteiro indicando a porta (valor inicial 1). |
| **time** | *int* | Tempo diferenciado de abertura em milissegundos. |

### Parâmetros de Energia

Esses parâmetros permitem ajustar configurações de energia e pertencem ao módulo *general*. Disponíveis no iDFace Max.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **energy_mode** | *int* | Determina o modo de energia. Valores: 0 (Padrão), 1 (fonte 2A), 2 (fechadura + PoE) e 3 (Personalizado). |
| **energy_display_custom** | *double* | Determina o valor máximo do display ao usar o modo personalizado |
| **energy_sound_custom** | *double* | Determina o valor máximo do alto-falante ao usar o modo personalizado |
| **energy_ir_custom** | *double* | Determina o valor máximo do LED infravermelho ao usar o modo personalizado |
| **energy_led_custom** | *double* | Determina o valor máximo do LED branco ao usar o modo personalizado |

### *rs485*

Parâmetros de configuração da interface de comunicação RS485.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **enabled** | *int* | Habilita ou desabilita a transmissão de sinal via protocolo RS485. Os valores de entrada possíveis são: "0" (desabilitado) ou "1" (habilitado). Por padrão, o valor de entrada é "0", ou seja, a comunicação padrão não ocorrerá via RS485 e será somente via módulo de acionamento externo (MAE/Security Box). |
| **legacy_mode** | *int* | Define o modo de comunicação da interface RS485. Os valores de entrada possíveis são: "0", "1" ou "2". Para a entrada "0", a comunicação se comportará de acordo com o manual de comunicação serial RS485 disponível em [Manual de Comunicação Serial RS485](https://www.controlid.com.br/manual/Manual_de_Comunicacao_Serial_RS485.pdf). Para a entrada "1", ocorrerá o envio do ID, nome e matrícula do usuário autorizado no seguinte formato em hexadecimal: "01 02 < ID até 4 char > 00 00 < nome até 16 char > 00 00 < Matrícula até 6 char > 03 0D". Por fim, se for inserida a entrada "2", a transmissão será do ID do usuário em formato ASCII com 20 dígitos, sem quebra de linha. Por padrão, o valor de entrada de fábrica é "0". \*Este parâmetro só está disponível para os equipamentos: iDFlex, iDAccess Pro, iDAccess Nano e iDFace. |
| **receive_timeout** | *int* | Timeout das transmissões de dados (em milissegundos). O valor padrão deste parâmetro é 2000 ms. |

### *mifare*

A configuração do módulo MIFARE permite que a quantidade de bits que a leitora do equipamento vai ler seja configurável, podendo ser 24 ou 32 bits.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **byte_order** | *string* | O byte_order possui dois possíveis valores "W_26" (MIFARE c/ 24 bits de informação) usado para manter compatibilidade com plantas antigas e "LSB" (Least Significant Byte) onde lê 32 bits de dados e inverte a ordem dos bytes, qualquer configuração diferente de "W_26" irá operar no modo "LSB". |
| **read_sector** | *string* | Indica qual setor do cartão MIFARE será automaticamente lido quando aproximado do leitor. Quando vazio, nenhum setor será lido. O padrão é vazio. |
| **read_block** | *string* | Indica qual bloco do cartão MIFARE será automaticamente lido quando aproximado do leitor. Quando vazio, nenhum bloco será lido. O padrão é vazio. |
| **authentication_type** | *string* | Tipo de chave que será usado para autenticação do bloco indicado por **read_block**. Pode ser "A" ou "B". Valor padrão: "A". |
| **authentication_key** | *string* | Valor em base64 de chave usada para autenticar o bloco indicado por **read_block**. Valor padrão: "////////" (ou "ffffffffffff" em hexadecimal). |

### *rfid*

A configuração do módulo RFID permite que, para cartões ASK, a quantidade de bits que a leitora do equipamento vai ler seja configurável, podendo ser 0 ou 8 bits para tamanho de código de área, e 16, 24, 32 ou 40 bits para o tamanho do código de usuário.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **ask_site_code_size** | *string* | Tamanho do código de área, possui dois possíveis valores "0" bits e "8" bits. O valor de "0" bits possibilita que o tamanho do código de usuário, indicado por **ask_user_code_size**, seja de "40" bits. Padrão é "8" bits. |
| **ask_user_code_size** | *string* | Tamanho do código de usuário, possui quatro possíveis valores "16" bits, "24" bits, "32" bits e "40" bits. O valor de "40" bits só é válido se o tamanho do código de área, indicado por **ask_site_code_size**, for "0" bits. Padrão é "16" bits. |
| **ask_site_code_shift** | *int* | Posição, em bits, em que o site code começa dentro do dado do cartão ASK, usada para separá-lo do user code. A soma de **ask_site_code_shift** e **ask_site_code_size** não pode exceder o comprimento do dado do cartão. |

### *hid_reader* NEW!

Configuração do módulo de leitura HID. Possibilita operação do leitor com diferentes formatos de cartões. Durante a operação, apenas um formato deve estar ativo ("1"), enquanto os demais permanecem desativados ("0").

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **format_w37** | *string* | Configura operação do leitor HID com formato W37. Valores: "0" ou "1". |
| **w37_cardid_size** | *string* | Define número de bits de identificação do formato W37, desconsiderando bits de paridade. Valores: "19", "25" ou "35". |
| **format_w26** | *string* | Configura operação do leitor HID com formato W26. Valores: "0" ou "1". |
| **format_mifare** | *string* | Configura operação do leitor HID com formato Mifare. Valores: "0" ou "1". |
| **format_indala_b1** | *string* | Configura operação do leitor HID com formato Indala-B1. Valores: "0" ou "1". |
| **format_ask** | *string* | Configura operação do leitor HID com formato ASK. Valores: "0" ou "1". |
| **ignore_facility** | *string* | Possibilita ignorar bits referentes ao facility code do cartão ASK. Se ativo "1", os bits são ignorados, se não "0", os bits são considerados. Valores: "0" ou "1". |
| **card_process** | *int* | Define como o leitor OMNIKEY/HID processa o dado do cartão. Valores: "0" (`PACS_ONLY`), "1" (`CSN_ONLY`), "2" (`CSN_MIFARE`), "3" (`CUSTOM_ONLY`, usa exclusivamente `custom_data[custom_use_index]` como PACS; se não houver dado custom disponível, a leitura é recusada) ou "4" (`CUSTOM_PACS`, igual ao "3", mas com fallback para o fluxo `PACS_ONLY` normal caso não haja dado custom disponível). |
| **custom_use_index** | *int* | Só tem efeito quando **card_process** está em "3" ou "4". O leitor OMNIKEY pode devolver uma lista de blocos de dados "customizados" lidos do cartão (ex.: blocos arbitrários de um MIFARE 1K); este parâmetro escolhe qual índice dessa lista deve ser tratado como o dado válido do cartão. Padrão: "0". |
| **toc_crypt_mode** | *int* | Disponível apenas no **iDFace Max HID**. Modo de criptografia de dados no cartão (Template on Card / ToC) para leitores HID. Valores: "0" (sem criptografia) ou "1"/"2" (modos de criptografia). Padrão de fábrica: "0". |
| **toc_crypt_key** | *string* | Disponível apenas no **iDFace Max HID**. Chave de criptografia usada no modo Template on Card. Se estiver vazia, o firmware gera automaticamente uma chave a partir do número de série do dispositivo (SHA-256) no primeiro boot e a persiste — o valor vazio de fábrica não reflete o estado real do equipamento após o primeiro uso. |
| **toc_site_key** | *string* | Disponível apenas no **iDFace Max HID**. Site key do leitor HID usada no modo Template on Card. Padrão: vazio. |

### *osdp* NEW!

Parâmetros de configuração relacionados às definições de saída via protocolo OSDP.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **enabled** | *int* | Habilita o protocolo OSDP no dispositivo. Ele pode ter dois valores, "0" para protocolo desabilitado e "1" para protocolo habilitado. |
| **pd_address** | *string* | Define o endereço do módulo. Pode receber números inteiros de até 2 dígitos. |
| **baud_rate** | *int* | Configura a taxa de transmissão dos dados em bit/s. Ele pode receber os valores: 9600, 19200, 38400, 57600, 115200 e 230400. |
| **card_read_report_format** | *string* | Configura o formato dos dados transmitidos. Ele pode receber três valores: "raw" para binário (padrão), "wiegand" para Wiegand ou "ascii" para ASCII. |
| **wiegand_size** | *int* | Define o número de bits de saída do Wiegand. Pode ter os valores: 26 (padrão), 32, 34 e 66. |
| **enforce_secure_channel** | *int* | Habilita o canal seguro obrigatório. Ele pode ter dois valores, "0" para canal desabilitado e "1" para canal habilitado. |
| **out_mode** | *int* | Configura o tipo de saída de dados via OSDP. Valores: "0" (`ID`, envia o *user_id* do usuário identificado), "1" (`AUTH_CARD`, **novo padrão de fábrica**; só em acesso concedido, envia o cartão do usuário ou a credencial facial vinculada), "2" (`ANY_CARD`, modo "Passthrough": encaminha qualquer leitura — cartão/QR/PIN/rosto — ao host OSDP, independentemente do resultado da identificação local; é o único modo em que **remote_validation_enabled**, abaixo, tem efeito) ou "3" (`USER_CARD` - modo legado, sempre envia o primeiro cartão do usuário, mesmo em evento negado — não é oferecido pela tela web, só é atingível definindo o parâmetro diretamente). |
| **remote_validation_enabled** | *int* | Habilita um fluxo de "validação remota" pelo protocolo OSDP nativo, disponível apenas quando **out_mode** = "2" (`ANY_CARD`/Passthrough). Ao identificar um cartão/QR/PIN/rosto em modo Passthrough, o equipamento não decide localmente — notifica o host OSDP com um evento `OSDP_CMD_LED` de aviso e aguarda a resposta do próprio host, também via protocolo OSDP (comando `OSDP_CMD_LED` de volta: verde = autorizado, outra cor = negado), dentro da janela definida por **remote_validation_timeout_ms**. Se o timer expirar sem resposta, o evento é resolvido como timeout de identificação. Não existe um comando FCGI para o cliente "responder" à validação — a resposta é feita pelo painel/host via protocolo OSDP, fora da API HTTP. Valores: "0" ou "1". Padrão: "0". |
| **remote_validation_timeout_ms** | *int* | Janela de tempo, em milissegundos, para a resposta da validação remota descrita em **remote_validation_enabled**. Padrão: "5000". |
| **use_ascii_prefix** | *int* | Só tem efeito quando **card_read_report_format** é "ascii". Com "1", o dado enviado ao host recebe um prefixo com o tipo da credencial seguido de dois-pontos (por exemplo `card:12345`); com "0", apenas o valor é enviado. Valores: "0" ou "1". Padrão: "0". |
| **led_controller_confirmation** | *int* | Permite que o host OSDP confirme o resultado da identificação por meio de um comando `OSDP_CMD_LED` (verde = autorizado, qualquer outra cor = negado). Quando "0" e **remote_validation_enabled** também é "0", os comandos de LED recebidos são ignorados. Valores: "0" ou "1". Padrão: "0". |
| **bypass_unlock_status** | *int* | Armazena o estado atual de bypass/destravamento solicitado pelo host por meio de um comando `OSDP_CMD_LED` permanente. É escrito pelo próprio firmware sempre que o estado muda e é lido na inicialização para restaurar o estado anterior. Valores: "0" (travado) ou "1" (destravado). Padrão: "0". |
| **special_function_keys_enabled** | *int* | Habilita as teclas de função especial do teclado do equipamento na operação por OSDP. Valores: "0" ou "1". Padrão: "1". |
| **auto_keypad_after_card** | *int* | Com "1", o teclado é aberto automaticamente logo após a leitura de um cartão, para que o usuário digite o PIN sem precisar pressionar uma tecla adicional. Valores: "0" ou "1". Padrão: "0". |
| **auto_keypad_timeout** | *int* | Tempo, em segundos, que o teclado aberto automaticamente permanece disponível antes de ser fechado. Só tem efeito quando **auto_keypad_after_card** é "1". |
| **osdp_text_debounce_time** | *int* | Intervalo mínimo, em segundos, entre duas mensagens de texto recebidas do host por `OSDP_CMD_TEXT`. Mensagens que chegam dentro dessa janela são descartadas, evitando que a tela pisque quando o host repete o comando. Com "0", não há debounce. Padrão: "10". |
| **expansion_rs485** | *int* | Seleciona qual porta serial o módulo OSDP utiliza. Com "1", é usada a porta RS485 de expansão; com "0", a porta padrão. Valores: "0" ou "1". Padrão: "0". É necessário reiniciar o equipamento para que a alteração deste parâmetro tenha efeito. |
| **serial_port_timeout** | *int* | Timeout de leitura da porta serial do OSDP, em milissegundos. Um valor "0" ou menor é substituído por "5000", já que um bloqueio indefinido travaria a comunicação quando nenhuma controladora está conectada. Padrão: "5000". |

### *card_readerN*

Configuração da entrada **wiegand**, em que o **N** em **card_readerN** representa o número da **wiegand in**. Por exemplo, no iDAccess é sempre zero, ou seja, **card_reader0**, enquanto que na iDBlock tem-se as opções 0 para a wiegand principal e 1 para wiegand da urna, e na iDBox, 4 opções (de 0 a 3), correspondendo à quantidade de leitoras wiegand.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **mode** | *string* | A configuração *mode* pode ter dois valores, "WIN" para wiegand e "MAG" para cartão magnético (ABA Track II, disponível nos modelos iDBlock H2, iDFit H2 e iDAccess H2). É importante sempre **reiniciar o equipamento** após a mudança de *mode*. |
| **type** | *int* | A configuração *type* pode ter dois estados: "LSB" (Least Significant Byte) ou "" (vazio). Quando a leitura for wiegand 34, é invertida a ordem dos bytes; para qualquer outro valor ("") a inversão não será realizada. |

### *alarm*

Parâmetros de configuração de alarme do equipamento.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **door_sensor_enabled** | *string* | Habilita ou desabilita o sensor de porta. Os valores possíveis são "0" ou "1" para desabilitar e habilitar respectivamente. |
| **door_sensor_delay** | *string* | Define o tempo em segundos em que a porta deverá permanecer aberta antes que o alarme de porta aberta seja gerado. O valor padrão é "5". |
| **forced_access_enabled** | *string* | Habilita ou desabilita a detecção de arrombamento de porta. Os valores possíveis são "0" ou "1" para desabilitar e habilitar respectivamente. |
| **siren_enabled** | *string* | Habilita ou desabilita a sirene (aplicável apenas para o iDAccess e iDFit). Para que a sirene seja acionada por um relé, é necessário especificar o número através do parâmetro **siren_relay**. Os valores possíveis são "0" ou "1" para desabilitar e habilitar respectivamente. |
| **siren_relay** | *string* | Relé no qual a sirene está conectada, o padrão é "2" (aplicável apenas para o iDAccess e iDFit). |
| **timed_alarm_timeout** | *int* | Indica o tempo em segundos que o dispositivo deixará a sirene acionada. |

### *identifier* NEW!

Configurações do identificador.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **card_identification_enabled** | *int* | Disponível para dispositivos iDFace a partir da versão 6.5.1. Determina se a identificação por cartão está habilitada. Valores: "0" para desativada ou "1" para ativada. |
| **face_identification_enabled** | *int* | Disponível para dispositivos iDFace a partir da versão 6.5.1. Determina se a identificação facial está habilitada. Valores: "0" para desativada ou "1" para ativada. |
| **qrcode_identification_enabled** | *int* | Disponível para dispositivos iDFace a partir da versão 6.5.1. Determina se a identificação por QR Code está habilitada. Valores: "0" para desativada ou "1" para ativada. |
| **pin_identification_enabled** | *int* | No momento só está disponível para o iDFace a partir da versão 6.9.0. Em breve estará disponível para os equipamentos da linha V5. Determina se a identificação habilitada é o modo de ID + Senha ou o modo PIN. Valores: "0" para ativar apenas o modo de ID + Senha e "1" para habilitar apenas o modo PIN. |
| **log_type** | *bool* | Esta configuração só é válida para o iDFlex ponto (apesar de existir para os outros dispositivos) e determina se as batidas customizadas (tipos de batidas) estão habilitadas. |
| **multi_factor_authentication** | *int* | Esta configuração determina se a autenticação múltipla deve ser utilizada. Para a linha de acesso V5: 0 = Desabilitado 1 = Cartão + Biometria Para dispositivos iDFace: 0 = Desabilitado 1 = identificação qualquer: {Cartão, Senha, QR Code ou PIN} + Face. |
| **verbose_logging** | *string* | Se ativado, registra todos os tipos de acesso, inclusive não identificados. Utiliza mais memória. Se desativado, registra apenas acessos autorizados e não autorizados. Valores: "0" ou "1". |
| **antipassback_enabled** | *string* | Habilita ou desabilita a configuração de anti-passback, que bloqueia acessos repetidos de uma mesma pessoa. Valores: "0" ou "1". |
| **antipassback_mode** | *string* | Modo de operação do anti-passback, por padrão possui o valor "timed". Valores: "daily_catra" para acesso diário de catraca, "timed_catra" para bloqueio por tempo para catracas e "timed" para bloqueio por tempo em outros dispositivos. |
| **antipassback_timeout** | *int* | Tempo de bloqueio do anti-passback quando os modos "timed" ou "timed_catra" estão em uso. Valor especificado em minutos. |
| **two_person_auth_enabled** | *string* | Disponível apenas no **iDFace Max HID**. Habilita globalmente a regra de autenticação de 2 pessoas (escort/two-person auth). Valores: "0" ou "1". Padrão de fábrica: "1". |
| **enable_custom_auth_message** | *int* | Habilita a mensagem personalizada exibida no acesso autorizado, definida em **custom_auth_message**. Com "0", é exibida a mensagem padrão. Valores: "0" ou "1". |
| **custom_auth_message** | *string* | Texto exibido na tela do equipamento no acesso autorizado quando **enable_custom_auth_message** é "1". |
| **enable_custom_deny_message** | *int* | Habilita a mensagem personalizada exibida no acesso negado, definida em **custom_deny_message**. Valores: "0" ou "1". |
| **custom_deny_message** | *string* | Texto exibido na tela do equipamento no acesso negado quando **enable_custom_deny_message** é "1". |
| **enable_custom_not_identified_message** | *int* | Habilita a mensagem personalizada exibida quando o usuário não é identificado, definida em **custom_not_identified_message**. Valores: "0" ou "1". |
| **custom_not_identified_message** | *string* | Texto exibido na tela do equipamento quando o usuário não é identificado e **enable_custom_not_identified_message** é "1". |
| **enable_custom_mask_message** | *int* | Habilita a mensagem personalizada exibida quando a detecção de máscara recusa o acesso, definida em **custom_mask_message**. Valores: "0" ou "1". |
| **custom_mask_message** | *string* | Texto exibido na tela do equipamento pela detecção de máscara quando **enable_custom_mask_message** é "1". |
| **kiosk_block_enabled** | *int* | Habilita o bloqueio temporário da leitura de QR Code e PIN no modo quiosque após tentativas consecutivas sem sucesso. Valores: "0" ou "1". Padrão: "1". |
| **kiosk_max_attempts** | *int* | Número de tentativas consecutivas sem sucesso que acionam o bloqueio do modo quiosque. Um valor inválido volta para "3". Padrão: "3". |
| **kiosk_block_interval** | *int* | Duração do bloqueio do modo quiosque, em segundos, contada a partir da última tentativa recusada. Padrão: "60". |
| **identifier_event_timeout** | *int* | Tempo máximo, em milissegundos, que o equipamento aguarda a resolução do evento de identificação antes de descartá-lo. |
| **multi_factor_delay** | *int* | Tempo, em milissegundos, permitido entre os dois fatores quando **multi_factor_authentication** está habilitado. Passada essa janela, o primeiro fator é descartado e a identificação recomeça. |
| **attendance_id_timeout** | *int* | Tempo, em milissegundos, que a tela do modo ponto aguarda o usuário digitar a identificação antes de voltar para a tela ociosa. |
| **biometry_identification_enabled** | *int* | Determina se a identificação por biometria digital está habilitada. Valores: "0" para desabilitado ou "1" para habilitado. |

### *face_module*

Configurações sobre o uso dos LEDs durante a identificação.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **led_ir_brightness** | *int* | Determina o brilho do LED infravermelho. Valores: "0" até "100". |
| **light_threshold_led_activation** | *int* | Determina o limite de luminosidade para ativar o LED branco durante a identificação. Valores: "0" (brilho baixo), "1" (brilho médio), "2" (brilho alto), "3" (ligado em qualquer luminosidade do ambiente). |

### *bio_id*

Configurações da biometria do equipamento.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **similarity_threshold_1ton** | *string* | Quanto maior o número definido, mais rigorosa será a identificação. Aumentar este número irá diminuir a taxa de falsas aceitações e aumentar a taxa de falsas rejeições, diminuí-lo terá o efeito inverso. Valor padrão: "0". |

### *online_client*

Configurações do *client* (equipamento) quando em modo enterprise.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **server_id** | *int 64* | Id do servidor de acesso na tabela **devices**, que indica para quem serão enviados os eventos de identificação. |
| **extract_template** | *string* | Configura se, em caso de identificação por biometria, o equipamento irá extrair o template da digital e enviá-lo ou se irá enviar a imagem da digital. Valores: "0" ou "1". |
| **contingency_enabled** | *int* | Indica se o terminal deve ou não entrar em contingência quando houver falha na comunicação com o servidor de acesso. Valores: 0 ou 1. Valor 0 indica modo de contingência desativado, o valor 1 indica modo de contingência ativado. |
| **max_connection_attempts** | *int* | Número de tentativas consecutivas sem sucesso de alcançar o servidor antes de o equipamento entrar em contingência. |
| **server_default_response_time** | *int* | Tempo, em milissegundos, que o equipamento aguarda a resposta do servidor antes de aplicar a ação padrão do modo online. |
| **extract_face_template** | *int* | Define o que o [cadastro remoto](../acoes/cadastro-remoto-biometria-facial-cartao.md) síncrono do tipo "face" retorna: com "0", a imagem do usuário (**user_image**); com "1", o template facial (**face_template**). Valores: "0" ou "1". Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |
| **max_request_attempts** | *int* | Quantidade de tentativas que o equipamento faz para se comunicar com o servidor antes de entrar no modo de contingência. |
| **request_timeout** | *int* | Indica o tempo de timeout em milissegundos que o dispositivo irá aguardar pela resposta das requisições. O tempo máximo permitido é de 5000 ms. |
| **alive_interval** | *int* | Indica o intervalo de tempo em milissegundos em que o dispositivo irá tentar voltar para o modo online quando em contingência (device_is_alive). |

### *monitor*

Parâmetros de configuração do [*Monitor*](../monitor/introducao-ao-monitor.md).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **request_timeout** | *int* | O tempo, em ms, para a requisição atingir o timeout. |
| **hostname** | *string* | O endereço para onde a requisição será enviada, por exemplo, o IP do servidor. |
| **infer** | *int* | Com "1", o equipamento atualiza automaticamente o **hostname** com o endereço do último servidor que o contatou, de modo que o monitor acompanhe o servidor sem reconfiguração manual. Valores: "0" ou "1". |
| **port** | *string* | A porta para onde a requisição será enviada. |
| **path** | *string* | Este parâmetro define o caminho que compõe o endpoint onde os eventos do monitor são notificados. O valor padrão deste parâmetro é "api/notifications". |
| **inform_access_event_id** | *int* | Determina se o id da entrada na tabela [access_events](../objetos/lista-de-objetos.md#access_events) deve ser reportado nos endpoints *catra_event*, *door* e *secbox*. |
| **alive_interval** | *int* | Intervalo de tempo em ms entre as requisições de *device_is_alive* enviadas pelo dispositivo quando o modo *standalone* está habilitado. |

### *push_server*

Parâmetros de configuração do [*Push*](../modo-push/introducao-ao-push.md).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **push_request_timeout** | *int* | Timeout das requisições do equipamento para o servidor em milissegundos. |
| **push_request_period** | *int* | Período entre as requisições de push em segundos. |
| **push_remote_address** | *string* | Endereço ip e porta em que o servidor está rodando separados por **':'**. Exemplo: *192.168.120.94:80* |

### *uhf*

Parâmetros de configuração específicos da antena iDUHF.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **identification_bits** | *int* | Indica quantos bits serão usados na identificação da tag UHF, este valor varia dependendo do modo de operação: "Standard" e "Extended". Se for selecionado o modo "Standard", o valor será igual ao valor da saída Wiegand em bits, podendo ser 26, 32, 34 ou 66 bits. Se for selecionado o modo "Extended", serão utilizados 96 bits. Valor padrão: 24 bits no modo "Standard". |
| **reader_type** | *string* | Define a ordem dos bytes, podendo ter os seguintes valores: "lsb" ou "default". Valor padrão: "default". |
| **read_interval** | *int* | Define o intervalo mínimo, em milissegundos, para que duas leituras consecutivas da MESMA tag sejam consideradas leituras distintas, evitando leitura duplicada acidental. Valor mínimo: 100. Valor máximo: 30000. Valor padrão: 5000. |
| **read_interval_diff_tags** | *int* | Define o intervalo mínimo, em milissegundos, para que duas leituras consecutivas de tags DIFERENTES sejam consideradas leituras distintas, evitando leitura duplicada acidental. Valor mínimo: 250. Valor máximo: 30000. Valor padrão: 100. |
| **transmit_power** | *int* | Define a potência de transmissão da antena. Este valor é dividido por 100 para obter o valor em dBm. Valor mínimo: 1500. Valor máximo: 2400. Valor padrão: 2400. |
| **work_channel** | *string* | Define os canais de operação da antena que serão usados. Exemplo: permitir canais 1, 2, 3, 4, 5, 7, 8, 9 e 10: "1-5;7-10" |
| **operation_mode** | *string* | Define o modo de operação, "continuous" para modo contínuo, "trigger" para modo trigger e "inhibit" para modo inibição. No modo contínuo, o dispositivo realizará a leitura de tags independentemente do estado do pino de trigger. No modo trigger, o dispositivo realizará a leitura durante o período de timeout sempre que houver uma borda de descida (pulso) no pino de trigger. Já no modo inibição, o dispositivo não realizará a leitura enquanto o pino de trigger estiver ativado. Quando o pino for desativado, após o período de timeout, o dispositivo voltará a realizar a leitura das tags. Valor padrão: "continuous". |
| **trigger_timeout** | *int* | Define o timeout do trigger, em milissegundos. Usado somente quando operation_mode for configurado para modo trigger. Valor mínimo: 250. Valor máximo: 60000. Valor padrão: desativado. |
| **trig_idle** | *int* | Indica qual o nível lógico do trigger quando ele estiver ocioso. Valores: "0" ou "1". Valor padrão: 1. |
| **tag_detector_enabled** | *int* | Indica o modo de funcionamento do relé interno da iDUHF. Caso seja "0", o dispositivo assume o comportamento padrão em relação à abertura do relé interno. Caso seja "1", sempre que uma tag estiver visível, o relé estará ativo. Logo, enquanto uma ou mais tags estiverem na frente da antena, o relé ficará ativo. Se estiver habilitada, o relé interno da iDUHF fica reservado a essa funcionalidade. Valor padrão: 0. |

### *sec_box* NEW!

A maioria das configurações do módulo de acionamento externo (MAE/Security Box) usado no iDFlex, iDAccess Pro, iDAccess Nano e iDFace, por estarem armazenadas em banco de dados, necessitam ser alteradas através do objeto [*sec_boxs*](../objetos/lista-de-objetos.md#sec_boxs), e manipuladas através das funções de manipulação de objetos. As demais configurações, internas ao módulo, encontram-se descritas a seguir.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **mode** | *string* | Indica a posição do bit mais significativo do protocolo Wiegand. Assume o valor ("LSB") se o bit mais significativo está à direita e ("") caso esteja à esquerda (MSB, Padrão). |
| **wiegand_out_size** | *string* | Define o número de bits de saída do protocolo Wiegand. Valores: "26", "32", "34", "35", "37", "40", "42", "66". |
| **out_mode** | *string* | Define o tipo de dado de saída via protocolo Wiegand, "" para transmissão do ID do usuário (padrão), "CARD" para transmissão de cartões autorizados, "RELAY_CARD" para transmissão de qualquer cartão e "USERS_CARD" para transmissão do primeiro cartão associado ao usuário. |
| **passthrough_enabled** | *int* | Com "1", toda leitura de credencial é encaminhada para a saída Wiegand, independentemente do resultado da identificação local. Valores: "0" ou "1". |
| **alarm_uid** | *string* | Identificador do módulo responsável pelo alarme, como inteiro sem sinal. O valor "0" indica que nenhum módulo está associado ao alarme. |
| **v2_check_timeout** | *int* | Tempo, em milissegundos, que o equipamento aguarda a resposta da verificação do protocolo versão 2 do módulo antes de considerá-lo indisponível. |
| **security_mode** | *int* | Modo de segurança da comunicação com o módulo externo. Valores: "0" ou "1". |
| **catra_role** | *int* | Papel do equipamento em um conjunto de catraca. Valores: "0" (nenhum), "1" (primário) ou "2" (secundário). Alterar este parâmetro reinicia o banco de dados do equipamento. |
| **catra_relay_action** | *string* | Define quando o relé da catraca é acionado. Com "authorized", o relé é acionado na liberação; com qualquer outro valor o relé não é acionado na liberação. |
| **catra_relay_1_enabled** | *int* | Habilita o relé 1 da catraca. Valores: "0" ou "1". |
| **catra_relay_2_enabled** | *int* | Habilita o relé 2 da catraca. Valores: "0" ou "1". |
| **catra_relay_1_enable_direction** | *string* | Sentido de giro que aciona o relé 1 da catraca. Valores: "left" (sentido anti-horário) ou "right" (sentido horário). Padrão: "left". |
| **catra_relay_2_enable_direction** | *string* | Sentido de giro que aciona o relé 2 da catraca. Valores: "left" (sentido anti-horário) ou "right" (sentido horário). Padrão: "right". |
| **catra_side_to_enter** | *int* | Lado da catraca considerado como entrada, usado para classificar o sentido do acesso. |
| **catra_single_liberate_mode** | *int* | Com "1", cada liberação permite um único giro da catraca. Valores: "0" ou "1". |
| **catra_collect_visitor_card** | *int* | Com "1", a catraca recolhe o cartão de visitante na saída. Valores: "0" ou "1". |
| **catra_rgb_color** | *string* | Cor usada pelo indicador RGB da catraca. |
| **catra_show_counters** | *int* | Com "1", os contadores de acesso da catraca são exibidos na tela do equipamento. Valores: "0" ou "1". |
| **catra_left_counter** | *int* | Habilita a exibição, na tela do equipamento, do contador de giros para a esquerda. Valores: "0" para ocultar ou "1" para exibir. |
| **catra_right_counter** | *int* | Habilita a exibição, na tela do equipamento, do contador de giros para a direita. Valores: "0" para ocultar ou "1" para exibir. |
| **catra_total_counter** | *int* | Habilita a exibição, na tela do equipamento, do contador de giros totais (nos dois sentidos). Valores: "0" para ocultar ou "1" para exibir. |
| **catra_sync_period** | *int* | Intervalo, em milissegundos, entre as sincronizações de usuários entre o primário e o secundário de um conjunto de catraca. |
| **catra_config_sync_period** | *int* | Intervalo, em segundos, entre as sincronizações de configuração entre o primário e o secundário de um conjunto de catraca. |
| **catra_default_fsm** | *int* | Máquina de estados padrão usada pelo controle da catraca. |

### *w_out0*

Parâmetros de configuração referentes à saída Wiegand dos equipamentos sem módulo de acionamento externo (MAE/Security Box). Através desse módulo, é possível realizar a configuração do modo de saída e tamanho do seu protocolo.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **size** | *string* | Define o número de bits de saída do protocolo Wiegand. Valores: "26", "32", "34", "35", "37", "40", "42", "66". |
| **data** | *string* | Define o tipo de dado de saída via protocolo Wiegand, ("") para transmissão de ID (Padrão), "CARD" para transmissão de cartões autorizados e "RELAY_CARD" para transmissão de qualquer cartão. |

### *wiegand* NEW!

Configurações da saída Wiegand compartilhadas por todas as interfaces Wiegand do equipamento. Estes parâmetros são definidos no módulo *wiegand*, distintos das instâncias numeradas de saída descritas em *w_out0* acima.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **wiegand_format_size** | *int* | Número de bits de saída do protocolo Wiegand. Ao ser definido, também atualiza o parâmetro equivalente dos módulos *sec_box* e *osdp*, de modo que as três interfaces sempre usem o mesmo tamanho. Se o tamanho solicitado não corresponder a um formato conhecido, o equipamento volta para o formato padrão e reescreve os três parâmetros com esse valor. |
| **default_issue_code** | *int* | Issue code enviado na saída Wiegand para formatos que possuem esse campo, quando a própria credencial não define um. |
| **biometric_mismatch_issue_code** | *int* | Issue code enviado na saída Wiegand quando a leitura biométrica não corresponde à credencial apresentada, permitindo que a controladora distinga esse caso de uma leitura normal. |
| **parse_user_id_as_card** | *int* | Com "1", o *user_id* é enviado na saída Wiegand como se fosse o número do cartão, para instalações que indexam a controladora pelo identificador do usuário em vez do cartão. Valores: "0" ou "1". |

### *gpio*

Parâmetros de configuração referentes aos GPIOs dos equipamentos. Através desse módulo, é possível realizar a configuração dos relés das catracas da Control iD.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **catra_relay_1_enabled** | *int* | Indica se o relé 1 da catraca está habilitado. Valores: "0" para desabilitado e "1" para habilitado. |
| **catra_relay_1_enable_direction** | *string* | Indica a direção da liberação de rotação do braço da catraca que causará a ativação do relé 1, caso o parâmetro `catra_relay_1_enabled` esteja habilitado. Valores: "left" para rotação em sentido anti-horário e "right" para rotação em sentido horário. |
| **catra_relay_2_enabled** | *int* | Indica se o relé 2 da catraca está habilitado. Valores: "0" para desabilitado e "1" para habilitado. |
| **catra_relay_2_enable_direction** | *string* | Indica a direção da liberação de rotação do braço da catraca que causará a ativação do relé 2, caso o parâmetro `catra_relay_2_enabled` esteja habilitado. Valores: "left" para rotação em sentido anti-horário e "right" para rotação em sentido horário. |

### *onvif*

Parâmetros de configuração referentes ao [*Streaming iDFace*](../particularidade-dos-produtos/streaming-idface.md). Através desse módulo, é possível realizar a configuração dos modos de operação, resolução desejada e orientação da imagem que será mostrada.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **rtsp_enabled** | *int* | Indica se streaming RTSP está habilitado. Valores: "0" para desabilitado e "1" para habilitado. |
| **rtsp_rgb** | *int* | Altera a câmera que está sendo utilizada para o streaming. Valores: "0" para IR e "1" para RGB. |
| **rtsp_username** | *string* | Altera a credencial de usuário para que a transmissão só seja habilitada a partir do preenchimento do usuário e senha corretos |
| **rtsp_password** | *string* | Altera a credencial de senha para que a transmissão só seja habilitada a partir do preenchimento do usuário e senha corretos |
| **rtsp_video_width** | *int* | Largura, em pixels, do stream de vídeo RTSP. |
| **rtsp_video_height** | *int* | Altura, em pixels, do stream de vídeo RTSP. |
| **rtsp_port** | *int* | Altera a porta utilizada para a transmissão. |
| **rtsp_codec** | *string* | Altera o codec de vídeo utilizado na transmissão. Valores: "mjpeg" para codec MJPEG e "h264" para codec H.264 |
| **rtsp_flipped** | *int* | Habilita o espelhamento do vídeo. Valores: "0" para desabilitado e "1" para habilitado. |
| **onvif_enabled** | *int* | Habilita a transmissão seguindo padrão ONVIF. Valores: "0" para desabilitado e "1" para habilitado. |
| **onvif_port** | *int* | Altera a porta utilizada para a transmissão seguindo o padrão ONVIF. |
| **onvif_username** | *string* | Altera a credencial de usuário requerida para acesso à transmissão ONVIF; a conexão só será estabelecida se o usuário fornecido for válido. |
| **onvif_password** | *string* | Define a senha requerida para acesso à transmissão ONVIF; a conexão só será estabelecida se a senha fornecida corresponder ao usuário. |

### *video_stream*

Parâmetros de configuração da marca d'água da transmissão de vídeo do [*Streaming iDFace*](../particularidade-dos-produtos/streaming-idface.md). Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **rtsp_watermark** | *int* | Habilita marca d'água na transmissão de vídeo. Valores: "0" para desabilitado e "1" para habilitado. |
| **rtsp_watermark_logo_enabled** | *int* | Habilita logo na transmissão de vídeo. Valores: "0" para desabilitado e "1" para habilitado. |
| **rtsp_watermark_custom_logo_enabled** | *int* | Habilita utilização de logo customizado na transmissão de vídeo. Valores: "0" para desabilitado e "1" para habilitado. |

### *ntp*

Parâmetros de configuração do módulo NTP. Através desse módulo, é possível realizar a configuração da time-zone e do horário de verão (para os países adotantes da medida).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **enabled** | *int* | Indica se o protocolo NTP está habilitado. Valores: "0" para desabilitado e "1" para habilitado. |
| **timezone** | *string* | Define o horário local em relação ao Tempo Universal Coordenado (UTC). Faixa de valores: "UTC-12" a "UTC+12". |

### *enroller*

Configurações referentes ao cadastro de imagens e templates faciais.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **return_face_template** | *int* | Define qual o tipo de evento do monitor será enviado no cadastro remoto assíncrono de faces, sendo `face_template` caso valor configurado seja **1** e `user_image` caso seja **0**. |

### *video_player*

Configuração dos parâmetros do player de vídeo.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **custom_video_enabled** | *int* | Habilita o uso de um vídeo customizado na tela inicial (Modo Propaganda). Valores: "0" para desabilitado e "1" para habilitado. |
| **hide_control_bar** | *int* | Indica se deve ocultar a barra de controle no Modo Propaganda. Valores: "0" para desabilitado e "1" para habilitado. |
| **hide_clock** | *int* | Indica se deve ocultar o relógio no Modo Propaganda. Valores: "0" para desabilitado e "1" para habilitado. |
| **hide_menu_button** | *int* | Indica se deve ocultar o botão de menu no Modo Propaganda. Valores: "0" para desabilitado e "1" para habilitado. |
| **hide_logo** | *int* | Indica se deve ocultar a logo no Modo Propaganda. Valores: "0" para desabilitado e "1" para habilitado. |
| **hide_device_name** | *int* | Indica se deve ocultar o nome do dispositivo no Modo Propaganda. Valores: "0" para desabilitado e "1" para habilitado. |
| **hide_call_buttons** | *int* | Indica se deve ocultar o botão de chamada no Modo Propaganda. Valores: "0" para desabilitado e "1" para habilitado. |
| **slide_show_enabled** | *int* | Habilita ou desabilita o slideshow. Valores: "0" para desabilitado e "1" para habilitado. |
| **slide_show_images_name** | *string* | Nome do arquivo ZIP contendo as imagens. |
| **slide_show_interval** | *int* | Duração da exibição de cada imagem (em milissegundos) no slideshow. |
