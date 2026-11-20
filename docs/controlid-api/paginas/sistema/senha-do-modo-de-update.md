<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/senha-do-modo-de-update/ -->

# Senha do modo de update NEW!

Define, remove ou consulta a senha opcional do [modo de update](../sistema/reiniciar-em-modo-de-update.md). A senha protege a restauração de fábrica feita a partir do modo de update, que apaga os administradores do equipamento.

Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### POST /recovery_password.fcgi

Requer uma sessão de **administrador**. Com outro nível de acesso, a chamada retorna o erro "Invalid access level for command recovery_password".

A ação depende dos parâmetros enviados:

- Com **password**: define ou altera a senha.
- Sem **password** e com **enabled** igual a `false`: remove a senha.
- Sem nenhum dos dois: apenas consulta se há senha definida.

Se os dois parâmetros forem enviados, vale **password**.

*Parâmetros*

- **password** *(string)*: Opcional. Nova senha do modo de update. Deve ter exatamente 6 dígitos numéricos e não pode ser `001818`. Caso contrário, a chamada retorna o erro "Invalid recovery password (6 digits expected)".
- **enabled** *(bool)*: Opcional. Aceita apenas `false`, que remove a senha. Para habilitar a senha, use **password**. Qualquer outro valor retorna o erro "Invalid enabled (only false is accepted, use password to enable)". Remover quando não há senha definida não gera erro.

*Resposta*

- **enabled** *(bool)*: `true` se há senha do modo de update definida no equipamento, após a operação.

A senha nunca é devolvida pela API nem gravada no log de auditoria.

#### Exemplo de requisição

Definindo a senha:

```
$.ajax({
    url: "/recovery_password.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "482913"
    })
});
```

Removendo a senha:

```
$.ajax({
    url: "/recovery_password.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        enabled: false
    })
});
```

Consultando se há senha definida:

```
$.ajax({
    url: "/recovery_password.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```

#### Exemplo de resposta

```
{
    "enabled": true
}
```
