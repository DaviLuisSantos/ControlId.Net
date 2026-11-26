<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/update-idflex-idaccess-nano/ -->

# Upgrade iDFlex e iDAccess Nano

O controlador de Acesso iDFlex, pode ser vendido nas versões Lite e Pro. A versão **Lite** não tem a interface de rede habilitada, portanto, não será possível sequer utilizar o servidor web embarcado, a versão **Pro**, possui a interface de rede ativa mas não é possível habilitar no modo enterprise (online).

O iDAccess Nano, na única versão vendida também não é possível habilitar o modo enterprise sem antes realizar o upgrade descrito a seguir.

Portanto, para ambos os dispositivos descritos acima, é necessário realizar o upgrade para a Versão Enterprise para depois habilitar o Modo Enterprise (online), e o *endpoint* abaixo permite que isso seja realizado remotamente. O método HTTP usado é o POST.

### POST /idflex_upgrade_enterprise.fcgi

*Parâmetros*

- **password** *(string)* : Recebe uma senha que deve ser fornecida pela Control iD para habilitar o modo enterprise no equipamento.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Realiza o upgrade do iDFlex para a versão enterprise:

```
$.ajax({
    url: "/idflex_upgrade_enterprise.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password:
    })
});
```

## Upgrade de capacidade de templates de digital

Aumenta a capacidade de armazenamento de templates de digital para 10.000 templates nos equipamentos iDFlex, iDAccessH2, iDFitH2, iDAccess Pro e iDAccess Nano. Requer uma senha de licença fornecida pela Control iD.

### POST /upgrade_ten_thousand_templates.fcgi

*Parâmetros*

- **password** *(string)*: Senha de licença fornecida pela Control iD.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/upgrade_ten_thousand_templates.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "ABCDE12345"
    })
});
```
