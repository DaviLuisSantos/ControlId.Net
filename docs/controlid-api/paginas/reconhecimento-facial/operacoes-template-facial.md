<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/operacoes-template-facial/ -->

# Operações de Template Facial

Os endpoints a seguir permitem extrair templates faciais de imagens, cadastrar e remover templates, e comparar um template com os registros cadastrados. São operações de baixo nível para processamento direto de dados biométricos faciais.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

## Extrair template facial

Extrai um template facial de um arquivo de imagem. O template pode então ser usado para verificação via [face_template_match](../reconhecimento-facial/operacoes-template-facial.md#verificar-template-facial) ou para cadastro via [face_template_enroll](../reconhecimento-facial/operacoes-template-facial.md#cadastrar-template-facial).

### POST /face_template_extract.fcgi

*Parâmetros*

- Arquivo de imagem em formato binário, enviado no corpo da requisição (`application/octet-stream`). A imagem deve atender aos seguintes requisitos:
  - Formato: JPG ou PNG
  - Tamanho mínimo: 160×160 pixels
  - Tamanho máximo: 1920×1080 pixels
  - Profundidade de pixel: 32 bits

*Resposta*

- **scores** *(objeto JSON)* : Medidas de posicionamento e qualidade da imagem recebida. São elas:
  > - **bounds_width** *(int)* : Largura do rosto.
  > - **horizontal_center_offset** *(int)* : Distância horizontal do centro do rosto ao centro da imagem.
  > - **vertical_center_offset** *(int)* : Distância vertical do centro do rosto ao centro da imagem.
  > - **center_pose_quality** *(int)* : Nota para qualidade de centralização, que indica se rosto está virado para a câmera ou se está torto.
  > - **sharpness_quality** *(int)* : Nitidez da imagem.
- **success** *(bool)* : Indica se o cadastro foi bem-sucedido ou não.
- **errors** *(array de objetos JSON)* : Lista contendo um ou mais erros justificando uma extração mal-sucedida. Para cada erro:
  > - **code** *(int)* : Código correspondente ao erro informado.
  > - **message** *(string)* : Mensagem descrevendo o erro.
- **face_template** *(string)* : Template facial em base 64

#### Erros possíveis da imagem a ser cadastrada

Conforme descrito no tópico [Recomendações - fotos e instalação](../reconhecimento-facial/recomendacoes-fotos-instalacao.md), existem alguns critérios de qualidade da foto que devem ser seguidos para que o reconhecimento facial ocorra da maneira correta. Caso exista uma tentativa de extração, inserindo um arquivo de imagem que não siga as recomendações previstas, haverá mensagens de erros informando os motivos pelos quais a foto não foi aceita. A seguir estão listados os códigos de erros e as mensagens correspondentes que explicam suas causas:

- **code 1:** Corresponde a erros não relacionados com a qualidade do arquivo, mas sim com algum erro na passagem de parâmetros da requisição. Existem vários tipos de mensagens que podem surgir com erros desse código, por exemplo:
  > - **message:** "Invalid data (image file expected)"
- **code 2:** Ocorre quando não é possível identificar uma face no arquivo de imagem enviado.
  > - **message:** "Face not detected"
- **code 4:** Ocorre quando as distâncias horizontais e verticais do centro do rosto ao centro da imagem estão muito significativas. Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a reposta da requisição. No *objeto JSON* *score*, é preciso analisar os valores dos parâmetros *horizontal_center_offset* e *vertical_center_offset*. O valor máximo permitido para ambos é 1000. Portanto, quando esse valor é ultrapassado a seguinte mensagem é exibida:
  > - **message:** "Face not centered"
- **code 5:** Ocorre quando a largura do rosto na imagem é muito pequena (face distante da câmera). Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a reposta da requisição. No *objeto JSON* *score*, é preciso analisar o valor do parâmetro *bounds_width*. O valor mínimo permitido é 60. Portanto, quando esse valor é ultrapassado a seguinte mensagem é exibida:
  > - **message:** "Face too distant"
- **code 6:** Ocorre quando a largura do rosto na imagem é muito grande (face muito perto da câmera). Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a reposta da requisição. No *objeto JSON* *score*, é preciso analisar o valor do parâmetro *bounds_width*. O valor máximo permitido é 800. Portanto, quando esse valor é ultrapassado a seguinte mensagem é exibida:
  > - **message:** "Face too close"
- **code 7:** Ocorre quando a centralização do rosto não está boa, indicando que o rosto está torto em relação à câmera. Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a reposta da requisição. No *objeto JSON* *score*, é preciso analisar o valor do parâmetro *center_pose_quality*. O valor máximo permitido é 400. Portanto, quando esse valor é ultrapassado a seguinte mensagem é exibida:
  > - **message:** "Face pose not centered"
- **code 8:** Ocorre quando a imagem cadastrada não possui nitidez suficiente para garantir o reconhecimento facial. Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a reposta da requisição. No *objeto JSON* *score*, é preciso analisar o valor do parâmetro *sharpness_quality*. O valor mínimo permitido é 450. Portanto, quando esse valor é inferior a seguinte mensagem é exibida:
  > - **message:** "Low sharpness"
- **code 9:** Ocorre quando o rosto está muito próximo das bordas da imagem.
  > - **message:** "Face too close to image borders"
- **code 11:** Ocorre quando a imagem fornecida é preta e branca e o parâmetro *face_enroll_mode* é definido como 1.
  > - **message:** "Face image is in grayscale"
- **code 12:** Ocorre quando há mais de uma face na imagem e o parâmetro *face_enroll_mode* é definido como 1.
  > - **message:** "Multiple faces in image"

#### Exemplo de requisição

```
$.ajax({
    url: "/face_template_extract.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de imagem]
});
```

#### Exemplo de resposta (sucesso)

```
{
    "scores": {
        "bounds_width": 113,
        "horizontal_center_offset": -12,
        "vertical_center_offset": -134,
        "center_pose_quality": 901,
        "sharpness_quality": 865
    },
    "face_template": "vRYFPUe+C76B1RY+azJD...",
    "success": true
}
```

#### Exemplo de resposta (falha)

```
{
    "success": false,
    "errors": [{ ... }]
}
```

## Extrair templates faciais em lote NEW!

Extrai templates faciais de uma lista de imagens em uma única chamada. Cada imagem é identificada por um `id` definido pelo chamador, retornado no respectivo resultado para permitir associar as respostas às entradas independentemente da ordem de processamento.

### POST /face_template_extract_list.fcgi

*Parâmetros*

- **face_images** *(array)*: Obrigatório. Cada elemento é um objeto com:
  - **id** *(int64)*: Identificador definido pelo chamador para esta imagem, retornado no resultado.
  - **image** *(string)*: Imagem codificada em Base64. Mesmos requisitos de formato/tamanho/profundidade do [face_template_extract](../reconhecimento-facial/operacoes-template-facial.md#extrair-template-facial).

*Resposta*

- **results** *(array)*: Um item por imagem de entrada, na mesma ordem, cada um contendo:
  - **id** *(int64)*: O identificador fornecido na requisição.
  - **success** *(bool)*: `true` se o template foi extraído com sucesso.
  - **face_template** *(string)*: Template facial codificado em Base64. Presente apenas quando `success` é `true`.
  - **errors** *(array)*: Lista de objetos de erro. Presente apenas quando `success` é `false`.

#### Exemplo de requisição

```
$.ajax({
    url: "/face_template_extract_list.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        face_images: [
            { id: 1, image: "imagemcodificadaembase64..." },
            { id: 2, image: "imagemcodificadaembase64..." }
        ]
    })
});
```

#### Exemplo de resposta

```
{
    "results": [
        { "id": 1, "success": true, "face_template": "templatecodificadoembase64..." },
        { "id": 2, "success": false, "errors": [{ ... }] }
    ]
}
```

## Cadastrar template facial NEW!

Cadastra um ou mais templates faciais previamente extraídos como biometria de usuários já existentes, sem passar pelo fluxo de captura/validação de foto descrito em [Cadastro facial por fotos](../reconhecimento-facial/cadastro-facial.md).

### POST /face_template_enroll.fcgi

*Parâmetros*

- **face_templates** *(array)*: Obrigatório. Cada elemento é um objeto com:
  - **user_id** *(int64)*: Obrigatório. ID de um usuário já existente para o qual o template será cadastrado.
  - **face_template** *(string)*: Obrigatório. Template facial codificado em Base64, obtido via [face_template_extract](../reconhecimento-facial/operacoes-template-facial.md#extrair-template-facial). O template decodificado deve ter exatamente 1024 bytes.
  - **timestamp** *(int)*: Timestamp associado à imagem de origem, armazenado como o timestamp da foto do usuário.
- **match** *(bool)*: Opcional, padrão `true`. Quando `true`, o template é primeiro comparado com os rostos já cadastrados; se houver correspondência, o cadastro é rejeitado com erro "Face exists" em vez de ser salvo. Defina como `false` para ignorar essa verificação.

*Resposta*

- **results** *(array)*: Um item por template de entrada, cada um contendo:
  - **user_id** *(int64)*: O ID de usuário fornecido na requisição.
  - **success** *(bool)*: `true` se o template foi cadastrado com sucesso.
  - **errors** *(array)*: Lista de objetos de erro. Presente apenas quando `success` é `false`.

#### Exemplo de requisição

```
$.ajax({
    url: "/face_template_enroll.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        face_templates: [
            { user_id: 42, face_template: "templatecodificadoembase64...", timestamp: 1700000000 }
        ],
        match: true
    })
});
```

#### Exemplo de resposta

```
{
    "results": [
        { "user_id": 42, "success": true }
    ]
}
```

## Remover template facial NEW!

Remove templates faciais cadastrados. Exatamente um dos parâmetros **all**, **user_ids** ou **user_id** deve ser fornecido.

### POST /face_template_destroy.fcgi

*Parâmetros*

- **all** *(bool)*: Se `true`, remove os templates faciais de todos os usuários do equipamento e zera os respectivos timestamps de imagem.
- **user_ids** *(array de int64)*: Remove os templates faciais dos usuários indicados.
- **user_id** *(int64)*: Remove o template facial de um único usuário já existente.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/face_template_destroy.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        user_ids: [42, 43]
    })
});
```

## Contar usuários com template facial NEW!

Retorna o número de usuários distintos que possuem atualmente ao menos um template facial cadastrado no equipamento.

### GET /face_template_count_distinct.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **count** *(int)*: Número de usuários distintos com template facial cadastrado.

#### Exemplo de requisição

```
$.ajax({
    url: "/face_template_count_distinct.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "count": 128
}
```

## Verificar template facial

Compara um template facial com os templates cadastrados no equipamento. Opcionalmente restringe a busca a um usuário específico.

### POST /face_template_match.fcgi

*Parâmetros*

- **face_template** *(string)*: **Obrigatório.** Template facial codificado em Base64, obtido via [face_template_extract](../reconhecimento-facial/operacoes-template-facial.md#extrair-template-facial). O template decodificado deve ter exatamente 1024 bytes.
- **user_id** *(int64)*: Opcional. Se fornecido, restringe a verificação aos templates do usuário indicado. Se omitido ou `0`, compara com todos os usuários cadastrados.

*Resposta*

- **match** *(bool)*: `true` se um template correspondente foi encontrado.
- **info** *(object)*: Presente apenas quando `match` é `true`. Contém:
  - **match_user_id** *(int64)*: ID do usuário correspondente.
  - **match_score** *(int)*: Pontuação principal de confiança do verificador, em uma escala fixa de 0 a 1000. É o valor usado para decidir se a correspondência é aceita, comparado ao parâmetro de configuração **match_score_threshold** (padrão `700`).
  - **match_similarity** *(int)*: Uma métrica de similaridade secundária, usada como sinal adicional de calibração junto ao `match_score`. É calculada em sua própria escala interna e multiplicada por 1000 antes de ser retornada aqui, para corresponder ao parâmetro de configuração **match_similarity_threshold** (padrão `3.5`, ou seja, `3500` neste valor de resposta já escalado).

`match_score` e `match_similarity` são duas saídas independentes do verificador facial, não uma versão bruta/normalizada da mesma medida — um `match_score` alto não garante um `match_similarity` proporcionalmente alto, e vice-versa. Ao ajustar a sensibilidade de aceitação, ajuste os dois limiares em conjunto, em vez de depender de apenas um deles.

#### Exemplo de requisição

```
$.ajax({
    url: "/face_template_match.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        face_template: "templatecodificadoembase64...",
        user_id: 42
    })
});
```

#### Exemplo de resposta (correspondência encontrada)

```
{
    "match": true,
    "info": {
        "match_user_id": 42,
        "match_score": 850,
        "match_similarity": 4120
    }
}
```

#### Exemplo de resposta (sem correspondência)

```
{
    "match": false
}
```
