<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/configuracoes-reconhecimento-facial/ -->

# Configurações Gerais

As funções descritas abaixo devem ser usadas para ajustar o funcionamento do reconhecimento facial, conforme a necessidade do usuário. Assim como os outros parâmetros de configuração do dispositivo, o método HTTP usado para modificação é o POST e o contentType é application/json. O parâmetro será um Objeto JSON correspondente aos parâmetros de configuração que se deseja modificar.

## Uso de máscara

Essa funcionalidade permite o reconhecimento da face de usuários que estão usando máscaras. Ou seja, se o dispositivo estiver instalado em um local onde o uso de máscara é obrigatório ou recomendado, essa função deve estar habilitada para que o reconhecimento dos usuários seja feito de forma correta.
O parâmetro que define a ativação dessa função é o **mask_detection_enabled**. Os valores de entrada possíveis para ele são: "0" (desabilitado), indica que o acesso será autorizado ou não independentemente do uso de máscara; "1" (obrigatório), indica que o acesso somente será autorizado para usuários que estiverem utilizando máscara; e "2" (recomendado), indica que o acesso poderá ser autorizado sem a utilização de máscara, porém, uma mensagem será exibida ao usuário recomendando o uso de máscara. Por padrão de fábrica este parâmetro vem com o valor "0".

Obs: A restrição de acesso por uso de máscara só tem efeito quando o equipamento está operando em modo Standalone. Em modo Online, é enviado o parâmetro "face_mask" na resposta de identificação de usuário indicando se o usuário está de máscara, para validação de regra de acesso do lado do servidor.

### Exemplo de requisição

Esta requisição configura os parâmetros de uso de máscara obrigatório.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_id": {
                "mask_detection_enabled": "1",
            }
        }
    )
});
```

## Modo de detecção

Essa funcionalidade oferece a possibilidade de utilizar o reconhecimento facial em dois modos diferentes, o **veicular** e o **pedestre**. O modo pedestre é o modo usual e é o padrão de fábrica. O modo veicular permite o reconhecimento de pessoas que estão em veículos. Ou seja, se o dispositivo estiver instalado em um local onde detecção será feita em usuários que estão no interior de um veículo, essa função deve estar habilitada para que o reconhecimento dos usuários seja feito de forma correta.
O parâmetro que define a ativação dessa função é o **vehicle_mode**. Os valores de entrada possíveis para ele são: "0" (desabilitado) ou "1"(habilitado). Por padrão de fábrica este parâmetro vem com o valor "0" - modo pedestre.

### Exemplo de requisição

Esta requisição configura os parâmetros do modo de detecção.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_id": {
                "vehicle_mode": "1"
            }
        }
    )
});
```

## Tempo para liberar de novo a mesma face (s)

Através dessa configuração é possível ajustar o tempo em que um mesmo usuário poderá ser novamente identificado, após a última identificação facial. O parâmetro que define este tempo é o **max_identified_duration**. Por padrão de fábrica este parâmetro vem com o valor "30000", que corresponde a 30 segundos, pois a unidade do parâmetro é em milissegundos.

### Exemplo de requisição

Esta requisição configura os parâmetros para ajuste do tempo para liberar de novo a mesma face.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_id": {
                "max_identified_duration": "30000"
            }
        }
    )
});
```

## Região de Interesse

Essa função torna possível fazer ajustes na câmera do dispositivo para definir a região onde o rosto do usuário deverá ser identificado. Podem ser feitos dois ajustes: Zoom ou Deslocamento vertical da imagem. O parâmetro que define o zoom aplicado está no módulo **zoom** e o que define o deslocamento vertical é o **vertical_crop**. Os valores de entrada possíveis para o zoom são números entre o intervalo: 3.25 (zoom máximo) até 1.0 (zoom mínimo). Os valores de entrada possíveis para deslocamento vertical da imagem são números entre o intervalo: -0.36 (máximo deslocamento para cima - zoom máximo) até 0.36 (máximo deslocamento para baixo - zoom máximo).O valor de entrada padrão de fábrica é 1.5 para o Zoom e -0.06 para o deslocamento vertical.

Obs: Apesar de ser possível alterar esses parâmetros remotamente, recomenda-se que estes ajustes não sejam feitos de forma remota, pois para um bom ajuste da região de interesse é importante uma verificação visual das mudanças no display do próprio equipamento. Além disso, os valores de zoom e deslocamento vertical possuem uma relação de dependência entre si. Sendo que conforme diminuímos o zoom, o deslocamento vertical é limitado.

### Exemplo de requisição

Esta requisição configura os parâmetros de zoom.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "camera_overlay": {
                "zoom": "1.5"
            }
        }
    )
});
```

Esta requisição configura os parâmetros de deslocamento vertical da imagem.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "camera_overlay": {
                "vertical_crop": "-0.06"
            }
        }
    )
});
```

## Ativação dos LEDs brancos

Essa configuração permite definir o nível de luminosidade ambiente necessário para o acendimento dos LEDs brancos durante a identificação. Existem 4 opções possíveis para o acendimento: baixa luminosidade, média luminosidade, alta luminosidade e acesos em qualquer ambiente. A configuração deve ser feita através do parâmetro `light_threshold_led_activation` do módulo `face_module`. Os valores de entrada possíveis e as respectivas condições são: "0" (baixa luminosidade, definido por padrão), "1" (média luminosidade), "2" (alta luminosidade) e "3" (acesos em qualquer ambiente).

### Exemplo de requisição

Esta requisição configura o acendimento dos LEDs brancos em ambientes com baixa luminosidade.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_module": {
                "light_threshold_led_activation": "40"
            }
        }
    )
});
```

## Limitação de identificação à região do display

Essa configuração permite limitar a identificação facial à região visível no display do iDFace, a qual é exibida para o usuário. Por padrão, a região do campo de visão em que o dispositivo é capaz de detectar a presença de rostos e identificar o usuário é maior do que a região exibida no display do equipamento. A configuração deve ser feita através do parâmetro `limit_identification_to_display_region` do módulo `face_id`. Os valores são "0" para desativado ou "1" para ativar a restrição da identificação facial à região visível na tela.

### Exemplo de requisição

Esta requisição limita identificação à região do display.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_id": {
                "limit_identification_to_display_region": "1"
            }
        }
    )
});
```

## Modo de Liveness

Essa configuração modifica os parâmetros utilizados na detecção de rosto vivo do reconhecimento facial, adotando valores padrão ou mais rigorosos. Com isso, a identificação se trona mais restritiva ao determinar se a face detectada pertence a um indivíduo fisicamente presente ou não. A configuração deve ser feita através do parâmetro `liveness_mode` do módulo `face_id`. Os valores são "0" para utilizar o modo normal e "1" para o modo rigoroso.

### Exemplo de requisição

Esta requisição ativa o modo de liveness rigoroso.

```
$.ajax({
    url: "set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_id": {
                "liveness_mode": "1"
            }
        }
    )
});
```

## Distância de Identificação (cm)

Essa configuração modifica um dos parâmetros utilizados no processo de reconhecimento facial, a saber, a distância (em cm) em que o rosto do indivíduo deve estar com relação ao equipamento. Dessa forma, mesmo com a faixa de distância seguramente reconhecida pelo dispositivo (de 30cm a 200cm), é possível configurar um valor mais preciso a fim de se adequar ao contexto do ambiente (ambientes muito amplos podem requerer uma distância menor para que a liberação seja mais eficiente). A configuração deve ser feita através do parâmetro `min_detect_bounds_width` do módulo `face_id`. Contudo, o parâmetro não recebe a distância de identificação em centímetros (cm) mas, sim, o resultado de um cálculo.

*Cálculo*

- min_detect_bounds_width = 11.6/distance_cm

### Exemplo de requisição

Esta requisição configura a distância de identificação do dispositivo para 40cm.

```
$.ajax({
    url: "set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "face_id": {
                "min_detect_bounds_width": "0.29"
            }
        }
    )
});
```

## Brilho dos LEDs:

Essa configuração permite alterar o brilho dos LEDs brancos ativados durante o reconhecimento facial. Os valores determinam a potência do LED e são representados por uma porcentagem. Dessa forma, os valores válidos estão na faixa de 0 a 100. A configuração deve ser feita através do parâmetro `brightness` do módulo `led_white`. Os valores são entre "0" para LED apagado e "100" para LED com o máximo brilho possível.

### Exemplo de requisição

Esta requisição configura o brilho dos LEDs para 70%.

```
$.ajax({
    url: "set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "led_white": {
                "brightness": "70"
            }
        }
    )
});
```

## Verificar status de identificação

Retorna se uma identificação facial está em andamento no equipamento.

Disponível apenas na versão **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /is_identifying.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **is_identifying** *(bool)*: `true` se uma identificação facial está em andamento.

#### Exemplo de requisição

```
$.ajax({
    url: "/is_identifying.fcgi?session=" + session,
    type: 'GET'
});
```

#### Exemplo de resposta

```
{
    "is_identifying": false
}
```
