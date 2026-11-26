<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/hardware/modulo-rfid/ -->

# Módulo RFID

Retorna o tipo de módulo leitor RFID presente no equipamento.

### GET /device_rfid_module.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **module** *(string)*: Tipo do módulo RFID. Valores possíveis:

| Valor | Descrição |
| --- | --- |
| `"mifare"` | Leitor RFID compatível com MIFARE |
| `"ASK"` | Leitor RFID ASK (125 kHz padrão) |
| `"HID"` | Leitor HID (módulo OMNIKEY Reader Core) |

#### Exemplo de requisição

```
$.ajax({
    url: "/device_rfid_module.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "module": "mifare"
}
```

## Obter último cartão HID lido NEW!

Retorna os dados do último cartão apresentado ao leitor HID. Útil para diagnóstico e fluxos de cadastro de cartão.

Disponível nas versões **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /get_hid_last_card.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **csn** *(string)*: Número de série do cartão (Card Serial Number).
- **pac** *(string)*: Valor de credencial do cartão (PAC).
- **type** *(string)*: Identificador do tipo de cartão.
- **bits** *(int)*: Número de bits nos dados do cartão.
- **custom** *(string)*: String hexadecimal do bloco `custom_data[custom_use_index]` (ver os parâmetros **hid_reader.card_process** e **hid_reader.custom_use_index** em [Parâmetros de Configuração](../configuracoes/parametros-configuracao.md#hid)) do último cartão lido, ou string vazia se não houver dado custom disponível/configurado.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_hid_last_card.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "csn": "A1B2C3D4",
    "pac": "00123456",
    "type": "hid",
    "bits": 26,
    "custom": "0x1a2b3c4d"
}
```

## Obter leitura de cartão Wiegand

Retorna dados de diagnóstico da última leitura de cartão Wiegand. Útil para solução de problemas em leitores de cartão e verificação de compatibilidade de formato.

Disponível nas versões **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**, **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e **[V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

### GET /get_wiegand_card_read.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **card_read_value** *(string)*: Valor Wiegand bruto lido do cartão.
- **card_read_num_bits** *(int)*: Número de bits nos dados Wiegand.
- **facility_code** *(string)*: Facility code extraído dos dados do cartão.
- **card_number** *(string)*: Número do cartão extraído dos dados.
- **parities** *(int)*: Resultado da verificação de paridade.
- **last_read_time** *(string)*: Timestamp da última leitura de cartão.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_wiegand_card_read.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "card_read_value": "0A1B2C3D",
    "card_read_num_bits": 26,
    "facility_code": "10",
    "card_number": "12345",
    "parities": 0,
    "last_read_time": "2024-01-15 10:30:00"
}
```
