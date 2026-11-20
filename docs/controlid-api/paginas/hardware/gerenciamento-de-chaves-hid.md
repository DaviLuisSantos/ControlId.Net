<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/hardware/gerenciamento-de-chaves-hid/ -->

# Gerenciamento de Chaves do Leitor HID NEW!

O módulo OMNIKEY Reader Core (OKRC) armazena suas chaves criptográficas em **referências de chave**
internas (também chamadas de slots de chave). Os endpoints a seguir injetam essas chaves, consultam
o que um slot contém e leem ou alteram os parâmetros de configuração (OIDs) do próprio módulo.

Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Estas operações seguem a hierarquia de chaves definida no *OMNIKEY SE Reader Core User Guide*
(PLT-06682, seção 10.32). Todos os campos de chave, referência de chave e valor de OID são
**strings hexadecimais**.

## Hierarquia de chaves

As chaves são injetadas em cadeia, cada uma envolvida (*wrapped*) pela chave do nível acima:

| Nível | Endpoint | Finalidade |
| --- | --- | --- |
| KEK padrão | [hid_inject_default_kek](../hardware/gerenciamento-de-chaves-hid.md#post-hid_inject_default_kekfcgi) | Substitui a Key Encryption Key de fábrica do leitor. Deve ser a primeira operação — a KEK de fábrica é obtida junto à HID Global. |
| KEK intermediária | [hid_inject_custom_kek](../hardware/gerenciamento-de-chaves-hid.md#post-hid_inject_custom_kekfcgi) | Injeta uma KEK intermediária em uma referência de chave, envolvida por uma KEK já existente. |
| Chave de mídia | [hid_inject_media_key](../hardware/gerenciamento-de-chaves-hid.md#post-hid_inject_media_keyfcgi) | Injeta a chave final do cartão (por exemplo, uma chave de setor MIFARE), envolvida por uma KEK já existente. |

## Modo plain e modo wrapped

Todos os endpoints de injeção aceitam a chave em um de dois modos, selecionado pela **presença** do
campo **kek** no corpo da requisição — não existe um parâmetro de modo separado:

| Modo | Como selecionar | Significado |
| --- | --- | --- |
| **Plain** | Envie **kek** | **key** e **kek** são enviadas em texto plano. O equipamento envolve a chave internamente antes de injetá-la. |
| **Wrapped** | Omita **kek** | **key** já vem envolvida pelo cliente, via NIST SP 800-38F KWP. |

O modo plain é mais simples de usar; o modo wrapped evita transmitir a KEK ao equipamento.

## Alterar a KEK padrão

Substitui a Key Encryption Key de fábrica do leitor. Este é o ponto de entrada de toda a hierarquia
de chaves: até que esta operação seja concluída com sucesso, nenhuma outra chave pode ser injetada.

### POST /hid_inject_default_kek.fcgi

*Parâmetros*

- **key** *(string)*: **Obrigatório.** A nova KEK, como string hexadecimal. No modo plain é a
  própria chave; no modo wrapped é a chave já envolvida por KWP.
- **kek** *(string)*: Opcional. A KEK atual, em texto plano. Enviá-la seleciona o modo plain;
  omiti-la seleciona o modo wrapped.
- **key_id** *(string)*: Opcional. Um identificador de 8 bytes, escolhido livremente pelo cliente,
  armazenado junto à chave para que ela possa ser reconhecida depois — o
  [hid_get_key_info](../hardware/gerenciamento-de-chaves-hid.md#post-hid_get_key_infofcgi) devolve esse mesmo valor. Aceito **apenas** neste
  endpoint (ver a observação abaixo).

*Resposta*

- **success** *(bool)*: `true` quando a chave foi injetada.

Se o módulo recusar a injeção, a chamada retorna o erro informado pelo leitor, ou
"Key injection failed" quando o leitor não informa o motivo.

#### Exemplo de requisição

Modo plain, substituindo a KEK de fábrica e marcando-a com um identificador:

```
$.ajax({
    url: "/hid_inject_default_kek.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        key: "1111111111111111111111111111111111111111111111111111111111111111",
        kek: "65547522234A0B72CE20D458D91C1C2514362085C3C79A13641575F07CD4E627",
        key_id: "4445464B45593031"
    })
});
```

#### Exemplo de resposta

```
{
    "success": true
}
```

## Injetar uma KEK intermediária

Injeta uma KEK em uma referência de chave de destino, envolvida por uma KEK já presente em outra
referência.

### POST /hid_inject_custom_kek.fcgi

*Parâmetros*

- **target_ref** *(string)*: **Obrigatório.** Referência de chave que receberá a chave, como string
  hexadecimal. Exemplo: `"03F001"`.
- **kek_ref** *(string)*: **Obrigatório.** Referência de chave que contém a KEK usada para envolver
  a nova chave. Exemplo: `"03F000"`.
- **key** *(string)*: **Obrigatório.** A chave a injetar, como string hexadecimal.
- **kek** *(string)*: Opcional. Seleciona o modo plain, conforme descrito acima.

*Resposta*

- **success** *(bool)*: `true` quando a chave foi injetada.

#### Exemplo de requisição

```
$.ajax({
    url: "/hid_inject_custom_kek.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        target_ref: "03F001",
        kek_ref: "03F000",
        key: "3333333333333333333333333333333333333333333333333333333333333333",
        kek: "1111111111111111111111111111111111111111111111111111111111111111"
    })
});
```

## Injetar uma chave de mídia

Injeta a chave final do cartão — a chave efetivamente usada para ler um smartcard, como uma chave de
setor MIFARE Classic. Recebe os mesmos parâmetros do
[hid_inject_custom_kek](../hardware/gerenciamento-de-chaves-hid.md#post-hid_inject_custom_kekfcgi).

### POST /hid_inject_media_key.fcgi

*Parâmetros*

- **target_ref** *(string)*: **Obrigatório.** Referência de chave que receberá a chave. Exemplo:
  `"030101"`.
- **kek_ref** *(string)*: **Obrigatório.** Referência de chave que contém a KEK usada para envolver
  a chave.
- **key** *(string)*: **Obrigatório.** A chave do cartão, como string hexadecimal. Uma chave MIFARE
  Classic tem 6 bytes, por exemplo `"FFFFFFFFFFFF"`.
- **kek** *(string)*: Opcional. Seleciona o modo plain, conforme descrito acima.

*Resposta*

- **success** *(bool)*: `true` quando a chave foi injetada.

#### Exemplo de requisição

```
$.ajax({
    url: "/hid_inject_media_key.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        target_ref: "030101",
        kek_ref: "03F001",
        key: "FFFFFFFFFFFF",
        kek: "3333333333333333333333333333333333333333333333333333333333333333"
    })
});
```

## Consultar informações de uma chave

Informa o que uma referência de chave contém no momento. Útil para confirmar que uma injeção teve
sucesso, já que as chaves em si nunca podem ser lidas de volta.

### POST /hid_get_key_info.fcgi

*Parâmetros*

- **target_ref** *(string)*: **Obrigatório.** Referência de chave a consultar, como string
  hexadecimal.

*Resposta*

- **key_type** *(string)*: Tipo de chave armazenada na referência. Valores: `"KEK"` (uma chave de
  criptografia de chaves) ou `"MEDIA"` (uma chave de cartão).
- **key_id** *(string)*: O identificador armazenado junto à chave, ou string vazia quando a chave
  não possui identificador.

Se a referência não contiver chave alguma, a chamada retorna o erro informado pelo leitor, ou
"Key not found".

**Observação sobre key_id:** apenas o
[hid_inject_default_kek](../hardware/gerenciamento-de-chaves-hid.md#post-hid_inject_default_kekfcgi) aceita **key_id**, porque somente a
operação de KEK padrão subjacente expõe esse parâmetro. Portanto, chaves injetadas por
`hid_inject_custom_kek` ou `hid_inject_media_key` sempre retornam **key_id** vazio aqui, mesmo
imediatamente após uma injeção bem-sucedida.

#### Exemplo de requisição

```
$.ajax({
    url: "/hid_get_key_info.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ target_ref: "03F000" })
});
```

#### Exemplo de resposta

```
{
    "key_type": "KEK",
    "key_id": "4445464B45593031"
}
```

## Parâmetros de configuração do leitor (OIDs)

Os dois endpoints a seguir leem e alteram qualquer parâmetro de configuração do OMNIKEY diretamente,
endereçado pelo seu **OID** em notação com pontos — sempre seis partes, por exemplo
`"1.1.1.2.3.5"`.

Estes são os parâmetros internos do próprio módulo, distintos das configurações **hid_reader** de
nível de equipamento descritas em
[Parâmetros de Configuração](../configuracoes/parametros-configuracao.md#hid).

### POST /hid_set_oid.fcgi

O tipo do valor é **inferido a partir do tipo JSON** do campo **value** — não existe um parâmetro de
tipo separado.

*Parâmetros*

- **oid** *(string)*: **Obrigatório.** OID do parâmetro, em notação com pontos.
- **value** *(bool, int ou string)*: **Obrigatório.** O valor a escrever. Um booleano JSON é escrito
  como booleano, um número JSON como inteiro, e uma string JSON é tratada como sequência de bytes em
  hexadecimal. Qualquer outro tipo JSON retorna o erro "Invalid value: must be a bool, an int or a
  hex string".

*Resposta*

- **success** *(bool)*: `true` quando o parâmetro foi escrito.

#### Exemplo de requisição

Escreve uma sequência de bytes (a definição de um slot CPE):

```
$.ajax({
    url: "/hid_set_oid.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        oid: "1.1.1.2.3.1",
        value: "A261300161318244A1613002581CA361300161314E"
    })
});
```

### POST /hid_get_oid.fcgi

*Parâmetros*

- **oid** *(string)*: **Obrigatório.** OID do parâmetro, em notação com pontos.

*Resposta*

- **value** *(bool, int ou string)*: Valor atual do parâmetro. Como o leitor devolve bytes crus sem
  informação de tipo, o tipo é inferido pela quantidade de bytes: 1 byte é retornado como booleano,
  4 bytes como inteiro, e qualquer outro tamanho como string hexadecimal.

#### Exemplo de requisição

```
$.ajax({
    url: "/hid_get_oid.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ oid: "1.1.1.2.3.1" })
});
```

#### Exemplo de resposta

```
{
    "value": "A261300161318244A1613002581CA361300161314E"
}
```

## Redefinir as configurações do leitor

Inicia — ou consulta o andamento de — um reset das configurações do módulo OMNIKEY, opcionalmente
restaurando um arquivo de configuração (`config.orco`) enviado no próprio corpo da requisição.

### POST /hid_reset_config.fcgi

*Parâmetros*

- **start** *(bool)*: Inicia o reset das configurações do leitor para os valores de fábrica. Se a chamada for feita sem **start**, apenas o andamento do reset em curso é consultado.

Para restaurar um arquivo `.orco` customizado em vez de fazer o reset de fábrica, envie o conteúdo
binário do arquivo no corpo da requisição, com `Content-Type: application/octet-stream` (sem
envelope JSON e sem o parâmetro **start**).

*Resposta*

- **resetting** *(bool)*: Indica se o reset ainda está em andamento.
- **status** *(string)*: Estado atual do reset. Valores: `"running"` ou `"done"`.

Se já houver um reset em andamento, uma nova chamada retorna o erro "HID reader config reset already
in progress".

#### Exemplo de requisição

Inicia o reset com a configuração de fábrica:

```
$.ajax({
    url: "/hid_reset_config.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ start: true })
});
```

#### Exemplo de resposta

Enquanto o reset está em andamento:

```
{
    "resetting": true,
    "status": "running"
}
```

Após a conclusão (na chamada seguinte, sem **start**):

```
{
    "resetting": false,
    "status": "done"
}
```
