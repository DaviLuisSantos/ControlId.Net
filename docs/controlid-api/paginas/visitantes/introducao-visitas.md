<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/visitantes/introducao-visitas/ -->

# Suporte a visitantes na linha de acesso

Os equipamentos da linha de acesso apresentam uma funcionalidade de suporte a visitantes. Isso é útil para permitir a identificação de usuários temporários, os quais não fazem parte da organização.

O cadastro de visitantes é utilizado para cadastrar pessoas que acessam uma determinada área em um horário definido. Os usuários visitantes são aqueles que possuem permissão de acesso por tempo limitado. Este período de acesso limitado é determinado no momento do registro do usuário. Os parâmetros que determinam o período de acesso são *begin_time* e *end_time*. Os dados do usuário são visíveis em [Lista de Objetos](../objetos/lista-de-objetos.md#users).

#### Usuários e visitantes expirados

As credenciais de acesso de usuários expirados, como cartões cadastrados, biometrias, faces e senhas, são excluídos do banco de dados do equipamento todos os dias, **16 minutos após o reinício diário do equipamento**.

A partir das versões V6.11.2 (iDFace) e V5.8.10 (iDAccess Nano, iDAccess Pro, iDFit, iDFlex, iDBlock V2 e iDUHF), é possível selecionar a opção de limpeza entre "`all`" (usuários e visitantes), "`visitors`" (visitantes), ou "`disable`" (desabilitado) na tela de reinício do equipamento ou via *web*. Isto é feito através do parâmetro de configuração `clear_expired_users`.

#### Exemplo de requisição

Esta requisição irá fazer com que apenas os dados dos visitantes expirados sejam limpos. É necessário reiniciar o equipamento para que alterações nesse parâmetro entrem em vigor.

```
$.ajax({
    url: "/set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            general:{
                "clear_expired_users":'visitors'
            }
        }
    )
});
```
