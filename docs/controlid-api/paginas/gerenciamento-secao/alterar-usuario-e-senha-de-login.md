<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/gerenciamento-secao/alterar-usuario-e-senha-de-login/ -->

# Alterar usuário e senha de login

Comando para alterar o usuário e/ou a senha utilizados para *login* no equipamento.

### POST /change_login.fcgi

*Parâmetros*

- **login** *(string)* : Nome do usuário a ser usado para o login no equipamento.
- **password** *(string)* : Senha do usuário a ser usada para login no equipamento. Deve ser em texto claro, sem *Hash*.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
  url: "/change_login.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
    data: JSON.stringify({
    login: "WalterWhite",
    password: "Heisenberg"
  })
});
```
