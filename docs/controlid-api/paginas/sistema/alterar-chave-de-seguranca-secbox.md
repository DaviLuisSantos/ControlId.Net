<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/alterar-chave-de-seguranca-secbox/ -->

# Alterar Chave de Segurança do SecBox

Os equipamentos de controle de acesso da Control iD podem incluir um módulo de segurança de hardware chamado **SecBox**, responsável por armazenar e gerenciar as chaves criptográficas utilizadas nos processos de controle de acesso.

Os endpoints a seguir permitem alterar ou redefinir a chave de segurança do SecBox.

## Definir chave como padrão

Reverte a chave de segurança do SecBox para o valor padrão de fábrica.

### POST /change_crypto_key_default.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **Status Key** *(string)*: Código de resultado da operação. Retorna `"00"` em caso de sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/change_crypto_key_default.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "Status Key": "00"
}
```

## Definir chave como aleatória

Altera a chave de segurança do SecBox para um valor gerado aleatoriamente.

### POST /change_crypto_key_random.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **Status Key** *(string)*: Código de resultado da operação. Retorna `"00"` em caso de sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/change_crypto_key_random.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "Status Key": "00"
}
```

## Redefinir chave de segurança

Executa um reset completo em nível de dispositivo do módulo de segurança de hardware SecBox. Diferente das operações de alteração de chave acima, este comando reinicializa todo o estado criptográfico do módulo SecBox — não apenas o valor da chave.

### POST /reset_crypto_key.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **Status Key** *(string)*: Código de resultado da operação. Retorna `"00"` em caso de sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/reset_crypto_key.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "Status Key": "00"
}
```
