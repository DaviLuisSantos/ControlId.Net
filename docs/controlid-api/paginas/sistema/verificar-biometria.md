<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/verificar-biometria/ -->

# Verificar Biometria

Verifica se uma biometria é valida ou inválida. O método HTTP usado é o **POST**.

### POST /is_valid_biometry.fcgi

*Parâmetros*

- **biometry** *(string)* : Biometria a ser verificada.

*Resposta*

- **isValid** *(bool)* : Representa se a biometria é valida (true) ou inválida (false).

#### Exemplo de requisição

```
$.ajax({
    url: "/is_valid_biometry.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        biometry: "SUNSUzIxAAABRgEBAAAAAMUAxQAEASwBAAAAgRgFzwCLAI0PqgDJAHAP4QDTAJcP2ADeACIPtgDgAHQPSx8r+zNvPf8jURvaRXReJ3MqPYzesfO1uQcKVQLfESAXAQDVCYwFANNcDz4IALBhA/tdRgYAyWEMNcEKAJ5oAClUPgsAkHP9MP/+wMBCBgCRgPD8/v/+BQCXhf3/MgUA044WwDYMAIqS9zM8/0EMAIqe9P4z/zVBDACRpfr/MTJADACPsff//MAp/1UIAIXE5Pv/J8EDAKXJdMMMAIjN5/37/z7/NQQAstyDxsIIALrhEP3+/8FXDACL4uL9//r//MD+wGQFALLidMOqBQDd4iD/TAkQoA3M+vcewsELEJgRwPz7+/v8//2JBxCvG6348PrAwkRCAQEAAAAWAAAAAAIFAAAAAAAARUI=",
    })
});
```

#### Exemplo de resposta

```
{
    "isValid": true
}
```
