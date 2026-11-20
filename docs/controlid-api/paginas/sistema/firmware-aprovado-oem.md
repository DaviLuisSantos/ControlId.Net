<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/firmware-aprovado-oem/ -->

# Firmware aprovado pelo OEM NEW!

Permite que o integrador OEM fixe a versão de firmware instalada pelo [modo de update](../sistema/reiniciar-em-modo-de-update.md). Os comandos exigem um [código OEM](../sistema/obter-informacoes-do-sistema.md) vinculado ao equipamento e a chave de licença correspondente, fornecida pela Control iD.

Quando o equipamento tem um código OEM e uma versão aprovada definida, a atualização feita pelo modo de update instala a versão aprovada em vez da mais recente publicada. Se a versão aprovada for anterior à versão atual, a atualização é um downgrade, aceito apenas para as versões retornadas por [get_oem_downgrade_approved_versions](../sistema/firmware-aprovado-oem.md#post-get_oem_downgrade_approved_versionsfcgi).

Disponível apenas na versão **[V7](../particularidade-dos-produtos/versoes-de-firmware.md)**.

## Obter versões aprovadas para downgrade

### POST /get_oem_downgrade_approved_versions.fcgi

Retorna a lista de versões de firmware aceitas como destino de downgrade OEM. É uma lista, e não um piso: uma versão fora dela não é aceita, mesmo que uma versão mais antiga esteja na lista.

A lista vem do próprio firmware. Após um downgrade, continua valendo a lista gravada pela versão mais nova que já esteve instalada no equipamento.

*Parâmetros*

- Esta chamada não possui parâmetros.

*Resposta*

- **versions** *(array de strings)*: Versões aceitas como destino de downgrade, no formato `major.minor.patch`.

#### Exemplo de requisição

```
$.ajax({
    url: "/get_oem_downgrade_approved_versions.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json'
});
```

#### Exemplo de resposta

```
{
    "versions": ["7.12.0", "7.13.2"]
}
```

## Definir a versão de firmware aprovada

### POST /set_oem_approved_fw.fcgi

Define a versão de firmware aprovada pelo OEM, ou remove a restrição.

*Parâmetros*

- **oem_code** *(string)*: **Obrigatório.** Código OEM. Deve ser o mesmo código OEM já vinculado ao equipamento.
- **license** *(string)*: **Obrigatório.** Chave de licença do código OEM para este equipamento, fornecida pela Control iD.
- **approved_fw** *(string)*: **Obrigatório.** Versão aprovada, no formato `major.minor.patch` (por exemplo, `"7.14.0"`). Deve ter a mesma versão *major* do firmware atual. Se for anterior à versão atual, deve estar na lista de [get_oem_downgrade_approved_versions](../sistema/firmware-aprovado-oem.md#post-get_oem_downgrade_approved_versionsfcgi). Uma string vazia (`""`) remove a restrição.

*Resposta*

- Em caso de sucesso, retorna um objeto JSON vazio.

Erros possíveis:

- "Cannot set an approved firmware version: invalid OEM code/license": a licença não é válida para o código OEM neste equipamento.
- "Cannot set an approved firmware version: OEM code does not match the equipment's linked OEM code": o código OEM é diferente do vinculado ao equipamento.
- "Invalid firmware version: ...": **approved_fw** não está no formato `major.minor.patch`.
- "Approved firmware version ... must have the same major version as the equipment's current firmware ...": a versão *major* é diferente da atual.
- "Approved firmware version ... is not an approved OEM downgrade target": a versão é anterior à atual e não está na lista de versões aprovadas para downgrade.

#### Exemplo de requisição

```
$.ajax({
    url: "/set_oem_approved_fw.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({
        oem_code: "MYOEM",
        license: "ABCDE12345",
        approved_fw: "7.14.0"
    })
});
```

#### Exemplo de resposta

```
{}
```
