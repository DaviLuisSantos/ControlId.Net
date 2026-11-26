<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/gerenciamento-secao/verificar-validade-da-sessao/ -->

# Verificar validade da sessão

Comando para verificar a validade de uma sessão.

### POST /session_is_valid.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **session** *(string)* : Código da sessão iniciada.

#### Exemplo de requisição

```
$.ajax({
  url: "/session_is_valid.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "session_is_valid": true
}
```
