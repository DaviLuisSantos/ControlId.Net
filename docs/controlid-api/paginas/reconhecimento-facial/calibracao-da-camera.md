<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/calibracao-da-camera/ -->

# Calibração remota da câmera NEW!

Permite iniciar, cancelar e acompanhar pela API a calibração da câmera, o mesmo processo disponível no menu do equipamento. Durante a calibração, o equipamento exibe a tela de calibração e orienta a pessoa posicionada à sua frente.

Disponível apenas na versão **[V6](../particularidade-dos-produtos/versoes-de-firmware.md)**.

## Iniciar a calibração

### POST /remote_camera_calibration.fcgi

Abre a tela de calibração no equipamento e inicia a calibração. A tela é fechada ao fim do tempo limite, ou antes, se a calibração for [cancelada](../reconhecimento-facial/calibracao-da-camera.md#post-cancel_remote_camera_calibrationfcgi). Se a tela de calibração já estiver aberta, a calibração é reiniciada com o novo tempo limite.

A chamada retorna assim que o comando é enviado à tela, sem aguardar o resultado. Para acompanhar a calibração, use [camera_calibration_status](../reconhecimento-facial/calibracao-da-camera.md#post-camera_calibration_statusfcgi).

*Parâmetros*

- **timeout** *(int)*: Opcional. Tempo limite da calibração, em segundos. Valores entre `10` e `600`. Padrão: `60`. Fora desse intervalo, a chamada retorna o erro "Invalid timeout (10 to 600 seconds expected)".

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/remote_camera_calibration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        timeout: 120
    })
});
```

## Cancelar a calibração

### POST /cancel_remote_camera_calibration.fcgi

Interrompe a calibração iniciada pela API e fecha a tela de calibração. Não tem efeito se a tela de calibração não estiver aberta.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

```
$.ajax({
    url: "/cancel_remote_camera_calibration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

## Obter o estado da calibração

### POST /camera_calibration_status.fcgi

Informa se a câmera está calibrada e se há uma calibração em andamento.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **calibrated** *(bool)*: Indica se o equipamento possui uma calibração da câmera gravada.
- **discrepancy_offset** *(float)*: Valor de calibração gravado. Só é significativo quando **calibrated** é `true`.
- **running** *(bool)*: Indica se há uma calibração em andamento.

#### Exemplo de requisição

```
$.ajax({
    url: "/camera_calibration_status.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "calibrated": true,
    "discrepancy_offset": 0.0123,
    "running": false
}
```
