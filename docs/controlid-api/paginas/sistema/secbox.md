<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/secbox/ -->

# Informações e Firmware do SecBox

Os endpoints a seguir permitem verificar a presença e o status do módulo de segurança de hardware SecBox, obter seu número de série e gerenciar seu firmware.

## Verificar se o equipamento possui SecBox

Retorna se o equipamento possui um módulo SecBox.

Disponível apenas em **equipamentos da linha iDAccess**.

### GET /device_has_secbox.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **has_secbox** *(bool)*: `true` se o equipamento possui um módulo SecBox.

#### Exemplo de requisição

```
$.ajax({
    url: "/device_has_secbox.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "has_secbox": true
}
```

## Verificar se o SecBox está ativo

Retorna se um módulo SecBox está atualmente detectado e ativo no equipamento.

### GET /secbox_is_active.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **isActive** *(bool)*: `true` se um SecBox está detectado e ativo.

#### Exemplo de requisição

```
$.ajax({
    url: "/secbox_is_active.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "isActive": true
}
```

## Obter número de série do SecBox

Retorna o número de série do módulo SecBox conectado.

### GET /secbox_serial_number.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **serial** *(string)*: Número de série do SecBox. Retorna `"0"` se nenhum SecBox estiver conectado.

#### Exemplo de requisição

```
$.ajax({
    url: "/secbox_serial_number.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "serial": "0A1B2C3D4E5F"
}
```

## Obter versão do firmware do SecBox

Retorna a versão atual do firmware do módulo SecBox.

### GET /update_secbox_firmware_version.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **fw_version** *(int)*: Número da versão atual do firmware do SecBox.

#### Exemplo de requisição

```
$.ajax({
    url: "/update_secbox_firmware_version.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "fw_version": 3
}
```

## Atualizar firmware do SecBox

Aciona a atualização de firmware do módulo SecBox. Esta é uma chamada síncrona — aguarda a conclusão do processo antes de retornar.

### POST /update_secbox_firmware.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **isUpdated** *(bool)*: `true` se a atualização de firmware foi aplicada com sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/update_secbox_firmware.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "isUpdated": true
}
```

## Obter status da atualização de firmware do SecBox

Retorna o progresso atual de uma atualização de firmware do SecBox em andamento.

### GET /update_secbox_firmware_status.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **isFinished** *(bool)*: `true` se o processo de atualização de firmware foi concluído.
- **bitsWritten** *(int)*: Número de bits gravados no SecBox durante a atualização.

#### Exemplo de requisição

```
$.ajax({
    url: "/update_secbox_firmware_status.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "isFinished": false,
    "bitsWritten": 4096
}
```
