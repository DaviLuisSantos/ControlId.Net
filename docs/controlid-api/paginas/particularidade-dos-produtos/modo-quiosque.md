<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/modo-quiosque/ -->

# Modo Quiosque

O Modo Quiosque, disponível no iDFace Max, substitui a tela principal do dispositivo por uma interface personalizável, com cabeçalho, mensagens, cores, ícones e botões configuráveis. Os botões são gerenciados através do objeto [`kiosk_buttons`](../objetos/lista-de-objetos.md) e podem executar ações como abrir a lista de contatos da interfonia SIP, liberar a porta por PIN ou realizar a leitura de QR code.

## Configurações do Modo Quiosque

Configurações gerais do Modo Quiosque. Os parâmetros abaixo podem ser alterados através do comando `set_configuration.fcgi` e pertencem ao módulo `guiFaceID` em **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)** e ao módulo `guiFaceIDMax` em **equipamentos da linha iDFace e iDFace Max com firmware [V8](../particularidade-dos-produtos/versoes-de-firmware.md)**.

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **kiosk_mode_enabled** | *int* | Habilita / desabilita o Modo Quiosque. Valores: "0" para desabilitado e "1" para habilitado. |
| **kiosk_header_title** | *string* | Texto principal mostrado no cabeçalho da tela principal. |
| **kiosk_header_subtitle** | *string* | Texto secundário mostrado no cabeçalho da tela principal. |
| **kiosk_message_title** | *string* | Texto principal mostrado no meio da tela principal. |
| **kiosk_message_subtitle** | *string* | Texto secundário mostrado no meio da tela principal. |
| **kiosk_text_color** | *string* | Cor de todos os textos mostrados na tela principal. O valor deve ser uma cor hexadecimal com prefixo #. |
| **kiosk_device_name** | *string* | Texto com o nome do dispositivo mostrado na barra superior na tela principal. |
| **kiosk_general_icons_color** | *string* | Cor de todos os ícones mostrados na tela principal e telas derivadas. Valor deve ser uma cor hexadecimal com prefixo #. |

### Exemplo de requisição

Essa requisição configura os parâmetros do modo quiosque em **equipamentos da linha iDFace Max com firmware [V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

Nota: firmware V8

Em **equipamentos da linha iDFace e iDFace Max com firmware [V8](../particularidade-dos-produtos/versoes-de-firmware.md)**, substitua o módulo `"guiFaceID"` por `"guiFaceIDMax"` no exemplo abaixo. Com `"guiFaceID"`, a requisição não funciona nesses equipamentos.

```js
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST', 
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "guiFaceID": {
                "kiosk_mode_enabled": "1",
                "kiosk_header_title": "The Pike",
                "kiosk_header_subtitle": "227 Pane Road, Newington",
                "kiosk_message_title": "Hi",
                "kiosk_message_subtitle": "Detectando\nRosto ou QR code...",
                "kiosk_text_color": "#FFFFFF",
                "kiosk_device_name": "ACCESS",
                "kiosk_general_icons_color": "#000000"
            }
        }
    )
});
```

## Listar ícones do quiosque

Retorna a lista de arquivos de ícone atualmente armazenados no equipamento.

### GET /get_kiosk_icons_list.fcgi

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **icons** *(array)*: Lista de objetos de ícone. Cada objeto contém:
  - **filename** *(string)*: Nome do arquivo de ícone.
- **max_files** *(int)*: Número máximo de ícones permitidos no equipamento (sempre `4`).

#### Exemplo de requisição

```
$.ajax({
    url: "/get_kiosk_icons_list.fcgi?session=" + session,
    type: 'GET',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "icons": [
        { "filename": "menu1.png" },
        { "filename": "menu2.svg" }
    ],
    "max_files": 4
}
```

## Obter ícone do quiosque

Baixa um arquivo de ícone armazenado no equipamento.

### GET /get_kiosk_icon.fcgi

*Parâmetros*

- **filename** *(string)*: **Obrigatório.** Nome do arquivo de ícone a ser obtido. Passado como parâmetro na query string.

*Resposta*

- Arquivo de imagem em formato binário. O Content-Type é definido de acordo com a extensão do arquivo (`.png`, `.jpg`/`.jpeg` ou `.svg`).

#### Exemplo de requisição

```
$.ajax({
    url: "/get_kiosk_icon.fcgi?filename=menu1.png&session=" + session,
    type: 'GET'
});
```

## Enviar ícone do quiosque

Envia um arquivo de ícone para o equipamento. O corpo da requisição deve conter o conteúdo binário bruto da imagem. Retorna erro se o limite de 4 ícones já tiver sido atingido.

### POST /set_kiosk_icon.fcgi

*Parâmetros*

- **filename** *(string)*: **Obrigatório.** Nome a ser atribuído ao arquivo de ícone. Passado como parâmetro na query string. Formatos aceitos: `.png`, `.jpg`, `.jpeg`, `.svg`.

*Resposta*

- **success** *(bool)*: `true` se o arquivo foi salvo com sucesso.
- **filename** *(string)*: Nome do arquivo de ícone salvo.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_kiosk_icon.fcgi?filename=menu1.png&session=" + session,
    type: 'POST',
    contentType: 'application/octet-stream',
    data: [bytes do arquivo de imagem]
});
```

#### Exemplo de resposta

```
{
    "success": true,
    "filename": "menu1.png"
}
```

## Excluir ícone do quiosque

Exclui um arquivo de ícone do equipamento.

### POST /delete_kiosk_icon.fcgi

*Parâmetros*

- **filename** *(string)*: **Obrigatório.** Nome do arquivo de ícone a ser excluído. Passado como parâmetro na query string.

*Resposta*

- **success** *(bool)*: `true` se o arquivo foi excluído com sucesso.

#### Exemplo de requisição

```
$.ajax({
    url: "/delete_kiosk_icon.fcgi?filename=menu1.png&session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "success": true
}
```

## Bloqueio por tentativas de autenticação

No Modo Quiosque, é possível bloquear temporariamente a autenticação por PIN ou QR code após múltiplas falhas consecutivas. Esses parâmetros pertencem ao módulo `identifier` (veja [Parâmetros de configuração](../configuracoes/parametros-configuracao.md)).

| Campo | Tipo | Descrição |
| --- | --- | --- |
| **kiosk_block_enabled** | *int* | Habilita / desabilita o mecanismo de bloqueio PIN ou QR code no caso de múltiplas falhas de autenticação. Valores: "0" para desabilitado e "1" para habilitado. |
| **kiosk_max_attempts** | *int* | Número máximo de tentativas antes do bloqueio. |
| **kiosk_block_interval** | *int* | Duração do bloqueio em segundos. |

### Exemplo de requisição

Essa requisição habilita o bloqueio após 3 tentativas malsucedidas, com duração de 60 segundos.

```js
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "identifier": {
                "kiosk_block_enabled": "1",
                "kiosk_max_attempts": "3",
                "kiosk_block_interval": "60"
            }
        }
    )
});
```
