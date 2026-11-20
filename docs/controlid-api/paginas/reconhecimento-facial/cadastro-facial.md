<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/cadastro-facial/ -->

# Cadastro facial por fotos

As funções descritas abaixo devem ser usadas para cadastrar, ler e apagar fotos de usuários nos terminais de controle de acesso da Control iD com **reconhecimento facial**.

Neste tipo de equipamento, não é necessário lidar com templates, sendo suficiente incluir ou modificar a foto do usuário para que o reconhecimento facial ocorra normalmente.

Contudo, por questão de privacidade, é possível configurar o equipamento para que a foto seja removida logo após o cadastro do usuário, sendo utilizada apenas para gerar o template facial correspondente. Por padrão, a imagem do usuário é mantida. Se for necessário, é possível configurar a remoção da foto após o cadastro como apresentado na seção [Remoção de foto do usuário após o cadastro](../reconhecimento-facial/cadastro-facial.md#remocao-de-foto-do-usuario-apos-o-cadastro).

## Obter foto do usuário

Obtém a foto de um usuário especificado pelo seu id. O retorno pode ser tanto a própria imagem, no formato **image/jpeg**, quanto um objeto JSON que contém os dados da imagem em formato Base64 e o timestamp de cadastro no padrão Unix Timestamp.

O formato de retorno é definido pelos parâmetros passados na requisição. Como o método HTTP usado é o GET, todos os parâmetros são enviados através de query string.

### GET /user_get_image.fcgi

*Parâmetros*

- **user_id** *(int64)* : Identificador do usuário cuja foto será obtida.
- **get_timestamp** *(int)* : Se positivo, determina retorno em JSON contendo o timestamp de cadastro da foto.

*Resposta*

> Quando *get_timestamp = 0*:
>
> - Imagem do usuário em formato **jpg**
>
> Quando *get_timestamp = 1*:
>
> - **timestamp** *(int)* : Valor de timestamp da foto cadastrada no padrão Unix Timestamp.
> - **image** *(string)* : Imagem de cadastro em formato **Base64**

#### Exemplo de requisição

Esta requisição retorna somente a foto do usuário em formato jpg (get_timestamp=0).

```
$.ajax({
  url: "/user_get_image.fcgi?user_id=123&get_timestamp=0&session=" + session,
  type: 'GET',
});
```

#### Exemplo de resposta quando get_timestamp=1

```
{
  "timestamp": 1624997578,
  "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAE ... SurHnqq4vn5U3Pf5H//Z"
}
```

## Obter lista de usuários com foto cadastrada

Obtém a lista de ids dos usuários que possuem foto cadastrada no equipamento.

### GET /user_list_images.fcgi

*Parâmetros*

- **get_timestamp** *(int)* : Se positivo, determina retorno em JSON contendo o timestamp de cadastro da foto.

*Resposta*

> Quando *get_timestamp = 0*:
>
> - **user_ids** *(array de int64)* : Lista de ids dos usuários com foto cadastrada.
>
> Quando *get_timestamp = 1*:
>
> - **image_info** *(array de objetos JSON)* : Lista de usuários com foto cadastrada, contendo:
>   > - **user_id** *(int64)* : Identificador do usuário.
>   > - **timestamp** *(int)* : Valor de timestamp da foto cadastrada no padrão Unix Timestamp.

#### Exemplo de requisição

Esta requisição retorna a lista de usuários com foto cadastrada, incluindo identificador e timestamp de cadastro para cada um deles.

```
$.ajax({
  url: "/user_list_images.fcgi?get_timestamp=1&session=" + session,
  type: 'GET'
});
```

#### Exemplo de resposta

```
{
  "image_info": [
    {
        "user_id": 1,
        "timestamp": 1628203752
    },
    {
        "user_id": 2,
        "timestamp": 1628203752
    },
    {
        "user_id": 3,
        "timestamp": 1628203752
    }
  ]
}
```

## Obter lista de fotos de usuário

Obtém lista de fotos cadastradas no equipamento, conforme os ids de usuário requeridos. Esta chamada está limitada a retornar **no máximo 100 fotos** de usuário por requisição.

### POST /user_get_image_list.fcgi

*Parâmetros*

- **user_ids** *(array de int64)* : Lista de ids dos usuários cujas fotos devem ser retornadas.

*Resposta*

- **user_images** *(array de objetos JSON)* : Lista com os dados de imagem de cada usuário.

> Cada objeto contém:
>
> - **id** *(int64)* : Identificador do usuário cuja foto será obtida.
> - **timestamp** *(int)* : Timestamp no formato Unix Timestamp da foto de cadastro retornada.
> - **image** *(string)* : Arquivo de imagem do usuário no formato **Base64**.
>
> Quando o retorno falha para um usuário, o objeto contém as informações de erro:
>
> - **id** *(int64)* : Identificador do usuário cuja foto foi requerida.
> - **error** *(objeto JSON)* : Objeto de informações de erro, composto por:
>   > - **code** *(int)* : Código de erro.
>   > - **message** *(string)* : Mensagem explicativa do erro.

#### Exemplo de requisição

Esta requisição irá retornar imagens de uma lista de usuários.

```
$.ajax({
  url: "/user_get_image_list.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json'
  data: {
    user_ids: [ 1, 2, 3 ]
  }
});
```

#### Exemplo de resposta

```
{
  "user_images": [
    {
      "id": 1,
      "timestamp": 1626890032,
      "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAE ... BrBRkuWacorVep//2Q=="
    },
    {
      "id": 2,
      "error": {
        "code": 1,
        "message": "User does not exist"
      }
    },
    {
      "id": 3,
      "timestamp": 1626889927,
      "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAE ... 8kP5Tl+u4uesqtj/2Q=="
    }
  ]
}
```

## Cadastrar foto de usuário

Salva e cadastra a foto de um usuário especificado pelo seu id. Diferentemente da grande maioria dos comandos desta API, o Content-Type deste comando deve ser necessariamente **application/octet-stream**. A imagem é passada no corpo da requisição POST e as informações de cadastro são passadas na Query String. O arquivo da foto deve ter tamanho menor que 2 MB.

Para cadastro de usuários em massa, recomenda-se o uso do endpoint /user_set_image_list.fcgi.

Recomendações sobre formato e tamanho das imagens, além de posicionamento de rosto para cadastro, podem ser encontradas consultando o tópico [Recomendações - fotos e instalação](../reconhecimento-facial/recomendacoes-fotos-instalacao.md).

### POST /user_set_image.fcgi

*Parâmetros*

- **user_id** *(int64)* : Identificador do usuário cuja foto será atribuída (**obrigatório**).
- **timestamp** *(int)* : Timestamp no formato Unix Timestamp a ser registrado para o cadastro da foto (**obrigatório**).
- **match** *(int)* : Assume os valores 0 ou 1. Quando seu valor é 1, o cadastro da foto deverá ser rejeitado se o rosto já estiver cadastrado para outro usuário. Para cadastros em massa, é recomendável não verificar duplicados (match = 0) para acelerar o processo.
- **bypass_quality_score** *(int)*: Assume os valores 0 ou 1. Quando seu valor é 1, ignora a verificação de qualidade da imagem no cadastro da foto. Padrão: 0.
- **import** *(int)*: Assume os valores 0 ou 1. Quando seu valor é 1 e o equipamento está no modo de cadastro facial estrito, a foto é avaliada com os critérios de qualidade do modo padrão, sem as verificações de imagem em escala de cinza e de múltiplos rostos. Padrão: 0. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

> **Esses parâmetros são passados na Query String.**

Atenção: uso de bypass_quality_score

O parâmetro **bypass_quality_score** existe para facilitar a migração de bancos de dados de fotos já existentes. Fotos cadastradas sem a verificação de qualidade podem causar falhas ou lentidão na identificação facial. Não utilize este parâmetro em cadastros do dia a dia.

*Resposta*

- **user_id** *(int64)* : Identificador do usuário cuja foto será atribuída.
- **scores** *(objeto JSON)* : Medidas de posicionamento e qualidade da imagem recebida. São elas:
  > - **bounds_width** *(int)* : Largura do rosto.
  > - **horizontal_center_offset** *(int)* : Distância horizontal do centro do rosto ao centro da imagem.
  > - **vertical_center_offset** *(int)* : Distância vertical do centro do rosto ao centro da imagem.
  > - **center_pose_quality** *(int)* : Nota para qualidade de centralização, que indica se o rosto está virado para a câmera ou se está torto.
  > - **sharpness_quality** *(int)* : Nitidez da imagem.
- **success** *(bool)* : Indica se o cadastro foi bem-sucedido ou não.
- **errors** *(array de objetos JSON)* : Lista contendo um ou mais erros justificando um cadastro mal-sucedido. Para cada erro:
  > - **code** *(int)* : Código correspondente ao erro informado.
  > - **message** *(string)* : Mensagem descrevendo o erro.
  > - **info** *(objeto JSON)*: Retornado apenas com o erro de código 3 ("Face exists"). Contém **match_user_id** *(int64)*, o ID do usuário cuja face corresponde à foto enviada, e **match_score** *(int)*, a pontuação da correspondência. Em alguns equipamentos com firmware [V6](../particularidade-dos-produtos/versoes-de-firmware.md), a pontuação é retornada em **match_confidence** *(int)* no lugar de **match_score**. A partir dos **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**, contém também **match_similarity** *(int)*, o grau de similaridade entre as faces.
- **face_template** *(string)*: Template facial extraído da foto, em Base64. Retornado apenas quando a foto é aceita e a configuração **return_face_template** do módulo **enroller** está em 1.

#### Erros possíveis da imagem a ser cadastrada

Conforme descrito no tópico [Recomendações - fotos e instalação](../reconhecimento-facial/recomendacoes-fotos-instalacao.md), existem alguns critérios de qualidade da foto que devem ser seguidos para que o reconhecimento facial ocorra da maneira correta. Caso exista uma tentativa de cadastramento de foto remoto, inserindo um arquivo de imagem que não siga as recomendações previstas, haverá mensagens de erro informando os motivos pelos quais a foto não foi aceita. A seguir estão listados os códigos de erro e as mensagens correspondentes que explicam suas causas:

- **code 1:** Corresponde a erros não relacionados com a qualidade do arquivo, mas sim com algum erro na passagem de parâmetros da requisição. Existem vários tipos de mensagens que podem surgir com erros desse código. Alguns exemplos:
  > - **message:** "Image file not recognized. Image should be either JPG or PNG.", "User does not exist", "Invalid user_id".
- **code 2:** Ocorre quando não é possível identificar uma face no arquivo de imagem enviado.
  > - **message:** "Face not detected"
- **code 3:** Ocorre quando há uma tentativa de cadastro de face que já existe. Além de retornar a mensagem de erro abaixo, é retornado também o ID do usuário que coincide com a foto enviada. O parâmetro `match_user_id` indica o ID do usuário já existente que corresponde à foto.
  > - **message:** "Face exists"
  > - **info { match_user_id: 1 }**
- **code 4:** Ocorre quando as distâncias horizontais e verticais do centro do rosto ao centro da imagem são grandes demais. Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a resposta da requisição. No *objeto JSON* *scores*, é preciso analisar os valores dos parâmetros *horizontal_center_offset* e *vertical_center_offset*. O valor máximo permitido para ambos é 1000. Portanto, quando esse valor é ultrapassado, a seguinte mensagem é exibida:
  > - **message:** "Face not centered"
- **code 5:** Ocorre quando a largura do rosto na imagem é muito pequena (face distante da câmera). Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a resposta da requisição. No *objeto JSON* *scores*, é preciso analisar o valor do parâmetro *bounds_width*. O valor mínimo permitido é 60. Portanto, quando o valor é inferior a esse mínimo, a seguinte mensagem é exibida:
  > - **message:** "Face too distant"
- **code 6:** Ocorre quando a largura do rosto na imagem é muito grande (face muito perto da câmera). Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a resposta da requisição. No *objeto JSON* *scores*, é preciso analisar o valor do parâmetro *bounds_width*. O valor máximo permitido é 800. Portanto, quando esse valor é ultrapassado, a seguinte mensagem é exibida:
  > - **message:** "Face too close"
- **code 7:** Ocorre quando a centralização do rosto não está boa, indicando que o rosto está torto em relação à câmera. Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a resposta da requisição. No *objeto JSON* *scores*, é preciso analisar o valor do parâmetro *center_pose_quality*. O valor máximo permitido é 400. Portanto, quando esse valor é ultrapassado, a seguinte mensagem é exibida:
  > - **message:** "Face pose not centered"
- **code 8:** Ocorre quando a imagem cadastrada não possui nitidez suficiente para garantir o reconhecimento facial. Para entender quantitativamente como isso pode ser resolvido, deve-se analisar a resposta da requisição. No *objeto JSON* *scores*, é preciso analisar o valor do parâmetro *sharpness_quality*. O valor mínimo permitido é 450. Portanto, quando o valor é inferior a esse mínimo, a seguinte mensagem é exibida:
  > - **message:** "Low sharpness"
- **code 9:** Ocorre quando o rosto está muito próximo das bordas da imagem.
  > - **message:** "Face too close to image borders"

#### Exemplo de requisição

Esta requisição irá cadastrar foto e timestamp para um usuário.

```
$.ajax({
  url: "/user_set_image.fcgi?user_id=123&timestamp=1624997578&match=0&session=" + session,
  type: 'POST',
  contentType: 'application/octet-stream',
  data: [bytes da imagem enviada]
});
```

#### Exemplo de resposta

Este resultado corresponde a um cadastro mal-sucedido devido à baixa nitidez da imagem fornecida.

```
{
  "user_id": 123,
  "scores": {
    "bounds_width": 397,
    "horizontal_center_offset": 87,
    "vertical_center_offset": -75,
    "center_pose_quality": 698,
    "sharpness_quality": 105
  },
  "success": false,
  "errors": [
    {
      "code": 8,
      "message": "Low sharpness"
    }
  ]
}
```

## Cadastrar lista de fotos de usuário NEW!

Este endpoint cadastra fotos de usuários em massa. Ele recebe um array de ids, timestamps e imagens codificadas em Base64. O limite de tamanho de requisição aceito pelo controlador de acesso é de 2 MB.

Recomendações sobre formato e tamanho das imagens, além de posicionamento de rosto para cadastro, podem ser encontradas consultando o tópico [Recomendações - fotos e instalação](../reconhecimento-facial/recomendacoes-fotos-instalacao.md).

### POST /user_set_image_list.fcgi

*Parâmetros*

- **match** *(bool)* : Indica se o cadastro das fotos deverá rejeitar aquelas cujos rostos já estiverem cadastrados para outros usuários. Para cadastros em massa, é recomendável não verificar duplicados (match = false) para acelerar o processo.
- **user_images** *(array de objetos JSON)* : Cada elemento deverá ser um objeto JSON contendo os membros **user_id**, **timestamp** e **image** (a imagem em Base64), todos **obrigatórios**.
- **bypass_quality_score** *(bool)*: Ignora a verificação de qualidade da imagem no cadastro das fotos. Padrão: false.

Atenção: uso de bypass_quality_score

O parâmetro **bypass_quality_score** existe para facilitar a migração de bancos de dados de fotos já existentes. Fotos cadastradas sem a verificação de qualidade podem causar falhas ou lentidão na identificação facial. Não utilize este parâmetro em cadastros do dia a dia.

*Resposta*

- **results** *(array de objetos JSON)* : Lista dos resultados individuais para o cadastro de cada foto enviada na requisição. Cada objeto de resultado possui o mesmo formato de resposta que o da chamada *Cadastrar foto de usuário* acrescido do respectivo identificador de usuário (incluindo, quando aplicáveis, **face_template** e o objeto **info** nos erros).

#### Exemplo de requisição

Esta requisição irá atribuir fotos a dois usuários.

```
$.ajax({
  url: "/user_set_image_list.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: {
    "match": false,
    "user_images": [
      {
        "user_id": 20,
        "timestamp": 1628727478,
        "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAY ... QK5JP3FQw2eE1oQf/9k="
      },
      {
        "user_id": 23,
        "timestamp": 1628873297,
        "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAo ... D1odQIroECBAgQIJ/9k="
      }
    ]
  }
});
```

#### Exemplo de resposta

Considerando respectivamente as situações de:

1. Erro na requisição
2. Erro na imagem ([Erros](../reconhecimento-facial/cadastro-facial.md#erros-possiveis-da-imagem-a-ser-cadastrada))
3. Rosto cadastrado com sucesso
4. Rosto rejeitado pelos critérios de validação

O resultado abaixo mostra os formatos de retorno correspondentes.

```
{
  "results": [
    {
      "user_id": 1,
      "success": false,
      "errors": [
        {
          "code": 1,
          "message": "Failed: Invalid member 'timestamp' (int expected, got string)"
        }
      ]
    },
    {
      "user_id": 2,
      "success": false,
      "errors": [
        {
          "code": 2,
          "message": "Face not detected"
        }
      ]
    },
    {
      "user_id": 3,
      "scores": {
        "bounds_width": 104,
        "horizontal_center_offset": 16,
        "vertical_center_offset": -150,
        "center_pose_quality": 768,
        "sharpness_quality": 1000
      },
      "success": true
    },
    {
      "user_id": 4,
      "scores": {
        "bounds_width": 151,
        "horizontal_center_offset": -16,
        "vertical_center_offset": -24,
        "center_pose_quality": 502,
        "sharpness_quality": 789
      },
      "success": false,
      "errors": [
        {
          "code": 7,
          "message": "Face pose not centered"
        }
      ]
    }
  ]
}
```

## Limiares personalizados para reconhecimento facial de faces semelhantes

Durante o cadastro facial, é possível que a face seja rejeitada por ser semelhante a um rosto já cadastrado no sistema. Nessas situações, o parâmetro *match_confidence* será reportado, em conjunto com o id do usuário a quem pertence a foto já existente. Com essa informação é possível modificar os limiares utilizados no reconhecimento facial da imagem já existente para que o novo rosto não seja compatível. É importante ressaltar que esse processo irá tornar a identificação para essa face mais rigorosa, e pode interferir na identificação durante o uso do equipamento, além de ser um procedimento empírico. Por esses motivos, é recomendado que as mudanças no parâmetro sejam feitas junto ao Suporte da Control iD.

### POST /create_objects.fcgi

*Parâmetros*

- **object** *(string)* : Tipo do objeto a ser criado. Valor "custom_thresholds".
- **values** *(array de objetos JSON)* : Cada elemento deve ser um objeto JSON representando o objeto a ser criado.
  > - **threshold** *(int)* : Valor do threshold desejado para criar uma identificação mais rígida.
  > - **user_id** *(int)* : id do usuário a quem o threshold customizado será vinculado.

*Resposta*

- **ids** *(array de inteiros de 64 bits)* : ids dos objetos criados.

#### Exemplo de requisição

Esta requisição irá criar um threshold customizado para um usuário.

```
$.ajax({
  url: "/create_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    object: "custom_thresholds",
    values: [{threshold: 1200, user_id: 3}]
  })
});
```

## Testar foto de usuário

Como o cadastro de fotos no equipamento deve obedecer a uma série de critérios, é possível realizar um teste prévio com a imagem que será cadastrada no sistema, identificando se ela se encontra apta para ser armazenada. Esse processo de teste segue premissas semelhantes às de um cadastro comum, entretanto, **imagens testadas nunca são armazenadas no equipamento**.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /user_test_image.fcgi

*Parâmetros*

- Imagem, em formato binário, que será testada.
- **match** *(int)*: Assume os valores 0 ou 1. Quando seu valor é 1 (padrão), o teste também verifica se o rosto já está cadastrado para outro usuário e, nesse caso, retorna o erro de código 3 ("Face exists"). **Esse parâmetro é passado na Query String.**

*Resposta*

- **scores** *(objeto JSON)* : Medidas de posicionamento e qualidade da imagem recebida. São elas:
  > - **bounds_width** *(int)* : Largura do rosto.
  > - **horizontal_center_offset** *(int)* : Distância horizontal do centro do rosto ao centro da imagem.
  > - **vertical_center_offset** *(int)* : Distância vertical do centro do rosto ao centro da imagem.
  > - **center_pose_quality** *(int)* : Nota para qualidade de centralização, que indica se o rosto está virado para a câmera ou se está torto.
  > - **sharpness_quality** *(int)* : Nitidez da imagem.
- **success** *(bool)* : Indica se o cadastro com a imagem testada seria bem-sucedido.
- **errors** *(array de objetos JSON)* : Lista contendo um ou mais erros justificando o motivo de a foto testada ter sido rejeitada. Para cada erro:
  > - **code** *(int)* : Código correspondente ao erro informado.
  > - **message** *(string)* : Mensagem descrevendo o erro.
  > - **info** *(objeto JSON)*: Retornado apenas com o erro de código 3 ("Face exists"). Contém **match_user_id** *(int64)*, o ID do usuário cuja face corresponde à foto enviada, e **match_score** *(int)*, a pontuação da correspondência. Em alguns equipamentos com firmware [V6](../particularidade-dos-produtos/versoes-de-firmware.md), a pontuação é retornada em **match_confidence** *(int)* no lugar de **match_score**. A partir dos **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**, contém também **match_similarity** *(int)*, o grau de similaridade entre as faces.

Descrições mais detalhadas dos erros possíveis encontram-se [aqui](../reconhecimento-facial/cadastro-facial.md#erros-possiveis-da-imagem-a-ser-cadastrada).

#### Exemplo de requisição

Esta requisição irá realizar o teste de uma imagem.

```
$.ajax({
  url: "/user_test_image.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/octet-stream',
  data: [bytes da imagem enviada]
});
```

## Excluir foto de usuário

Exclui fotos de usuários especificados pelo id. Pode conter um único id, um array de ids ou uma flag "all" para remover todos. A exclusão da foto implica necessariamente a exclusão do registro facial do usuário para reconhecimento.

### POST /user_destroy_image.fcgi

*Parâmetros*

- **user_id** *(int64)* : Identificador do usuário cuja foto será excluída.
- **user_ids** *(array de int64)* : Array de ids de usuários cujas fotos serão removidas.
- **dangling** *(bool)* : Se for enviado como 'true', remove as fotos não vinculadas a usuários.
- **all** *(bool)* : Se for enviado como 'true', todos os usuários terão as fotos excluídas.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Remove a foto do usuário com id 123.

```
$.ajax({
  url: "/user_destroy_image.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    user_id: "123"
  })
});
```

## Remoção de foto do usuário após o cadastro

É possível configurar o equipamento para que a foto seja removida logo após o cadastro do usuário, utilizando-a apenas para gerar o template de reconhecimento facial. Por padrão, as fotos dos usuários são mantidas após o cadastro.

A mudança pode ser realizada alterando-se o valor do parâmetro `keep_user_image`, sendo que
os valores possíveis são `'1'` (as fotos dos usuários são mantidas após o cadastro) ou `'0'` (as fotos dos usuários são removidas).

### Exemplo de requisição

Configura o equipamento para remover a foto dos usuários após seu cadastro.

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {
      "keep_user_image": '0'
    }
  })
});
```

## Sincronização de fotos de usuário

A API de cadastro facial pode ser utilizada também em situações nas quais se deseja sincronizar as fotos de usuário de um equipamento com um banco de dados externo. Para esses casos, é interessante fazer uso do parâmetro de **timestamp** de foto para identificar quais usuários precisam de atualização.

O conjunto de passos a seguir apresenta um método de sincronização utilizando as funções da API.

1. Obter usuários e timestamps de cadastro com **user_list_images**.
2. Para cada usuário, avaliar se o timestamp é maior no banco de dados do sistema externo ou do equipamento.
3. Separar os usuários cuja foto precisa ser atualizada.
4. Enviar o conjunto de fotos para atualização por usuário com **user_set_image_list**.
