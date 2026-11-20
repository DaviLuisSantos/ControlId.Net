<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/update-idface-max/ -->

# Upgrade iDFace Max

O controlador de Acesso iDFace MAX é vendido por padrão em sua versão Lite. A **versão Lite** possui um limite de 10.000 biometrias faciais e conta com suporte a interfonia SIP por padrão. O cliente pode optar em adquirir a **licença Pro** para realizar o upgrade do equipamento, o que aumentará o limite de biometrias faciais para 20.000 (com foto de cadastro) ou para 50.000 e 100.000 (sem foto de cadastro), dependendo do modo adquirido.

Para realizar o upgrade, primeiro o cliente deve entrar em contato com a Control iD e efetivar a compra da licença. Após isso, com sua licença em mãos, o cliente pode optar por realizar o upgrade de três formas, dependendo do modo adquirido.

**Atenção**: a cada mudança de modo, o equipamento é reiniciado e todos os dados são permanentemente apagados.

## Modo Pro *20k*

Aumenta a capacidade de armazenamento de templates faciais para 20.000, com armazenamento da foto de cadastro.

Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /upgrade_twenty_thousand_face_templates.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo Pro no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFace Max para a versão Pro *20k*:

```
$.ajax({
    url: "/upgrade_twenty_thousand_face_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```

## Modo Pro *50k*

### Opções de limite:

- **20.000 biometrias faciais:** Com armazenamento da foto de cadastro.
- **50.000 biometrias faciais:** Sem armazenamento da foto de cadastro.

### Opções de Upgrade:

- GUI - Config. Gerais > Config. Gerais > Licenças > Configurar Modo Pro > Upgrade Modo Pro 50k
- Web - Configurações > Upgrade Modo Pro > Pro 50k
- API - Endpoint: `/upgrade_fifty_thousand_face_templates.fcgi`

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /upgrade_fifty_thousand_face_templates.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo Pro no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFace Max para a versão Pro *50k*:

```
$.ajax({
    url: "/upgrade_fifty_thousand_face_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```

## Modo Pro *100k*

### Opções de limite:

- **20.000 biometrias faciais:** Com armazenamento da foto de cadastro.
- **50.000 biometrias faciais:** Sem armazenamento da foto de cadastro.
- **100.000 biometrias faciais:** Sem armazenamento da foto de cadastro.

### Opções de Upgrade:

- GUI - Config. Gerais > Config. Gerais > Licenças > Configurar Modo Pro > Upgrade Modo Pro 100k
- Web - Configurações > Upgrade Modo Pro > Pro 100k
- API - Endpoint: `/upgrade_hundred_thousand_face_templates.fcgi`

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /upgrade_hundred_thousand_face_templates.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo Pro no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFace Max para a versão Pro *100k*:

```
$.ajax({
    url: "/upgrade_hundred_thousand_face_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```

## Alternar entre os modos Pro

Para alternar entre os modos Pro já licenciados (`"20k"`, `"50k"` ou `"100k"`) sem uma nova senha de licença, use [`POST /set_pro_operation_mode_and_reboot.fcgi`](../particularidade-dos-produtos/update-idface.md#alternar-entre-os-modos-pro).
