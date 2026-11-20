<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/cadastro-remoto-biometria-facial-cartao/ -->

# Cadastro Remoto de Biometria, Face, Cartão, PIN ou Senha

Realiza o cadastro de impressão digital, face, cartão de proximidade, PIN ou senha remotamente. O método HTTP usado é o POST.

### POST /remote_enroll.fcgi NEW!

*Parâmetros*

- **type** *(string)* : Parâmetro que define a forma como será realizado o cadastro, sendo "card", "face", "biometry", "pin" ou "password" (**obrigatório**).
- **user_id** *(int)* : O parâmetro "user_id" recebe o número de identificação do usuário (**obrigatório** quando "save" for "true").
- **save** *(bool)* : O parâmetro "save" indica se o objeto (cartão, face, impressão digital, PIN ou senha) deverá ser salvo no equipamento ou não. Caso "save" seja "false", o objeto será enviado ao gerenciador cadastrado, ou ao servidor (caso o equipamento esteja em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md)). Caso "save" seja "true", o parâmetro *user_id* precisa ser enviado na requisição e deve conter o número de identificação de um usuário já existente no equipamento. Padrão: "false".
- **panic_finger** *(int)* : O parâmetro "panic_finger" indica se o dedo a ser cadastrado é um dedo de pânico,
  sendo os valores possíveis 0 ou 1.
- **registration** *(string)* : O parâmetro "registration" corresponde à matrícula da pessoa que está sendo cadastrada.
- **message** *(string)* : O parâmetro "message" é a mensagem que será mostrada ao usuário durante o cadastro. Quando vazia, a mensagem padrão é mostrada.
- **sync** *(bool)* : O parâmetro "sync" determina se o cadastro remoto será síncrono ou assíncrono. Quando o cadastro requerido ao dispositivo for síncrono, o retorno virá como resposta e conterá o template da impressão digital ou a foto da face cadastrada em formato Base64. Já quando o cadastro requerido for assíncrono, o equipamento enviará o resultado na forma de uma nova requisição POST com a estrutura de parâmetros descrita abaixo (endpoints *card_create*, *fingerprint_create*, *face_create*, *pin_create* e *password_create*), o que torna necessário configurar um endpoint no seu servidor para recebê-la.
- **auto** *(bool)* : O parâmetro "auto" determina se o cadastro remoto da face será automático ou manual. Quando o cadastro requerido ao dispositivo for automático, não será necessário fazer uso dos botões do display para confirmar o cadastro da foto. Após a face ser detectada e quando estiver centralizada, uma contagem regressiva se iniciará e, ao se encerrar, a foto será automaticamente confirmada. Por padrão de fábrica, o valor de entrada deste parâmetro é "false".
- **countdown** *(int)* : O parâmetro "countdown" determina o tempo da contagem regressiva, em segundos, utilizada para fazer o cadastramento automático da foto. Por padrão de fábrica, o valor de entrada deste parâmetro é "5", o que corresponde a 5 segundos.

*Resposta*

- Quando o parâmetro "type" (acima) tiver o valor "biometry", o retorno será composto de ***success***, ***user_id***, ***device_id*** e, se o cadastro for bem-sucedido, ***finger_type*** e a lista ***fingerprints***, contendo ***width***, ***height*** e ***image*** da impressão digital de cada etapa do cadastro. Se o cadastro falhar, o retorno conterá o elemento ***error***.
- Quando o parâmetro "type" (acima) tiver o valor "face", o retorno será composto de ***success***, ***user_id***, ***device_id*** e ***user_image*** (se o cadastro for bem-sucedido) ou ***error*** (se o cadastro falhar). Além disso, caso o erro seja FACE_EXISTS — erro relacionado a uma tentativa de cadastro de face que coincide com a de outro usuário já cadastrado — é retornado o ID do usuário cadastrado correspondente. O ID é indicado através do parâmetro `match_user_id` e é retornado dentro da seguinte estrutura JSON: **info { match_user_id: valor }**. Caso o dispositivo esteja com a configuração **extract_face_template** do módulo **online_client** em 1, **user_image** será substituído por **face_template**, que contém o template facial do usuário.
- Quando o parâmetro "type" (acima) tiver o valor "card", o retorno será composto de ***success***, ***user_id***, ***device_id***, ***card_value*** e ***num_bits*** (se o cadastro for bem-sucedido) ou ***error*** (se o cadastro falhar). O campo ***num_bits*** indica o número de bits lidos do cartão durante o cadastro remoto e é usado internamente para inferir o `wiegand_format_id` correto (ver objeto [***cards***](../objetos/lista-de-objetos.md#cards)) antes de salvar o cartão via `create_objects`. Retornado apenas pelos **equipamentos da linha iDAccess** e pela versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.
- Quando o parâmetro "type" (acima) tiver o valor "pin", o retorno será composto de ***success***, ***user_id***, ***device_id*** e ***pin_value*** (se o cadastro for bem-sucedido) ou ***error*** (se o cadastro falhar).
- Quando o parâmetro "type" (acima) tiver o valor "password", o retorno será composto de ***success***, ***user_id***, ***device_id*** e ***password_value*** (se o cadastro for bem-sucedido) ou ***error*** (se o cadastro falhar).
- Campos adicionais que podem ser retornados no cadastro síncrono ("sync" igual a "true"):
  - **info** *(objeto JSON)*: Retornado com o erro FACE_EXISTS. Além de **match_user_id** *(int64)*, contém **match_confidence** *(int)*, a pontuação de semelhança entre a face capturada e a face já cadastrada. Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.
  - **site_code_size** *(int)*: Retornado quando "type" é "card". Contém o tamanho configurado do *site code* do cartão.
  - **wiegand_format_id** *(int)*: Retornado quando "type" é "card", se o equipamento identificar o formato Wiegand do cartão lido. Contém o id do formato Wiegand (ver objeto [***cards***](../objetos/lista-de-objetos.md#cards)). Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.
  - **template** *(string)*: Retornado quando "type" é "biometry", no lugar da lista ***fingerprints***, quando o equipamento não fornece as imagens das etapas do cadastro. Contém o template da impressão digital em Base64.
  - **template_size** *(int)*: Retornado junto com ***template***. Contém o tamanho do template, em bytes. Disponível apenas em **equipamentos da linha iDAccess**.
  - **tag_value** *(string)*: Retornado quando "type" é "tag". Contém o valor da tag lida. Disponível apenas em **equipamentos da linha iDAccess**.
  - **error_code** *(int)*: Retornado junto com ***error*** quando o equipamento opera como unidade secundária de uma catraca. Contém o código numérico do erro. Disponível apenas em **equipamentos da linha iDAccess**.

#### Recomendações de uso

- Para realizar o cadastramento remoto de cartão, face, impressão digital, PIN ou senha de forma assíncrona ("sync" é enviado como "false"), o [**monitor**](../monitor/introducao-ao-monitor.md) precisará estar previamente configurado no dispositivo e os correspondentes endpoints, implementados no servidor.
- Quando o cadastro remoto é requerido na modalidade assíncrona com "save" sendo "true", o salvamento da face cadastrada não gera retorno dos dados de imagem para o servidor remoto. Neste caso, a obtenção da imagem cadastrada pode ser feita através da API [**Cadastro facial**](../reconhecimento-facial/cadastro-facial.md), conforme descrito na seção **Obter foto do usuário**. Devido à ausência de retorno direto de resultado, não é recomendado realizar o cadastro remoto com esta configuração.
- Nos equipamentos de reconhecimento facial, ao se realizar o cadastro remoto assíncrono, é recomendável esperar os dados de cadastro através da resposta enviada ao [**monitor**](../monitor/introducao-ao-monitor.md), no endpoint ***/api/notifications/user_image*** ou ***/api/notifications/face_template***. Ainda que o endpoint ***/face_create.fcgi*** sirva ao mesmo propósito, seu uso é restrito a quando o equipamento está operando no modo online, o que pode gerar confusão na obtenção dos dados.

#### Exemplo de requisição

Inicia o cadastro remoto de biometria:

```
$.ajax({
    url: "/remote_enroll.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        type:"biometry",
        user_id:123,
        message:"Teste",
        save:true
    })
});
```

Inicia o cadastro remoto síncrono de face:

```
$.ajax({
    url: "/remote_enroll.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        type:"face",
        user_id:123,
        save: true,
        sync: true
    })
});
```

Inicia o cadastro remoto síncrono de face, com cadastramento automático da foto com contagem regressiva de 3 segundos:

```
$.ajax({
    url: "/remote_enroll.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        type:"face",
        user_id:123,
        save: true,
        sync: true,
        auto: true,
        countdown: 3
    })
});
```

### POST /cancel_remote_enroll.fcgi

**Cancelar cadastro remoto em andamento:** Este comando pode ser utilizado a qualquer momento em que for necessário interromper o processo de cadastro descrito acima (remote_enroll). O método HTTP usado é o POST.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Cancela o cadastro remoto em andamento:

```
$.ajax({
    url: "/cancel_remote_enroll.fcgi?session=" + session,
    type: 'POST', contentType: 'application/json'
});
```

### POST /card_create.fcgi

Quando ocorrer o cadastro remoto de cartão em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md), o resultado virá por este endpoint. O método HTTP usado é o POST e o contentType é application/json.

*Parâmetros*

- **user_id** *(int64)* : Esse número representa o id do usuário.
- **card_value** *(int64)* : Número do cartão.
- **device_id** *(int64)* : Esse número representa o id do equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Exemplo de Mensagem:

```
{
    "user_id": 1,
    "card_value": 132456789,
    "device_id": 935107
}
```

### POST /fingerprint_create.fcgi

Quando ocorrer o cadastro remoto de biometria em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md), o resultado virá por este endpoint e deverá conter as imagens (fotos) do dedo capturadas pelo equipamento. O método HTTP usado é o POST e o contentType é application/json.

*Parâmetros*

- **user_id** *(int64)* : Esse número representa o id do usuário.
- **finger_type** *(int)* : 1 se for dedo de pânico ou 0 caso contrário.
- **device_id** *(int64)* : Esse número representa o id do equipamento em que está ocorrendo o cadastro remoto de biometria.
- **fingerprints** *(array de objetos JSON)* : Lista com as imagens de impressões digitais capturadas.
  > - **image** *(string)* : É o binário da imagem convertido para Base64. Este binário é um bitmap monocromático (escala de cinza) em que cada byte representa um pixel. Portanto, o campo **image** deve conter width\*height bytes.
  > - **width** *(int)* : Largura da imagem.
  > - **height** *(int)* : Altura da imagem.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Exemplo de Mensagem:

```
{
    "user_id": 1,
    "finger_type": 0,
    "device_id": 935107,
    "fingerprints": [
        {
            "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAY ... QK5JP3FQw2eE1oQf/9k=",
            "width": 300,
            "height": 200
        },
        {
            "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAo ... D1odQIroECBAgQIJ/9k=",
            "width": 300,
            "height": 200
        },
        {
            "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAE ... 8kP5Tl+u4uesqtj/2Q==",
            "width": 300,
            "height": 200
        }
    ]
}
```

Nota: Caso o equipamento esteja configurado para extrair templates, o endpoint chamado será template_create.fcgi, que contém os templates extraídos pelo equipamento.

### POST /face_create.fcgi

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Quando ocorrer o cadastro remoto de face em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md), o resultado virá por este endpoint e deverá conter a imagem da face capturada pelo equipamento. O método HTTP usado é o POST e o contentType é application/json.

*Parâmetros*

- **user_id** *(int64)* : Esse número representa o id do usuário.
- **device_id** *(int64)* : Esse número representa o id do equipamento em que está ocorrendo o cadastro remoto de face.
- **face** *(objeto JSON)* : Objeto com os dados da foto cadastrada.
  > - **image** *(string)* : É o binário da imagem convertido para Base64.
  > - **width** *(int)* : Largura da imagem.
  > - **height** *(int)* : Altura da imagem.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Exemplo de Mensagem:

```
{
    "user_id": 1,
    "device_id": 935107,
    "face": {
        "image": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAY ... QK5JP3FQw2eE1oQf/9k=",
        "width": 300,
        "height": 300
    }
}
```

### POST /pin_create.fcgi

Quando ocorrer o cadastro remoto de PIN em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md), o resultado virá por este endpoint. O método HTTP usado é o POST e o contentType é application/json.

*Parâmetros*

- **user_id** *(int64)* : Esse número representa o id do usuário.
- **pin_value** *(string)* : Valor do PIN.
- **device_id** *(int64)* : Esse número representa o id do equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Exemplo de Mensagem:

```
{
    "user_id": 1,
    "pin_value": "1324",
    "device_id": 935107
}
```

### POST /password_create.fcgi

Quando ocorrer o cadastro remoto de senha em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md), o resultado virá por este endpoint. O método HTTP usado é o POST e o contentType é application/json.

*Parâmetros*

- **user_id** *(int64)* : Esse número representa o id do usuário.
- **password_value** *(string)* : Valor da senha.
- **device_id** *(int64)* : Esse número representa o id do equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Exemplo de Mensagem:

```
{
    "user_id": 1,
    "password_value": "1324",
    "device_id": 935107
}
```
