<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/alarme/introducao-ao-alarme/ -->

# Introdução ao *Alarme*

Alarmes são ferramentas importantes para dispositivos de controle de acesso. Esse tipo de ferramenta é capaz de sinalizar operações indevidas do usuário, tentativas de invasão e situações de perigo ao usuário. Grande parte das configurações de alarme encontram-se armazenadas em banco e, portanto, podem ser alteradas a partir do objeto [*alarm*](../configuracoes/parametros-configuracao.md#alarm).

## POST /alarm_status.fcgi

Comando para alterar e obter o *status* atual do *alarme*. Possibilita também a desativação do alarme sonoro do aparelho.

*Parâmetros*

- **stop** (*bool*): O valor padrão é **true**

*Resposta*

- **active** (*bool*): Indica se o alarme está ativo.
- **cause** (*int*): Indica a causa da ativação do alarme.
  - 0: Sem causa
  - 1: Zona de alarme 1
  - 2: Zona de alarme 2
  - 3: Zona de alarme 3
  - 4: Zona de alarme 4
  - 5: Zona de alarme 5
  - 6: Porta aberta
  - 7: Arrombamento de porta
  - 8: Dedo de pânico
  - 9: Violação do equipamento
  - 10: Cartão de pânico
  - 11 em diante: dependem da linha do equipamento, conforme a tabela abaixo.

| Valor | Equipamentos da linha iDAccess | Equipamentos da linha iDFace ([V6](../particularidade-dos-produtos/versoes-de-firmware.md)), iDFace Max ([V7](../particularidade-dos-produtos/versoes-de-firmware.md)) e iDFace e iDFace Max ([V8](../particularidade-dos-produtos/versoes-de-firmware.md)) |
| --- | --- | --- |
| 11 | Zona monitorada sem presença | Senha de pânico |
| 12 | Zona monitorada aberta | PIN de pânico |
| 13 | Senha de pânico | - |
| 14 | PIN de pânico | - |
| 15 | Violação de entrada supervisionada (a partir da versão V5.15.0) | - |

***É importante notar que, na linha iDAccess, as causas *11* e *12* (Zona de Alarme Monitorada) são exclusivas dos equipamentos *iDFit* e *iDAccess*, visto que ambos possuem relé interno de acionamento da fechadura (e pinos de detecção de porta) por serem equipamentos anteriores à Secbox, portanto, o suporte a Zona de Alarme Monitorada é um recurso disponível apenas para os dispositivos sem Secbox.***

### Exemplo de requisição de *status*

*Mensagem*:

- A requisição de status de alarme não possui parâmetros.

*Exemplo de Resposta*:

```
{
    "active": false,
    "cause": 0
}
```

### Exemplo de desativação do alarme

*Mensagem*:

```
{
  "stop": true
}
```

*Resposta*:

- Essa chamada não possui retorno.

## POST /set_configuration.fcgi

Comando para alterar os parâmetros do alarme. Possibilita a configuração de intervalos de tempo e ativação/desativação de aspectos do alarme.

*Parâmetros*

- **device_violation_enabled**: Habilita/desabilita o alarme de violação do dispositivo.
- **door_sensor_alarm_timeout_after_closure**: Tempo de duração que o alarme indicará "alarme de porta aberta" após o fechamento da porta ao invés de indicar "alarme de arrombamento" (em ms).
- **door_sensor_delay**: Tempo de atraso para a abertura da porta após a detecção de identificação (em ms).
- **door_sensor_enabled**: Habilita/desabilita alarme de sensor de porta.
- **forced_access_debounce**: Tempo de debounce em caso de acesso forçado (em ms).
- **forced_access_enabled**: Habilita/desabilita alarme de acesso forçado.
- **panic_card_enabled**: Habilita/desabilita alarme de cartão de pânico.
- **panic_finger_delay**: Tempo de atraso para sinalizar uma situação de coerção por meio do alarme do dedo de pânico.
- **panic_finger_enabled**: Habilita/desabilita o alarme de dedo de pânico.
- **panic_password_enabled**: Habilita/desabilita o alarme de senha de pãnico.
- **panic_pin_enabled**: Habilita/desabilita o alarme de pin de pânico.

*Mensagem*:

```
{
    "alarm": {
        device_violation_enabled: "0"
        door_sensor_alarm_timeout_after_closure: "0"
        door_sensor_delay: "5"
        door_sensor_enabled: "1"
        forced_access_debounce: "2"
        forced_access_enabled: "0"
        panic_card_enabled: "1"
        panic_finger_delay: "1"
        panic_finger_enabled: "1"
        panic_password_enabled: "1"
        panic_pin_enabled: "1"
    }
}
```

*Resposta*:

- A requisição não possui resposta.
