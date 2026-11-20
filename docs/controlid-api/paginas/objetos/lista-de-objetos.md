<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/objetos/lista-de-objetos/ -->

# Lista de Objetos

Veja abaixo a descrição de todos os objetos da linha de acesso. A lista identifica os diferentes tipos de recursos que você pode utilizar por meio da API, muitos dos quais podem ser inseridos, atualizados, buscados e excluídos.

### *users*

Representa um usuário.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único de um usuário (**obrigatório**). |
| **registration** | *string* | Texto representando a matrícula de um usuário (**obrigatório**). |
| **name** | *string* | Texto contendo o nome de um usuário (**obrigatório**). |
| **password** | *string* | String que representa a senha do usuário após o processo de Hash (para definir esse parâmetro não se deve usar o texto simples digitado pelo usuário; deve-se gerar seu **Hash** através do comando [user_hash_password](../sistema/criar-hash-de-senha-de-usuario.md). Da mesma forma, a aquisição desse parâmetro através do comando [load_objects](../objetos/carregar-objetos.md) retorna a senha após o Hash). |
| **salt** | *string* | String representando o Salt usado para calcular o Hash da senha do usuário. |
| **user_type_id** | *int* | Inteiro que representa o tipo de usuário cadastrado. Para um usuário do tipo **Visitante**, este campo é definido como 1; para o tipo **Usuário**, o campo não possui valor definido (nulo). |
| **begin_time** | *int* | Inteiro representando a partir de que data e hora (Unix timestamp) o usuário é válido. Se este campo for definido com valor igual a 0 a verificação não é feita. Já para um valor maior que 0, qualquer que seja o tipo de usuário, após a data informada será um usuário válido. |
| **end_time** | *int* | Inteiro representando até que data e hora (Unix timestamp) o usuário é válido. Se este campo for definido com valor igual a 0 a verificação não é feita. Já para um valor maior que 0, qualquer que seja o tipo de usuário, após a sua expiração ocorrerá a exclusão das credenciais como explicado em [Suporte a visitantes](../visitantes/introducao-visitas.md). |
| **image_timestamp** | *int* | Inteiro representando a data e hora (Unix timestamp) em que a imagem do usuário foi cadastrada. Se o valor do campo for 0, o usuário não possui imagens cadastradas. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |
| **last_access** | *int* | Inteiro representando a data e hora (Unix timestamp) do último acesso feito pelo usuário. Se o valor deste campo for 0 o usuário ainda não fez seu primeiro acesso. |
| **expires** | *int* | Campo reservado (legado), não utilizado pelo equipamento. Para definir a validade do usuário, utilize **begin_time** e **end_time**. |
| **panic_password** | *string* | Hash da senha de pânico do usuário. Campo gerenciado pelo equipamento; não deve ser escrito diretamente por integradores. |
| **panic_salt** | *string* | Salt usado para calcular o hash da senha de pânico (**panic_password**). Campo gerenciado pelo equipamento; não deve ser escrito diretamente por integradores. |

### *change_logs*

Registro das operações (inserção, atualização e remoção) realizadas nos objetos: users, templates, face_templates e cards.  
Esse registro é importante pois a partir dele podemos mapear alterações feitas no equipamento.
Ou seja, através dele pode-se obter:

- Registro de inserções, atualizações e remoções de usuários.
- Registro de inserções, atualizações e remoções de templates de identificação de usuário.
- Registro de inserções, atualizações e remoções de templates faciais de usuário.
- Registro de inserções, atualizações e remoções de cartões de usuário.

Observações:  
1. Este registro possui limite de armazenamento de 10 mil operações. Quando ele é atingido, ocorre a rotação e somente os últimos 10 mil registros estarão disponíveis.  
2. No momento essa funcionalidade só está disponível para o iDFace.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único do registro (**obrigatório**). |
| **operation_type** | *string* | Texto representando a operação que foi realizada (**obrigatório**). |
| **table_name** | *string* | Texto contendo o nome do objeto alterado (**obrigatório**). |
| **table_id** | *int* | Inteiro representando o identificador (id) do registro do objeto que foi modificado (**obrigatório**). |
| **timestamp** | *int* | Inteiro representando o horário em que foi feita a operação, em formato UNIX timestamp (**obrigatório**). |

### *templates*

Dados biométricos das impressões digitais dos usuários (referidas a seguir como biometrias).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único de uma biometria (**obrigatório**). |
| **finger_position** | *int* | Campo reservado. |
| **finger_type** | *int* | Tipo de biometria: *dedo comum* (valor 0) ou *dedo de pânico* (valor 1) (**obrigatório**). |
| **template** | *string Base64* | String em Base64 representando um template biométrico. |
| **user_id** | *int 64* | Identificador único do usuário a quem essa biometria pertence (**obrigatório**). |

### *cards* NEW!

Representa os cartões de identificação por proximidade.

**Observações do campo *value*:**
Este campo indica a numeração do cartão para cartões de proximidade (ASK, FSK, PSK), também chamados de Wiegand. O Wiegand é um padrão de comunicação utilizado para a leitura de cartões de proximidade.

1. **Como fazer a conversão do valor do cartão para ser enviado na API:**
   O valor a ser enviado é: [parte antes da vírgula] \* 2^32 + [parte depois da vírgula].
   Exemplo: Para o cartão 123,45678 deve-se enviar o valor 123 \* 2^32 + 45678, ou seja, 528281023086.
2. **Como fazer a conversão do valor da API para o valor do cartão:**
   O código de área, parte antes da vírgula, será a parte inteira, ou seja, a parte antes da vírgula do valor da divisão entre o valor da API e 2^32. O número do cartão, parte após a vírgula, será o resultado de: [valor da API] - [código de área] \* 2^32.
   Exemplo: Para o valor da API 528281023086:
   [código de área]: 528281023086 / 2^32 = 123,0000106. Então, [código de área] = 123.
   [número do cartão]: 528281023086 - 123 \* 2^32 = 45678.
   Portanto, o valor do cartão é 123,45678.

**Equipamentos com o módulo OMNIKEY Reader Core (iDFace Max HID) usam outra
representação.** A divisão por `2^32` acima se aplica aos leitores Mifare/ASK. No módulo
OMNIKEY as duas partes são combinadas usando a largura em bits do formato Wiegand configurado
no equipamento, e não um deslocamento fixo de 32 bits:

```
value = código_de_área * 2^N + número_do_cartão
```

**N é o número de bits do número do cartão do formato Wiegand configurado.** As larguras em
bits do código de área e do número do cartão por formato são:

| Formato | Bits do código de área | Bits do número do cartão (N) |
| --- | --- | --- |
| 26 | 8 | 16 |
| 34 | 16 | 16 |
| 35 | 12 | 20 |
| 37 (H10304) | 16 | 19 |
| 48 | 22 | 23 |
| 56 | 20 | 34 |
| 64 | 8 | 48 |

Os formatos 32, 37 (H10302), 40, 42 e 66 não possuem campo de código de área. Nesses casos,
todo o valor da API é o número do cartão e o código de área é sempre 0.

Exemplo, formato 26 (N = 16), cartão `123,4567`: enviar `123 * 2^16 + 4567 = 8065495`. Na
leitura inversa, o código de área é a parte inteira de `8065495 / 65536 = 123,07...`, ou seja,
`123`, e o número do cartão é `8065495 - 123 * 65536 = 4567`.

Consulte o objeto [***wiegand_modes***](../objetos/lista-de-objetos.md#wiegand_modes) para a definição completa de cada
formato, incluindo seu layout de bits e configuração de paridade.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único de um cartão de identificação (**obrigatório**). |
| **value** | *unsigned int 64* | Este campo indica a numeração do cartão. Leia atentamente como realizar a conversão para o valor a ser enviado na API (**obrigatório e único, não podem existir dois cartões com mesmo *value* no banco de dados**). |
| **user_id** | *int 64* | Identificador único do usuário ao qual pertence o cartão de identificação (**obrigatório**). |
| **wiegand_format_id** | *int* | Formato Wiegand do cartão (referencia o **id** do objeto [***wiegand_modes***](../objetos/lista-de-objetos.md#wiegand_modes)). Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** (a partir da versão V7.12.0). |
| **credential_type** | *int* | Tipo de credencial representada pelo cartão. Valores: "0" (cartão), "1" (credencial facial, face vinculada a um cartão) ou "2" (QR Code). Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** (a partir da versão V7.12.0). |
| **secret** | *string* | Campo reservado. Gerenciado pelo equipamento; não deve ser escrito por integradores. |

### *wiegand_modes* NEW!

Tabela de consulta (somente leitura) com os formatos Wiegand suportados pelo equipamento, usados para interpretar/gerar o valor de um cartão. O campo **wiegand_format_id** do objeto [***cards***](../objetos/lista-de-objetos.md#cards) referencia o **id** desta tabela.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador único do formato Wiegand (**obrigatório**). Referenciado por **cards.wiegand_format_id**. |
| **name** | *string* | Nome do formato (ex.: "26", "34", "37(H10302)", "37(H10304)", "custom"). |
| **size** | *int* | Número total de bits do formato. |
| **format** | *string* | Layout dos campos de dados do formato. Indica, por meio de faixas de bits no formato `[início:fim]` (1-indexado), quais bits correspondem a cada campo: `F` (facility code/código de área), `C` (número do cartão) e `I` (issue code/código de emissão, quando presente). Exemplo: `"F[2:9], C[10:25]"` indica que os bits 2 a 9 são o código de área e os bits 10 a 25 são o número do cartão. |
| **parities** | *string* | Layout dos bits de paridade do formato. Cada trecho tem a forma `<tipo>(<bit>)[<faixa>]`, onde `<tipo>` é `E` (par) ou `O` (ímpar), `<bit>` é a posição do bit de paridade e `<faixa>` é o intervalo de bits sobre o qual essa paridade é calculada. Vazio quando o formato não usa bits de paridade. |
| **parities_calculation_order** | *string* | Ordem em que os bits de paridade listados em **parities** devem ser calculados/verificados, referenciados pela posição do bit de paridade (ex.: `"1, 26"`). |
| **inverted_facility** | *int* | Indica se os bits do código de área (`F`) devem ser lidos em ordem invertida. Valores: "0" ou "1". |
| **inverted_card** | *int* | Indica se os bits do número do cartão (`C`) devem ser lidos em ordem invertida. Valores: "0" ou "1". |

### *qrcodes*

Representa os QR Codes utilizados para identificação. O QR Code possui dois [modos de operação](../particularidade-dos-produtos/qr-code.md#modo-de-operacao) configuráveis. O QR Code só será armazenado como um objeto ***qrcodes*** caso o **Modo Alfanumérico** esteja ativo, caso contrário, o mesmo será tratado como um cartão, sendo armazenado como um objeto [***cards***](../objetos/lista-de-objetos.md#cards).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único de um QR Code de identificação (**obrigatório**). |
| **value** | *string* | Este campo indica o conteúdo representado no QR Code. Este também é o valor a ser recebido da API (**obrigatório e único, não podem existir dois QR Codes com mesmo *value* no banco de dados**). |
| **user_id** | *int 64* | Identificador único do usuário ao qual pertence o QR Code de identificação (**obrigatório**). |

### *uhf_tags*

Representa as tags UHF utilizadas para identificação. As tags UHF possuem dois **modos de operação** configuráveis: **extended** e **standard**. As tags UHF só serão armazenadas como um objeto ***uhf_tags*** caso o modo **extended** esteja ativo, nesse caso as tags podem ser registradas com até 96 bits em representação nvarchar e armazenadas em hexadecimal, uma tag "0xCAFEDAD0" será armazenada como "CAFEDAD0". No caso contrário, com o modo **standard** ativado, o mesmo será tratado como um cartão, sendo armazenado como um objeto [***cards***](../objetos/lista-de-objetos.md#cards).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único de uma tag UHF de identificação (**obrigatório**). |
| **value** | *string* | Este campo indica o valor lido pela tag UHF. Este também é o valor a ser recebido da API (**obrigatório e único, não podem existir duas tags UHF com mesmo *value* no banco de dados**). |
| **user_id** | *int 64* | Identificador único do usuário ao qual pertence a tag UHF de identificação (**obrigatório**). |

### *pins*

Representa os PINs utilizados para identificação.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único de um PIN de identificação (**obrigatório**). |
| **value** | *string* | Este campo indica o valor do PIN. Este também é o valor a ser recebido da API (**obrigatório e único, não podem existir dois PINs com mesmo *value* no banco de dados**). |
| **user_id** | *int 64* | Identificador único do usuário ao qual pertence o PIN de identificação (**obrigatório e único, não podem existir dois PINs com mesmo *user_id* no banco de dados**). |
| **pin_type** | *int* | Tipo do PIN: "0" (PIN comum) ou "1" (PIN de pânico). |

### *alarm_zones*

Dados referentes às zonas de alarmes.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **zone** | *int* | Identificador único de uma zona de alarme (**obrigatório**). |
| **enabled** | *int* | Indica se a entrada de alarme referente à *zona* está habilitada (valor 1) ou não (valor 0) (**obrigatório**). |
| **active_level** | *int* | Indica se a entrada de alarme referente à zona está configurada como 'ativo alto' (1) ou 'ativo baixo' (0) (**obrigatório**). |
| **alarm_delay** | *int* | Tempo de atraso no disparo do alarme uma vez que um sinal de alarme tenha sido detectado nesta zona (**obrigatório**). |

### *user_roles*

Relaciona usuários a níveis de privilégio. Contém apenas usuários que tenham algum nível de privilégio diferente do padrão.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **user_id** | *int 64* | Identificador único do usuário (**obrigatório**). |
| **role** | *int* | Se este campo estiver definido como 1, o usuário é um **administrador** (**obrigatório**). |

### *groups* NEW!

Representa os grupos de acesso. Nas interfaces nativas do equipamento e na interface web, esse tipo de objeto é referido como **departamento**.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único do grupo de acesso (**obrigatório**). |
| **name** | *string* | Nome do grupo de acesso (**obrigatório**). |
| **identification_mode** | *int ou null* | Modo de identificação usado pelos usuários do grupo. Valores: "0" (1:N), "1" (1:1), "2" (Template no Cartão / ToC) ou `null` (usa a configuração global do equipamento). Disponível apenas no **iDFace Max HID**. |

### *group_compatibility* NEW!

Define, por grupo, quais outros grupos são aceitos como "segunda pessoa" na regra de autenticação de 2 pessoas (ver **identifier.two_person_auth_enabled** em [Parâmetros de Configuração](../configuracoes/parametros-configuracao.md#identifier)).

Disponível apenas no **iDFace Max HID**.

**Atenção — regra unidirecional:** um registro `(group_id=A, compatible_group_id=B)` só habilita "grupo A apresenta credencial primeiro, grupo B segundo" — não o inverso. Para que o caminho reverso também funcione, é preciso criar explicitamente o registro `(group_id=B, compatible_group_id=A)`.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **group_id** | *int 64* | Identificador do grupo que apresenta credencial **primeiro** (**obrigatório**). |
| **compatible_group_id** | *int 64* | Identificador do grupo aceito como **segunda pessoa** (**obrigatório**). |

### *user_groups*

Relaciona os usuários aos grupos de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **user_id** | *int 64* | Identificador do usuário (**obrigatório**). |
| **group_id** | *int 64* | Identificador do grupo de acesso (**obrigatório**). |

### *scheduled_unlocks*

Representa as liberações agendadas de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único da liberação agendada (**obrigatório**). |
| **name** | *string* | Nome da liberação agendada (**obrigatório**). |
| **message** | *string* | Mensagem a ser exibida durante a liberação. |
| **timestamp** | *int* | Data e hora (Unix timestamp) de controle interno da liberação agendada. Campo gerenciado pelo equipamento. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |

### *actions*

Objeto que representa os scripts de ação.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único do script de ação no banco de dados (**obrigatório**). |
| **name** | *string* | Nome descritivo da ação (**obrigatório**). |
| **action** | *string* | Nome do arquivo do script de ação (**obrigatório**). |
| **parameters** | *string* | Parâmetros do script de ação (**obrigatório**). |
| **run_at** | *int* | Pode assumir 3 valores. Caso seja 0, o script é executado no equipamento que o usuário utilizou para a identificação. Caso seja 1, o script é executado em todos os equipamentos conectados. Caso seja 2, o script é executado no servidor de identificação (**obrigatório**). |

### *areas*

Representa as áreas cujo acesso se deseja controlar.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único da área (**obrigatório**). |
| **name** | *string* | Nome descritivo da área (**obrigatório**). |

### *portals*

Representa os portais. Um portal liga duas áreas e tem uma única direção.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador único do portal (**obrigatório**). |
| **name** | *string* | Nome descritivo do portal (**obrigatório**). |
| **area_from_id** | *int 64* | Identificador da área de origem (**obrigatório**). |
| **area_to_id** | *int 64* | Identificador da área de destino (**obrigatório**). |

### *portal_actions*

Relaciona portais e ações.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **portal_id** | *int 64* | Identificador do portal (**obrigatório**). |
| **action_id** | *int 64* | Identificador da ação (**obrigatório**). |

### *access_rules*

Representa as regras de acesso. A avaliação das regras de acesso acontece na seguinte ordem:

Dada uma tentativa de acesso, todas as regras de bloqueio são avaliadas antes das regras de liberação. Caso uma ou mais regras de bloqueio tenham seus critérios atendidos, suas ações serão executadas. Apenas caso nenhuma das regras de bloqueio tenha seus critérios atendidos, as regras de liberação são avaliadas e suas ações são executadas caso seus critérios sejam atendidos.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador da regra de acesso (**obrigatório**). |
| **name** | *string* | Nome descritivo da regra de acesso (**obrigatório**). |
| **type** | *int* | Tipo da regra de acesso: caso valha 0, é uma regra de bloqueio, e caso valha 1, é uma regra de permissão (**obrigatório**). |
| **priority** | *int* | Campo reservado (**obrigatório**). |

### *portal_access_rules*

Relaciona portais e regras de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **portal_id** | *int 64* | Identificador do portal (**obrigatório**). |
| **access_rule_id** | *int 64* | Identificador da regra de acesso (**obrigatório**). |

### *group_access_rules*

Relaciona grupos e regras de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **group_id** | *int 64* | Identificador do grupo (**obrigatório**). |
| **access_rule_id** | *int 64* | Identificador da regra de acesso (**obrigatório**). |

### *scheduled_unlock_access_rules*

Relaciona liberações agendadas e regras de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **scheduled_unlock_id** | *int 64* | Identificador da liberação agendada (**obrigatório**). |
| **access_rule_id** | *int 64* | Identificador da regra de acesso (**obrigatório**). |

### *time_zones*

Conjunto de intervalos que representa o critério de horário de uma regra de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do horário (**obrigatório**). |
| **name** | *string* | Nome descritivo do horário (**obrigatório**). |

### *time_spans*

Um dos intervalos de um horário, o qual representa o critério de horário de uma regra de acesso.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do intervalo (**obrigatório**). |
| **time_zone_id** | *int 64* | Horário ao qual esse intervalo pertence (**obrigatório**). |
| **start** | *int* | Horário de início do intervalo. É armazenado em segundos desde a 0 hora do dia. Exemplo: Uma hora da manhã será 3600, já que 1 × 60 × 60 = 3600. Duas horas da manhã será 7200, já que 2 × 60 × 60 = 7200 (**obrigatório**). |
| **end** | *int* | Horário de término do intervalo. É armazenado em segundos desde a 0 hora do dia (**obrigatório**). |
| **sun** | *int* | Indica se o intervalo está ativo para os domingos (**obrigatório**). |
| **mon** | *int* | Indica se o intervalo está ativo para as segundas-feiras (**obrigatório**). |
| **tue** | *int* | Indica se o intervalo está ativo para as terças-feiras (**obrigatório**). |
| **wed** | *int* | Indica se o intervalo está ativo para as quartas-feiras (**obrigatório**). |
| **thu** | *int* | Indica se o intervalo está ativo para as quintas-feiras (**obrigatório**). |
| **fri** | *int* | Indica se o intervalo está ativo para as sextas-feiras (**obrigatório**). |
| **sat** | *int* | Indica se o intervalo está ativo para os sábados (**obrigatório**). |
| **hol1** | *int* | Indica se o intervalo está ativo para os feriados do tipo 1 (**obrigatório**). |
| **hol2** | *int* | Indica se o intervalo está ativo para os feriados do tipo 2 (**obrigatório**). |
| **hol3** | *int* | Indica se o intervalo está ativo para os feriados do tipo 3 (**obrigatório**). |

### *contingency_cards*

Cadastra uma lista de cartões que estará disponível em modo contingência para acesso aos equipamentos.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador único do cartão, autoincremental (**obrigatório**). |
| **value** | *int 64* | Número do cartão liberado no modo de contingência (**obrigatório**). |

### *contingency_card_access_rules*

Vincula a regra de acesso que será válida para os cartões que estiverem cadastrados em contingency_cards.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **access_rule_id** | *int 64* | Corresponde ao id da regra de acesso que será utilizada em modo de contingência, por padrão é: 1 (regra de acesso "sempre liberado") (**obrigatório**). |

### *holidays*

Essa tabela contém os feriados, assim como indica a qual tipo eles pertencem.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador do feriado (**obrigatório**). |
| **name** | *string* | Nome descritivo do feriado (**obrigatório**). |
| **start** | *int* | A data e hora em que o feriado começa em formato UNIX timestamp (**obrigatório**). |
| **end** | *int* | A data e hora em que o feriado termina em formato UNIX timestamp (**obrigatório**). |
| **hol1** | *int* | Se o feriado pertence ao grupo 1. O valor é 0 ou 1 (**obrigatório**). |
| **hol2** | *int* | Se o feriado pertence ao grupo 2. O valor é 0 ou 1 (**obrigatório**). |
| **hol3** | *int* | Se o feriado pertence ao grupo 3. O valor é 0 ou 1 (**obrigatório**). |
| **repeats** | *int* | Se o feriado deve se repetir anualmente. O valor é 0 ou 1 (**obrigatório**). |

### *alarm_zone_time_zones*

Relaciona zonas de alarme e horários.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **alarm_zone_id** | *int 64* | Identificador da zona de alarme (**obrigatório**). |
| **time_zone_id** | *int 64* | Identificador do horário (**obrigatório**). |

### *access_rule_time_zones*

Relaciona regras de acesso e horários.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **access_rule_id** | *int 64* | Identificador da regra de acesso (**obrigatório**). |
| **time_zone_id** | *int 64* | Identificador do horário (**obrigatório**). |

### *access_logs*

Contém os logs de acesso do equipamento.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do log de acesso (**obrigatório**). |
| **time** | *int* | Horário da ocorrência em Unix [Timestamp](http://pt.wikipedia.org/wiki/Era_Unix). |
| **event** | *int* | Tipo do evento, pode ser:  1. Equipamento inválido 2. Parâmetros de identificação inválidos 3. Não identificado 4. Identificação pendente 5. Tempo de identificação esgotado 6. Acesso negado 7. Acesso concedido 8. Acesso pendente (usado quando o acesso depende de mais de uma pessoa) 9. Usuário não é administrador (usado quando um usuário tenta acessar o menu, mas não é administrador) 10. Acesso não identificado (quando o portal é aberto através da API e o motivo não é informado) 11. Acesso por botoeira 12. Acesso pela interface web 13. Desistência de entrada (*exclusivo para iDBlock*) 14. Sem resposta (nenhuma ação é tomada) 15. Acesso pela interfonia (*exclusivo para iDFace*)   Valores exclusivos dos **equipamentos da linha iDAccess**:  - 16: Acesso via OSDP (a partir da versão V5.9.4) - 17: Acesso negado por intertravamento (a partir da versão V5.16.0) - 18: Liberação pelo lado oposto (a partir da versão V5.20.0) |
| **device_id** | *int 64* | Identificador do equipamento onde o evento de acesso ocorreu. |
| **identifier_id** | *int* | Identificador do módulo de identificação que registrou o evento no equipamento. |
| **user_id** | *int* | Identificador do usuário envolvido na ocorrência. |
| **portal_id** | *int* | Identificador do portal envolvido na ocorrência. |
| **identification_rule_id** | *int* | Identificador da regra de identificação envolvida na ocorrência. |
| **qrcode_value** | *string* | Valor alfanumérico do QR Code utilizado durante a identificação. |
| **uhf_tag** | *string* | Valor lido pela tag UHF. Disponível apenas em **equipamentos da linha iDAccess**. |
| **pin_value** | *string* | Valor do PIN utilizado durante a identificação. |
| **card_value** | *int 64* | Número do cartão utilizado durante a identificação. |
| **confidence** | *int 64* | Indica o grau de confiança do rosto reconhecido. O valor varia de 0 (mínimo) a 1800 (máximo). Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |
| **mask** | *int 64* | Indica se o usuário está usando máscara ou não. O valor 1 indica a presença de máscara e o valor 0 indica que o usuário não está usando máscara. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |
| **log_type_id** | *int 64* | Identificador único do tipo de log, aplicável somente para o iDFlex Attendance (solução para marcação de ponto disponível apenas para exportação). |
| **equipment_id** | *int 64* | Campo reservado (legado). Para identificar o equipamento, utilize **device_id**. |
| **component_id** | *int* | Identificador do componente (por exemplo, leitor ou módulo externo) que originou o evento. |

### *access_log_access_rules*

Regras de acesso de um log de acesso. O par access_log_id e access_rule_id é **único**, ou seja, não pode haver nesta tabela mais de um elemento com o mesmo par dessas propriedades.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **access_log_id** | *int 64* | Identificador do log de acesso (**obrigatório**). |
| **access_rule_id** | *int 64* | Identificador da regra de acesso (**obrigatório**). |

### *alarm_logs*

Contém os logs de alarmes do equipamento.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do log de alarme (**obrigatório**). |
| **event** | *int* | Tipo do evento, pode ser:  1. Alarme ativado 2. Alarme desativado |
| **cause** | *int* | Causa do evento, pode ser:  1. Zona de alarme 1 2. Zona de alarme 2 3. Zona de alarme 3 4. Zona de alarme 4 5. Zona de alarme 5 6. Porta aberta 7. Arrombamento de porta 8. Dedo de pânico 9. Violação do equipamento 10. Cartão de pânico  Os valores a partir de 11 dependem da linha do equipamento:  - Equipamentos da linha iDFace ([V6](../particularidade-dos-produtos/versoes-de-firmware.md)), iDFace Max ([V7](../particularidade-dos-produtos/versoes-de-firmware.md)) e iDFace e iDFace Max ([V8](../particularidade-dos-produtos/versoes-de-firmware.md)): 11 = Senha de pânico; 12 = PIN de pânico. - Equipamentos da linha iDAccess: 11 = Zona monitorada sem presença; 12 = Zona monitorada aberta; 13 = Senha de pânico; 14 = PIN de pânico; 15 = Violação de entrada supervisionada (a partir da versão V5.15.0).  Veja [Introdução ao Alarme](../alarme/introducao-ao-alarme.md). |
| **user_id** | *int 64* | Identificador do usuário envolvido na ocorrência. |
| **time** | *int* | Horário da ocorrência em Unix [Timestamp](http://pt.wikipedia.org/wiki/Era_Unix). |
| **access_log_id** | *int* | Identificador do registro de log de acesso envolvido na ocorrência. |
| **door_id** | *int* | Identificador da porta envolvida na ocorrência. Disponível apenas em **equipamentos da linha iDAccess**. |

### *devices*

Representa os equipamentos cadastrados e reconhecidos pelo dispositivo, incluindo o próprio. É utilizado na comunicação via rede entre equipamentos.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do equipamento (**obrigatório**). |
| **name** | *string* | Nome descritivo do equipamento (**obrigatório**). |
| **ip** | *string* | Endereço do equipamento. Exemplo: 192.168.0.129 ou exemplo.controlid.com.br (**obrigatório**). |
| **public_key** | *string Base64* | Chave pública do equipamento, usada na comunicação segura entre equipamentos. Campo gerenciado pelo equipamento; não deve ser escrito por integradores. |

### *user_access_rules*

Vincula um usuário a uma regra de acesso.

**O par user_id e access_rule_id é único, ou seja, não pode haver mais de um objeto nesta tabela com mesmos valores nesses campos ao mesmo tempo.**

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **user_id** | *int* | Identificador único do usuário (**obrigatório**). |
| **access_rule_id** | *int* | Identificador único da regra de acesso (**obrigatório**). |

### *area_access_rules*

Vincula uma área a uma regra de acesso.

**O par area_id e access_rule_id é único, ou seja, não pode haver mais de um objeto nesta tabela com mesmos valores nesses campos ao mesmo tempo.**

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **area_id** | *int* | Identificador único da área (**obrigatório**). |
| **access_rule_id** | *int* | Identificador único da regra de acesso (**obrigatório**). |

### *catra_infos*

Permite consultar informações da catraca (aplicável apenas para catracas iDBlock).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador da catraca. |
| **left_turns** | *int 64* | Corresponde ao número de revoluções à esquerda. |
| **right_turns** | *int 64* | Corresponde ao número de revoluções à direita. |
| **entrance_turns** | *int 64* | Corresponde ao número de revoluções de entrada. |
| **exit_turns** | *int 64* | Corresponde ao número de revoluções de saída. |

### *log_types*

Permite consultar os tipos de log.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador do tipo de log (**obrigatório**). |
| **name** | *string* | Nome do tipo de log (**obrigatório**). |

### *sec_boxs*

Configura o módulo de acionamento externo (MAE/Security Box) usado no iDFlex, iDAccess Pro e iDAccess Nano.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | **id** da SecBox. Esse valor sempre será 65793. |
| **version** | *int* | Corresponde à versão da SecBox. |
| **name** | *string* | Corresponde ao nome da SecBox. |
| **enabled** | *bool* | Indica se a SecBox está habilitada ou não. |
| **relay_timeout** | *int* | Esse campo representa o tempo de abertura do relé da SecBox, em milissegundos (ms). |
| **door_sensor_enabled** | *bool* | Esse campo representa se o sensor de porta está habilitado ou não. |
| **door_sensor_idle** | *bool* | Esse campo representa se o sensor de porta está NO = 1 ou NC = 0. |
| **auto_close_enabled** | *int* | Inteiro (0 ou 1) indicando se o relé deverá fechar quando o sensor de porta abrir. |
| **give_up_enabled** | *int* | Inteiro (0 ou 1) indicando se a detecção de desistência de entrada está habilitada. Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**. |
| **rex_mode** | *int* | Modo de operação da botoeira (REX): "0" (sempre abre a porta) ou "1" (abre condicionalmente). |

### *contacts*

Permite manipular os contatos que podem ser utilizados em ligações de interfonia SIP.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador do contato. |
| **name** | *string* | Nome do contato. |
| **number** | *string* | Número (ramal) do contato. |
| **info** | *string* | Informações adicionais sobre o contato. |
| **user_id** | *int* | Identificador do usuário vinculado ao contato. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**. |

### *contact_groups*

Permite manipular os grupos de contatos que podem ser usados na chamada SIP de interfonia.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador do grupo de contatos. |
| **name** | *string* | Nome do grupo de contatos. |
| **description** | *string* | Descrição do grupo de contatos. |
| **parent_id** | *int* | Grupo de contatos pai. Pode ser null. |

### *contact_contact_groups*

Permite manipular os vínculos entre contatos e grupos de contatos.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **contact_id** | *int* | Identificador do contato. |
| **contact_group_id** | *int* | Identificador do grupo de contatos. |

### *kiosk_buttons*

Permite manipular os botões exibidos no Modo Quiosque.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador do botão. |
| **color** | *string* | Cor do botão. O valor deve ser uma cor hexadecimal com prefixo #. |
| **text** | *string* | Texto do botão. |
| **text_color** | *string* | Cor do texto do botão. O valor deve ser uma cor hexadecimal com prefixo #. |
| **font** | *string* | Fonte do texto do botão. Possíveis valores são: "Arial", "Bitstream Vera Sans", "Bitstream Vera Sans Mono", "Bitstream Vera Serif", "DejaVu Sans", "DejaVu Sans Mono", "Open Sans" e "Roboto". |
| **icon_name** | *string* | Nome do ícone do botão. O nome deve coincidir com um ícone carregado previamente. |
| **action** | *string* | Ação executada quando o botão é pressionado. Possíveis valores são: "directory", "doorPin", "qrCode" e "mainContactGroup". |
| **font_size** | *int* | Tamanho da fonte do texto do botão. |
| **icon_width** | *int* | Largura do ícone do botão. |
| **icon_height** | *int* | Altura do ícone do botão. |

### *timed_alarms*

Permite manipular horários fixos de sirene para determinados dias da semana.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do alarme (**obrigatório**). |
| **name** | *string* | Nome de identificação do alarme (**obrigatório**). |
| **time** | *int* | Horário de acionamento da sirene. É armazenado em segundos desde a 0 hora do dia. Exemplo: Uma hora da manhã será 3600, já que 1 × 60 × 60 = 3600. Duas horas da manhã será 7200, já que 2 × 60 × 60 = 7200 (**obrigatório**). |
| **sun** | *int* | Indica se o alarme está ativo para os domingos (**obrigatório**). |
| **mon** | *int* | Indica se o alarme está ativo para as segundas-feiras (**obrigatório**). |
| **tue** | *int* | Indica se o alarme está ativo para as terças-feiras (**obrigatório**). |
| **wed** | *int* | Indica se o alarme está ativo para as quartas-feiras (**obrigatório**). |
| **thu** | *int* | Indica se o alarme está ativo para as quintas-feiras (**obrigatório**). |
| **fri** | *int* | Indica se o alarme está ativo para as sextas-feiras (**obrigatório**). |
| **sat** | *int* | Indica se o alarme está ativo para os sábados (**obrigatório**). |

### *access_events*

Registra eventos de acesso como abertura de portas e ações da catraca.

Observação:

- Este registro possui limite de armazenamento de 10 mil eventos. Quando ele é atingido, ocorre a rotação e somente os últimos 10 mil registros estarão disponíveis.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int 64* | Identificador do evento de acesso (**obrigatório**). |
| **event** | *string* | Indica a categoria de evento, possíveis valores:  - catra - secbox - door  (**obrigatório**). |
| **type** | *string* | Indica o tipo de evento em sua categoria. Para **event** igual a *catra*, são válidos os valores:  - TURN_LEFT - TURN_RIGHT - GIVE_UP  Já para **event** com valor *door* ou *secbox*, são válidos:  - OPEN - CLOSE  (**obrigatório**). |
| **identification** | *string* | Caso **event** seja *secbox* ou *door*, indica o id em texto da SecBox ou porta correspondente, caso **event** seja *catra*, indica o UUID do evento (**obrigatório**). |
| **device_id** | *int 64* | Id associado ao equipamento que reportou o evento (**obrigatório**). |
| **timestamp** | *int* | Horário da ocorrência em Unix [Timestamp](http://pt.wikipedia.org/wiki/Era_Unix) (**obrigatório**). |
| **uuid** | *string* | Identificador único universal (UUID) do evento. Disponível nos **equipamentos da linha iDAccess** e nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**. |

### *custom_thresholds*

Permite manipular a rigidez da identificação facial no iDFace para poder diferenciar faces semelhantes.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador do threshold customizado. |
| **user_id** | *int* | Identificador único do usuário a quem o valor do threshold pertence (**obrigatório**). |
| **threshold** | *int* | Número do threshold customizado do usuário (**obrigatório**). |

### *network_interlocking_rules*

Representa as regras de intertravamento remoto. O funcionamento do intertravamento remoto é o seguinte:
- Nesse dispositivo (chamaremos de dispositivo A), cadastramos as informações do dispositivo remoto (chamaremos de dispositivo B).
- Após o cadastro das informações, o dispositivo A irá se conectar ao dispositivo B para verificar o estado de sua porta.
- Caso a porta do dispositivo B esteja aberta, o dispositivo A não irá permitir a abertura de sua porta.

O objetivo do intertravamento remoto é impedir a abertura simultânea das portas de dois ou mais dispositivos.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **id** | *int* | Identificador da regra de intertravamento. |
| **ip** | *string* | IP do dispositivo remoto (dispositivo B) (**obrigatório**). |
| **login** | *string* | Login do dispositivo remoto (dispositivo B). É o mesmo login utilizado para acessar a interface Web (**obrigatório**). |
| **password** | *string* | Senha do dispositivo remoto (dispositivo B). É a mesma senha utilizada para acessar a interface Web (**obrigatório**). |
| **portal_name** | *string* | Nome da regra de intertravamento (**obrigatório**). |
| **enabled** | *int* | Habilita ou desabilita esta regra de intertravamento. 1 = Habilitado, 0 = Desabilitado (**obrigatório**). |
| **port** | *string* | Porta do dispositivo remoto (dispositivo B). |
| **session_hash** | *string* | Sessão mantida pelo equipamento junto ao dispositivo remoto. Campo gerenciado pelo equipamento; não deve ser escrito por integradores. |
