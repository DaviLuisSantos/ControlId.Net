<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/reforco-de-seguranca/ -->

# Reforço de segurança

## Ativar/desativar acesso SSH

Os equipamentos da linha de controle de acesso possuem suporte a conexões SSH, permitindo que a equipe da Control iD realize diagnósticos e suporte técnico remoto quando necessário. Esse recurso facilita a manutenção e a resolução de problemas sem a necessidade de intervenção física no equipamento.

Por padrão, o SSH vem habilitado para garantir que o suporte possa ser prestado sempre que necessário. No entanto, há situações em que administradores podem optar por desativá-lo, especialmente quando há a necessidade de reduzir a superfície de ataque do sistema.

Manter esse serviço ativo pode representar um vetor de risco caso credenciais sejam comprometidas ou vulnerabilidades sejam exploradas. Em algumas organizações, políticas de segurança exigem a restrição do acesso remoto, seguindo diretrizes regulatórias ou padrões internos, o que pode tornar sua desativação necessária.

Além disso, em ambientes onde há preocupação com acessos não autorizados, como locais com múltiplos administradores ou funcionários que possuem acesso à rede local onde o dispositivo está conectado, desativar o SSH pode ser uma medida adicional de proteção.

Para ativar ou desativar o acesso SSH, deve-se utilizar o mesmo enpoint destinado a [modificar configurações](../configuracoes/modificar-configuracoes.md) alterando o parâmetro *ssh_enabled*, listado nos [parâmetros de configuração](../configuracoes/parametros-configuracao.md).

Uma vez desativado, o acesso por SSH pode ser ativado novamente a qualquer momento.

### POST /set_configuration.fcgi

*Parâmetros*

- O parâmetro será um **Objeto JSON** que contém o parâmetro *ssh_enabled* e (opcionalmente) outros parâmetros.
  - *ssh_enabled = 0* desativa o acesso por SSH
  - *ssh_enabled = 1* ativa o acesso por SSH

*Resposta*

- A resposta será um **Objeto JSON** correspondente aos parâmetros de configuração modificados.

#### Exemplo de requisição

Habilita o som de beep e desabilita o acesso por SSH:

```
$.ajax({
  url: "/set_configuration.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
    general: {"beep_enabled": "1", "ssh_enabled": "0"}
  })
});
```

## Recursos de rede disponíveis

Na tabela abaixo, há informações sobre todos os recursos de rede, IP e acesso remoto dos equipamentos.

| Recurso | Habilitado por padrão | Porta padrão | Descrição |
| --- | --- | --- | --- |
| **HTTP** | ✔️ | **80** | Utilizado para a Interface Web Server embarcado |
| **HTTPS/SSL** | ✖️ | **443** | Protocolo seguro para acesso à Interface Web via criptografia |
| **NTP** | ✖️ | **123** | Protocolo utilizado para sincronização de tempo em rede |
| **RTSP** | ✖️ | **554** | Protocolo para transmissão de áudio e vídeo pela rede em tempo real |
| **ONVIF** | ✖️ | **8000** | Padrão para interoperabilidade de dispositivos de videovigilância |
| **SIP** | ✖️ | **5060** | Protocolo usado para sinalização em comunicação VoIP. Habilita as chamadas SIP do dispositivo |
| **DNS** | ✔️ | **53** | Sistema para tradução de nomes de domínio para endereços IP |
| **DHCP** | ✖️ | **67/68** | Protocolo para configuração automática de IP na rede |
| **SNMP** | ✖️ | **161/162** | Protocolo para monitoramento e gerenciamento de dispositivos em rede |
| **SSH** | ✔️ | **22** | Utilizado para acesso remoto seguro ao dispositivo |

## Senha mestra

Gerencia a senha mestra do equipamento. Este endpoint opera em dois modos conforme a presença do campo `password` no corpo da requisição:

- **Sem `password`**: retorna a senha mestra atual como hash e salt.
- **Com `password`**: define uma nova senha mestra a partir do texto simples.

### POST /master_password.fcgi

*Parâmetros*

- **password** *(string)*: Opcional. Nova senha mestra em texto simples. Se omitido, o endpoint retorna o hash e salt atuais em vez de definir uma nova senha.

*Resposta (sem `password`)*

- **hash** *(string)*: Hash da senha mestra atual.
- **salt** *(string)*: Salt utilizado para gerar o hash.

*Resposta (com `password`)*

- Esta chamada não possui retorno.

#### Exemplo de requisição — obter hash atual

```
$.ajax({
    url: "/master_password.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```

#### Exemplo de resposta

```
{
    "hash": "ab5d13cea3c4c20c954711c7006004f8...",
    "salt": "y2a2B`Lt.A]8iI|\\M]Ao"
}
```

#### Exemplo de requisição — definir nova senha

```
$.ajax({
    url: "/master_password.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        password: "novasenha123"
    })
});
```
