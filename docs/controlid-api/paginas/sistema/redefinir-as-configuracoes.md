<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/redefinir-as-configuracoes/ -->

# Redefinir as configurações

Redefine as configurações do sistema para os valores de fábrica. O equipamento volta para o estado em que saiu da fábrica e todos os dados do usuário são perdidos. O método HTTP usado é o **POST**.

### POST /reset_to_factory_default.fcgi NEW!

*Parâmetros*

- **keep_network_info** *(bool)*: Se este parâmetro for definido como true, as informações de rede serão mantidas.
- **keep_all_config** *(bool)*: Se definido como true, a configuração do equipamento é mantida e apenas os dados de usuário são apagados. Quando false (padrão), a configuração também volta aos valores de fábrica, incluindo as configurações de rede, o hostname, os arquivos de VPN e as credenciais da interface web (de volta para `admin`/`admin`).
- **keep_osdp_info** *(bool)*: Se definido como true, os parâmetros de comunicação com a controladora (OSDP) são mantidos após o reset. Caso não seja possível salvá-los, o reset é abortado e é retornado o erro `Factory reset aborted: unable to save controller connection params`. Padrão: false. Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo de requisição

Redefine as configurações do equipamento para as de fábrica:

```
$.ajax({
    url: "/reset_to_factory_default.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```
