<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/estado-do-enrollment/ -->

# Estado do Enrollment

Retorna o estado atual do módulo de cadastro do equipamento. Use este endpoint para monitorar o progresso de uma operação de cadastro remoto em andamento.

### GET /enroller_state.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **enroller_state** *(string)*: Estado atual do módulo de cadastro.

| Valor | Descrição |
| --- | --- |
| `"NORMAL_STATE"` | Ocioso, nenhum cadastro em andamento |
| `"ENROLL_CARD_STATE"` | Cadastrando cartão |
| `"ENROLL_BIOMETRY_STATE"` | Cadastrando biometria (digital) |
| `"WAITING_BIOMETRY_EXTRACT_REPLY"` | Aguardando resultado da extração biométrica |
| `"WAITING_BIOMETRY_MATCH_REPLY"` | Aguardando resultado da verificação biométrica |
| `"WAITING_BIOMETRY_ENROLL_REPLY"` | Aguardando resultado do cadastro biométrico |
| `"ENROLL_FACE_STATE"` | Cadastrando rosto |
| `"ENROLL_PIN_STATE"` | Cadastrando PIN |
| `"ENROLL_PASSWORD_STATE"` | Cadastrando senha |

- **biometry_state** *(string)*: Etapa atual dentro de uma sequência de cadastro biométrico.

| Valor | Descrição |
| --- | --- |
| `"NOT_ENROLLING"` | Nenhum cadastro biométrico em andamento |
| `"ENROLL_FIRST_BIOMETRY_STATE"` | Capturando primeira leitura |
| `"ENROLL_SECOND_BIOMETRY_STATE"` | Capturando segunda leitura |
| `"ENROLL_THIRD_BIOMETRY_STATE"` | Capturando terceira leitura |

- **last_enroll** *(bool)*: `true` se a última tentativa de cadastro foi bem-sucedida.
- **last_enroll_error** *(string)*: Código de erro da última tentativa de cadastro.

| Valor | Descrição |
| --- | --- |
| `"UNKNOWN"` | Erro desconhecido |
| `"USER_NONEXISTENT"` | Usuário alvo não existe |
| `"CARD_EXISTS"` | Cartão já cadastrado para outro usuário |
| `"DIFFERENT_FINGERPRINTS"` | Digitais capturadas não conferem |
| `"FACE_EXISTS"` | Rosto já cadastrado para outro usuário |
| `"PIN_EXISTS"` | PIN já cadastrado para outro usuário |
| `"TEMPLATE_EXISTS"` | Template já cadastrado |
| `"TEMPLATE_LIMIT_REACHED"` | Número máximo de templates atingido |
| `"REMOTE_TIMEOUT"` | Timeout no cadastro remoto |
| `"REMOTE_COULD_NOT_CONNECT"` | Não foi possível conectar ao servidor remoto |
| `"REMOTE_SERVER_ERROR"` | Servidor remoto retornou erro |
| `"TIMEOUT"` | Operação de cadastro expirou |
| `"CANCELLED"` | Cadastro foi cancelado |

#### Exemplo de requisição

```
$.ajax({
    url: "/enroller_state.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "enroller_state": "ENROLL_BIOMETRY_STATE",
    "biometry_state": "ENROLL_SECOND_BIOMETRY_STATE",
    "last_enroll": false,
    "last_enroll_error": "UNKNOWN"
}
```
