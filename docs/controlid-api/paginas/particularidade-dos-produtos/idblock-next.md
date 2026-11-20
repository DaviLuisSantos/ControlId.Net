<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/idblock-next/ -->

# iDBlock Next

A catraca inteligente iDBlock Next (vide https://www.controlid.com.br/controle-de-acesso/idblock-next/) tem como componentes uma placa principal (Main) e dispositivos de identificação, a saber: até 2 iDFaces (conforme modelo) e Tampa com display (conforme modelo). Caso desejado, a conexão do conjunto como um todo à internet envolve um único ponto de rede Ethernet externo, pois as demais conexões são todas internas, em um switch integrado, de forma a possibilitar a comunicação entre os componentes.

Cada um dos componentes de identificação pode ter como papel o de primário ou o de secundário, conforme descrito na próxima seção. Uma vez configurados os papéis, toda a integração seguinte é feita com comunicação exclusiva com o dispositivo primário do conjunto. Por exemplo, é o endereço IP do primário que deve ser fornecido ao software iDSecure, o qual informará alterações de cadastro de usuários e de regras de acesso diretamente ao primário, de forma a serem válidas para todo o conjunto. Nas seções posteriores serão elencadas as demais particularidades de integração para a iDBlock Next.

## Modos de operação na iDBlock Next

Equipamentos que fazem parte de um conjunto iDBlock Next devem ser configurados para funcionarem em um de dois modos: *Dispositivo Primário* ou *Dispositivo Secundário*.

Um dispositivo configurado como primário será responsável por autorizar o acesso de usuários em todos os dispositivos secundários com os quais está conectado. Além disso, algumas funcionalidades, como cadastro de usuários e geração de relatórios de acesso, não estão disponíveis em dispositivos secundários e configurações específicas são modificadas automaticamente para se manterem consistentes em todo o conjunto.

É recomendado que a configuração do papel de cada dispositivo do conjunto seja feita assim que possível, pois é essencial para o funcionamento correto da catraca iDBlock Next. É importante também se assegurar que cada catraca possui um, e somente um, dispositivo primário, evitando conflitos durante a operação.

### Configuração dos modos de operação

Para configurar o modo de operação no conjunto da catraca pela API, é necessário modificar a configuração *catra_role*, localizada no módulo *sec_box*, de acordo com a tabela abaixo:

| Modo de Operação | *catra_role* |
| --- | --- |
| Dispositivo fora do conjunto | 0 |
| Dispositivo primário | 1 |
| Dispositivo secundário | 2 |

A alteração desse parâmetro deve ser feita com cuidado, pois, para garantir a integridade e bom funcionamento do conjunto iDBlock Next, a mudança no valor da configuração *catra_role* causa um reinício no equipamento e a limpeza de informações em seu banco de dados (configurações gerais e de rede são mantidas) automaticamente. Por esse motivo, é recomendado que exista um backup de informações importantes antes da execução desse procedimento, além de não se alterar o modo de operação frequentemente.

No caso do dispositivo primário, outras configurações de modo de operação podem ser modificadas, de acordo com a seção [*Modos de operação*](../modos-de-operacao/introducao-aos-modos-de-operacao.md) deste documento. Já para aparelhos configurados como secundários, tais configurações não devem ser modificadas.

Também é importante ressaltar que a Tampa com Display não deve funcionar como dispositivo primário caso um ou mais iDFace estejam conectados à iDBlock Next e, caso seja configurada dessa forma, a configuração será sobrescrita automaticamente.

### Configuração da catraca com iDFaces e Tampa com display

Vale ressaltar que uma iDBlock Next pode ser configurada de múltiplas maneiras, combinando sua tampa e dispositivos iDFace. A tampa do iDBlock Next, disponível no modelo iDBlock Next Tampa com Display, contém uma tela integrada.

- **Configuração 1: Apenas tampa, sem iDFace** - Neste modo a tampa se porta e deve ser configurada como primário do dispositivo, controlando-o em integralidade. O controle de acesso neste caso é feito por biometria, cartão, PIN ou senha.
- **Configuração 2: Apenas 1 ou 2 iDFaces, sem tampa** - Neste modo, um dos iDFaces será obrigatoriamente o primário. Com esta configuração, os iDFaces são responsáveis pelo controle de acesso da catraca no sentido de entrada e saida. Este modo de funcionamento não oferece o controle de acesso via biometria como com a tampa, mas oferece ainda o recurso via reconhecimento facial, cartão, PIN ou senha.
- **Configuração 3: Tampa com 1 ou 2 iDFaces** - Neste modo, um dos iDFaces será obrigatoriamente o primário, controlando o outro iDFace (se houver) e a tampa como secundários. Este modo oferece identificação tanto biométrica quanto facial, além de via cartão, PIN e senha.

É importante notar também que o secundário, em qualquer uma das configurações, jamais é ligado ao servidor online. Todas as configurações são unicamente feitas pelo dispositivo primário, que controla o conjunto como um dispositivo de controle de acesso ou através de comandos recebidos via API.

### Exemplo de requisição para iDFace ou tampa primários

Essa requisição configura um iDFace ou Tampa com Display como dispositivo primário:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "sec_box": {
            "catra_role": "1"
        }
    })
});
```

### Exemplo de requisição para iDFace ou tampa secundários

Essa requisição configura um iDFace ou Tampa com Display como dispositivo secundário:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "sec_box": {
            "catra_role": "2"
        }
    })
});
```

## Outras configurações importantes da catraca

Configurações específicas para controle da catraca. Todas as configurações também são do módulo *sec_box*.

### Parâmetros do módulo: *sec_box*

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **catra_default_fsm** | *string* | Modo de operação da catraca. Controla quais sentidos da catraca serão controlados ou liberados. Deve ser "0" (ambos os giros controlados), "1" (ambos os giros liberados), "2" (giro no sentido anti-horário liberado) ou "3" (giro no sentido horário liberado). |
| **catra_single_liberate_mode** | *string* | Determina a operação da catraca, definindo os sentidos de giro que devem ser liberados. É aplicado quando existe apenas o primário. Havendo um secundário, não tem efeito. Deve ser "0" (ambos os giros liberados) ou "1" (giro no sentido de entrada liberado; giro no sentido de saída bloqueado). |
| **catra_side_to_enter** | *string* | Indica qual sentido de giro da catraca será considerado como de entrada. Os valores são "0" (horário) ou "1" (anti-horário). |
| **catra_timeout** | *string* | Indica o tempo de duração da liberação do giro, em **milissegundos**. Ex.: "5000" para liberação de entrada / saída por 5 segundos. |
| **catra_bio_sync_period** | *string* | Intervalo de tempo em que haverá sincronismo periódico de *biometria* entre os dispositivos primário e secundário(s), em **milissegundos**. Valor padrão: "1000". |
| **catra_sync_period** | *string* | Intervalo de tempo em que haverá sincronismo periódico de *usuários* entre os dispositivos primário e secundário(s), em **milissegundos**. Valor padrão: "5000". |
| **catra_config_sync_period** | *string* | Intervalo de tempo em que haverá sincronismo periódico de *configurações* entre os dispositivos primário e secundário(s), em **segundos**. Valor padrão: "60". |
| **catra_relay_1_enabled** | *int* | Indica se o relé 1 da catraca está habilitado. Valores: "0" para desabilitado e "1" para habilitado. |
| **catra_relay_1_enable_direction** | *string* | Indica a direção da liberação de rotação do braço da catraca que causará a ativação do relé 1, caso o parâmetro `catra_relay_1_enabled` esteja habilitado. Valores: "left" para rotação em sentido anti-horário e "right" para rotação em sentido horário. |
| **catra_relay_2_enabled** | *int* | Indica se o relé 2 da catraca está habilitado. Valores: "0" para desabilitado e "1" para habilitado. |
| **catra_relay_2_enable_direction** | *string* | Indica a direção da liberação de rotação do braço da catraca que causará a ativação do relé 2, caso o parâmetro `catra_relay_2_enabled` esteja habilitado. Valores: "left" para rotação em sentido anti-horário e "right" para rotação em sentido horário. |
| **catra_collect_visitor_card** | *string* | Habilita a saída de visitantes com cartão feita exclusivamente através de depósito de cartão na urna coletora. Os valores são "0" para desabilitar a funcionalidade e "1" para habilitar. |
| **catra_show_counters** | *string* | Habilita o modo de contagem de giros na interface do dispositivo (Tampa com display). Os valores são "0" para desabilitar a funcionalidade e "1" para habilitar. |
| **catra_total_counter** | *string* | Habilita a visulização do contador de giros totais na interface do dispositivo (Tampa com display). Os valores são "0" para desabilitar a funcionalidade e "1" para habilitar. |
| **catra_left_counter** | *string* | Habilita a visualização do contador de giros para esquerda na interface do dispositivo (Tampa com display). Os valores são "0" para desabilitar a funcionalidade e "1" para habilitar. |
| **catra_right_counter** | *string* | Habilita a visulização do contador de giros para direita na interface do dispositivo (Tampa com display). Os valores são "0" para desabilitar a funcionalidade e "1" para habilitar. |

### Exemplo de requisição para dispositivo primário

Essa requisição configura um dispositivo primário com os parâmetros da catraca:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "sec_box": {
            "catra_default_fsm": "0",
            "catra_side_to_enter": "0",
            "catra_timeout": "1000",
            "catra_sync_period": "5000",
            "catra_config_sync_period": "60",
            "catra_relay_1_enabled": "1",
            "catra_relay_1_enable_direction": "right",
            "catra_relay_2_enabled": "1",
            "catra_relay_1_enable_direction": "left"
        }
    })
});
```

## Evento de desistência de acesso

Assim como nas catracas da linha iDBlock V2, nos modos **standalone** ou **contingência** (vide [*Modos de operação*](../modos-de-operacao/introducao-aos-modos-de-operacao.md)), caso um acesso autorizado (evento 7) não seja seguido de um evento de giro no respectivo tempo de liberação, este passará a ser uma desistência de acesso (evento 13) no respectivo log de acesso. Vide [*Lista de Objetos*](../objetos/lista-de-objetos.md#access_logs) para mais detalhes sobre os eventos dos logs de acesso.

## Notificações do monitor

De forma também equivalente às catracas da linha iDBlock V2, a iDBlock Next conta com o endpoint de eventos de catraca *catra_event* em todos os [*modos de operação*](../modos-de-operacao/introducao-aos-modos-de-operacao.md). Nele serão notificados eventos de giro à direita (horário), giro à esquerda (anti-horário) e desistência de acesso, conforme documentado [*aqui*](../monitor/introducao-ao-monitor.md#eventos-de-catraca).

## Eventos de identificação no modo online

Em complemento à documentação de mensagens dos [*eventos de identificação*](../modos-de-operacao/eventos-de-identificacao-online.md#evento-usuario-identificado) em modo online, caso o dispositivo seja o primário de um conjunto iDBlock Next haverá também o envio do seguinte parâmetro:

**component_id**,

o qual segue a mesma lei de formação documentada [*aqui*](../modos-de-operacao/eventos-de-identificacao-online.md#formatacao-identifier_id) e pode ser utilizado para definir se a identificação ocorreu em um iDFace primário, iDFace secundário, Wiegand da Urna ou Tampa com Display (com valores decodificados "0MS1", "0MS2", "0WS0" e "0TS0", respectivamente), possibilitando o correto gerenciamento do servidor online.

## Saída de visitantes

A saída de visitantes com cartão deve ser obrigatoriamente realizada com depósito de cartão na urna. Essa funcionalidade deve ser habilitada apenas sob o uso de catraca com urna coletora de cartão. Sua configuração pode ser feita via interface WEB do embarcado, via API ou via GUI. A requisição para habilitar essa opção está mostrada abaixo.

### Exemplo de requisição para habilitar saída de visitantes somente por cartão

Essa requisição configura a saída de visitantes somente com depósito de cartão na urna:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "sec_box": {
            "catra_collect_visitor_card": "1"
        }
    })
});
```

## Configuração dos LEDs da catraca

Através da API podemos configurar as cores dos LEDs RGB presentes na iDBlock Next e acionar eventos que trocam estas cores e estados dos LEDs.

```
$.ajax({
    url: "/remote_led_control.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "sec_box": {
            "color": "0",
            "event": 2
        }
    })
});
```

Com cada um dos parâmetros passados como detalhado abaixo:

- **color**: String contendo um número que represente um código Alfa-RGB hexadecimal (na ordem ARGB). Por exemplo "4278255360" para a cor verde (#FF00FF00) ou "4294901760" para a cor vermelha (#FFFF0000).
- **event**: Inteiro representando um evento dentre 5 opções:

  - 0: Evento autorizado (define a luz como verde)
  - 1: Evento não autorizado (define a luz como vermelha)
  - 2: Evento Idle (define a luz como *Idle Color*)
  - 3: Evento de desativação (desliga a luz)
  - 4: Evento de mudança da *Idle Color* (define a cor do estado Idle como o parâmetro de cor passado)
  - Outros: Evento Idle novamente

Em suma, as cores de evento autorizado e não-autorizado são permanentes em verde e vermelho, respectivamente. Contudo, podemos mudar a cor em estado de Idle para a que desejarmos, enviando-a com um evento de código 4, seguido de um evento de código 2 para ir ao estado Idle.

Para desligar as luzes, podemos enviar um evento de código 3.

## Contagem de giros na Tampa com Display

Através da API é possível configurar a visulização da contagem de giros na
interface do dispositivo. A configuração **catra_show_counters** habilita o modo de contagem de giros. Os contadores de giros para esquerda, direita e totais tem a sua visualização habilitada com **catra_total_counter**, **catra_left_counter** e **catra_right_counter**, respectivamente.

### Exemplo de requisição para habilitar os três contadores

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        "sec_box": {
            "catra_show_counters": "1",
            "catra_total_counter": "1",
            "catra_left_counter": "1",
            "catra_right_counter": "1"
        }
    })
});
```
