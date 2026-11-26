<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/personalizar-mensagem-eventos/ -->

# Personalizar Mensagens de Eventos

Este tópico descreve como personalizar as mensagens exibidas no display do dispositivo após a ocorrência dos eventos de identificação. Os eventos são:

- **Acesso autorizado**
- **Acesso não autorizado**
- **Usuário não identificado**
- **Use máscara**

Por padrão, alguns desses eventos não exibem nenhuma mensagem. No entanto, é possível configurar o dispositivo para exibir mensagens personalizadas para cada um desses eventos, conforme a necessidade.

**Observação:** Essa funcionalidade está disponível apenas nos equipamentos iDFace e iDFace Max, considerando as últimas versões de firmware disponíveis.

### POST /set_configuration.fcgi

*Parâmetros*

- Para personalizar as mensagens de identificação, os seguintes parâmetros são necessários:

  | Campo | Tipo | Descrição |
  | --- | --- | --- |
  | **custom_auth_message** | *string* | Define a mensagem personalizada para quando o acesso é autorizado. |
  | **custom_deny_message** | *string* | Define a mensagem personalizada para quando o acesso não é autorizado. |
  | **custom_not_identified_message** | *string* | Define a mensagem personalizada para quando o usuário não é identificado. |
  | **custom_mask_message** | *string* | Define a mensagem personalizada para o uso de máscara. |
  | **enable_custom_auth_message** | *int* | Habilita (1) ou Desabilita (0) a mensagem personalizada de acesso autorizado. |
  | **enable_custom_deny_message** | *int* | Habilita (1) ou Desabilita (0) a mensagem personalizada de acesso não autorizado. |
  | **enable_custom_not_identified_message** | *int* | Habilita (1) ou Desabilita (0) a mensagem personalizada quando o usuário não é identificado. |
  | **enable_custom_mask_message** | *int* | Habilita (1) ou Desabilita (0) a mensagem personalizada para uso de máscara. |

**Observação:** O conteúdo das mensagens personalizadas pode ser cortado conforme o tamanho da tela do dispositivo.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de Requisição:

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "identifier": {
                "custom_auth_message": "Seja bem-vindo",
                "custom_deny_message": "Acesso negado",
                "custom_not_identified_message": "Usuário não reconhecido",
                "custom_mask_message": "Por favor, use máscara",
                "enable_custom_auth_message": "1",
                "enable_custom_deny_message": "1",
                "enable_custom_not_identified_message": "1",
                "enable_custom_mask_message": "1"
            }
        }
    )
});
```
