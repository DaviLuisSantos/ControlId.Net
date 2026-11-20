<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/update-idface/ -->

# Upgrade iDFace

O controlador de Acesso iDFace é vendido por padrão em sua versão Lite. A **versão Lite** possui um limite de 3000 biometrias faciais e não conta com suporte a interfonia SIP. O cliente pode optar em adquirir a **licença Pro** para realizar o upgrade do equipamento, o que aumentará o limite de biometrias faciais para 10.000, 15.000 ou 30.000, dependendo do modo, além de liberar a funcionalidade de interfonia SIP. A ativação das licenças para 15.000 e 30.000 faces exige a aquisição prévia da licença de 10.000.

Para realizar o upgrade, primeiro o cliente deve entrar em contato com a Control iD e efetivar a compra da licença. Após isso, com sua licença em mãos, o cliente pode optar por realizar o upgrade de três formas, dependendo do modo.

Para os upgrades do iDFace Max, consulte [Upgrade iDFace Max](../particularidade-dos-produtos/update-idface-max.md).

## Modo Pro *10k*

\**Necessário para aquisição dos modos subsequentes*

- GUI - Configurações > Config. Gerais > Licenças > Configurar Modo Pro > Upgrade Modo Pro 10k
- Web - Configurações > Upgrade Modo Pro > Pro 10k
- API - Endpoint: `/upgrade_ten_thousand_face_templates.fcgi`

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /upgrade_ten_thousand_face_templates.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo Pro no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFace para a versão Pro *10k*:

```
$.ajax({
    url: "/upgrade_ten_thousand_face_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```

## Modo Pro *15k*

\**Dependente da aquisição do modo 10k*

- GUI - Configurações > Config. Gerais > Licenças > Configurar Modo Pro > Upgrade Modo Pro 15k
- Web - Configurações > Upgrade Modo Pro > Pro 15k
- API - Endpoint: `/upgrade_fifteen_thousand_face_templates.fcgi`

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /upgrade_fifteen_thousand_face_templates.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo Pro no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFace para a versão Pro *15k*:

```
$.ajax({
    url: "/upgrade_fifteen_thousand_face_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```

## Modo Pro *30k*

\**Dependente da aquisição do modo 10k*

- GUI - Configurações > Config. Gerais > Licenças > Configurar Modo Pro > Upgrade Modo Pro 30k
- Web - Configurações > Upgrade Modo Pro > Pro 30k
- API - Endpoint: `/upgrade_thirty_thousand_face_templates.fcgi`

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /upgrade_thirty_thousand_face_templates.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo Pro no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFace para a versão Pro *30k*:

```
$.ajax({
    url: "/upgrade_thirty_thousand_face_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```

## Alternar entre os modos Pro

Define o modo de capacidade de templates faciais ativo e reinicia o equipamento. Permite alternar entre os modos Pro já licenciados sem uma nova senha de licença. Exige a licença Pro *10k* ativada.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /set_pro_operation_mode_and_reboot.fcgi

*Parâmetros*

- **pro_operation_mode** *(string)*: Obrigatório. Modo de capacidade desejado. Não pode exceder o limite licenciado do equipamento. Valores aceitos:
  - iDFace: `"10k"`, `"15k"` e `"30k"`.
  - iDFace Max: `"20k"`, `"50k"` e `"100k"`.

*Resposta*

- Esta chamada não possui retorno. O equipamento reinicia aproximadamente 1 segundo após o envio da resposta.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_pro_operation_mode_and_reboot.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        pro_operation_mode: "15k"
    })
});
```
