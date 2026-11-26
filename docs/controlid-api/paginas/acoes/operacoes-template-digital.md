<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/operacoes-template-digital/ -->

# Operações de Template de Digital

Os endpoints a seguir permitem extrair, selecionar e mesclar templates biométricos de digital a partir de imagens grayscale brutas. São operações de baixo nível utilizadas em fluxos de cadastro de digital.

Disponível nos **equipamentos da linha iDAccess** e nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

## Extrair template de digital

Extrai um template biométrico de digital de uma imagem grayscale bruta. As dimensões da imagem devem ser passadas como parâmetros na query string e o corpo deve conter exatamente `largura × altura` bytes brutos.

### POST /template_extract.fcgi

*Parâmetros*

- **width** *(int)*: **Obrigatório.** Largura da imagem em pixels. Passado como parâmetro na query string.
- **height** *(int)*: **Obrigatório.** Altura da imagem em pixels. Passado como parâmetro na query string.
- Dados brutos da imagem grayscale no corpo da requisição (`application/octet-stream`). Deve conter exatamente `width × height` bytes.

*Resposta*

- **quality** *(int)*: Pontuação de qualidade da imagem da digital.
- **template** *(string)*: Template biométrico codificado em Base64.

#### Exemplo de requisição

```
$.ajax({
    url: "/template_extract.fcgi?width=256&height=256&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes brutos da imagem grayscale]
});
```

#### Exemplo de resposta

```
{
    "quality": 87,
    "template": "templatecodificadoembase64..."
}
```

## Selecionar melhor template de digital

Seleciona o melhor template a partir de múltiplas capturas de digital. Utilizado tipicamente durante o cadastro para identificar a leitura mais representativa de um conjunto de capturas.

### POST /template_match.fcgi

*Parâmetros*

Passados como parâmetros na query string:

- **temp_num** *(int)*: Opcional. Número de templates a comparar. Padrão: `3`.
- **size0**, **size1**, ... *(int)*: **Obrigatório.** Tamanho em bytes de cada template no corpo da requisição (um parâmetro por template, indexado a partir de `0`).

Corpo (`application/octet-stream`): bytes brutos dos templates concatenados, na mesma ordem dos parâmetros `sizeN`. O tamanho total deve ser igual à soma de todos os valores `sizeN`.

*Resposta*

- **template** *(string)*: Template selecionado como melhor correspondência, codificado em Base64.

#### Exemplo de requisição

```
$.ajax({
    url: "/template_match.fcgi?temp_num=3&size0=512&size1=512&size2=512&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes brutos dos templates concatenados]
});
```

#### Exemplo de resposta

```
{
    "template": "templatecodificadoembase64..."
}
```

## Mesclar templates de digital

Combina múltiplas capturas de digital em um único template otimizado. Utilizado tipicamente como etapa final do cadastro após coletar múltiplas leituras.

### POST /template_merge.fcgi

*Parâmetros*

Passados como parâmetros na query string:

- **temp_num** *(int)*: Opcional. Número de templates a mesclar. Padrão: `3`.
- **size0**, **size1**, ... *(int)*: **Obrigatório.** Tamanho em bytes de cada template no corpo da requisição (um parâmetro por template, indexado a partir de `0`).

Corpo (`application/octet-stream`): bytes brutos dos templates concatenados, na mesma ordem dos parâmetros `sizeN`. O tamanho total deve ser igual à soma de todos os valores `sizeN`.

*Resposta*

- **template** *(string)*: Template mesclado codificado em Base64.

#### Exemplo de requisição

```
$.ajax({
    url: "/template_merge.fcgi?temp_num=3&size0=512&size1=512&size2=512&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes brutos dos templates concatenados]
});
```

#### Exemplo de resposta

```
{
    "template": "templatecodificadoembase64..."
}
```

## Validar template de digital

Valida se um template biométrico de digital é reconhecido como correspondência válida pelo motor biométrico do equipamento.

### POST /is_valid_biometry.fcgi

*Parâmetros*

- **biometry** *(string)*: Obrigatório. Template de digital codificado em Base64 para validação.

*Resposta*

- **isValid** *(bool)*: `true` se o template for válido e reconhecido, `false` caso contrário.

#### Exemplo de requisição

```
$.ajax({
    url: "/is_valid_biometry.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        biometry: "templatecodificadoembase64..."
    })
});
```

#### Exemplo de resposta

```
{
    "isValid": true
}
```

## Iniciar sincronização de templates

Desativa o motor de identificação biométrica por digital do equipamento para permitir atualizações em lote de templates com segurança. Chamar antes de realizar modificações em lote de templates de digital, depois chamar [Finalizar sincronização de templates](../acoes/operacoes-template-digital.md#finalizar-sincronizacao-de-templates) ao concluir.

Disponível apenas em **equipamentos da linha iDAccess**.

### POST /template_sync_init.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/template_sync_init.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Finalizar sincronização de templates

Reativa o motor de identificação biométrica por digital do equipamento e notifica o sistema sobre os templates atualizados. Chamar após concluir as modificações em lote iniciadas com [Iniciar sincronização de templates](../acoes/operacoes-template-digital.md#iniciar-sincronizacao-de-templates).

Disponível apenas em **equipamentos da linha iDAccess**.

### POST /template_sync_end.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/template_sync_end.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
