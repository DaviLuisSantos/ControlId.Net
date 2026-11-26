<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/redirecionar-requisicao/ -->

# Redirecionar Requisição

Encaminha uma requisição FCGi para outro equipamento na rede. Os parâmetros de roteamento são passados na query string; o corpo da requisição é repassado integralmente ao endpoint de destino e a resposta é retornada ao chamador.

Útil em implantações com múltiplos equipamentos, onde um dispositivo central precisa emitir comandos para outros equipamentos em nome de um cliente.

### POST /request_redirect.fcgi

*Parâmetros* (query string)

- **command** *(string)*: Obrigatório. Nome do endpoint FCGi a ser executado no equipamento de destino, sem o sufixo `.fcgi`. Exemplo: `"load_all"`.

Identificação do equipamento de destino — um dos dois modos:

**Modo 1 — por ID do equipamento** (o equipamento deve existir na tabela local de dispositivos):

- **device_id** *(int)*: Obrigatório. ID do equipamento de destino. Não pode ser o próprio ID do equipamento atual.

**Modo 2 — por endereço** (IP direto com credenciais):

- **address** *(string)*: Obrigatório. Endereço IP do equipamento de destino. Não pode ser o próprio IP do equipamento atual.
- **login** *(string)*: Obrigatório. Credencial de login do equipamento de destino.
- **password** *(string)*: Obrigatório. Senha do equipamento de destino.

Quaisquer parâmetros adicionais na query string (exceto os campos de roteamento acima) são repassados ao endpoint de destino.

*Corpo*

- Repassado integralmente ao endpoint de destino. O tipo de conteúdo é preservado.

*Resposta*

- Resposta encaminhada do equipamento de destino. O tipo de conteúdo é preservado.

#### Exemplo de requisição — redirecionar por ID do equipamento

```
$.ajax({
    url: "/request_redirect.fcgi?session=" + session + "&command=load_all&device_id=2",
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```

#### Exemplo de requisição — redirecionar por endereço

```
$.ajax({
    url: "/request_redirect.fcgi?session=" + session + "&command=load_all&address=192.168.0.10&login=admin&password=admin",
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({})
});
```
