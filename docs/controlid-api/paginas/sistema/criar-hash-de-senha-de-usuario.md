<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/criar-hash-de-senha-de-usuario/ -->

# Criar *hash* de senha de usuário

Dada uma senha em texto simples, retorna um ***hash*** que representa uma senha de usuário e o ***salt*** que foi usado para gerá-lo. Note que ao se usar esse comando duas vezes para a mesma senha em texto simples, o Hash resultante não será o mesmo, pois o salt gerado é aleatório e será diferente a cada chamada. O método HTTP usado é o POST.

### POST /user_hash_password.fcgi

*Parâmetros*

- **password** *(string)* : Senha do usuário em texto simples para a qual se quer gerar o Hash.

*Resposta*

- **password** *(string)* : Contém o ***hash*** da senha fornecida.
- **salt** *(string)* : Contém o ***salt*** utilizado para gerar o ***hash*** da senha.

#### Exemplo de requisição

Gerando um Hash para senha 'abc123':

```
$.ajax({
    url: "/user_hash_password.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "abc123",
    })
});
```

#### Exemplo de resposta

```
{"password":"ab5d13cea3c4c20c954711c7006004f83c5e86486cc1736b231881cf63c08f19","salt":"y2a2B`Lt.A]8iI|\\M]Ao:?\\>FT-L](Xv"}
```
