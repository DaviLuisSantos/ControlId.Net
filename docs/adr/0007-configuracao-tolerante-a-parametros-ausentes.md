# 0007. Leitura de configuração tolerante a parâmetros que o modelo não tem

Status: Aceita (2026-10-13)

## Contexto

As seções de configuração tipadas (`GeneralSettings`, `MonitorSettings`...) pedem todos os parâmetros documentados. Num
iDFace 6.9.12, o `get_configuration.fcgi` recusa a leitura inteira se um único parâmetro não existir naquele modelo (ex.:
`general.catra_timeout`, que é de catraca), e 25 das 34 seções falhavam.

## Decisão

`GetConfigurationAsync` lê o nome do parâmetro na mensagem de erro do equipamento, tira ele da lista e repete. O cliente
guarda os parâmetros inexistentes daquele equipamento e não os pede de novo. Nas seções tipadas, o parâmetro fica `null`.

A gravação (`set_configuration`) não faz o mesmo: ela não é atômica (aplica os parâmetros anteriores ao inexistente e depois
recusa), e esconder um parâmetro que o usuário pediu para gravar mascararia um erro de digitação. Isso fica documentado.

## Consequências

- Uma seção tipada funciona em qualquer modelo; o custo é uma requisição a mais por parâmetro inexistente, só na primeira
  leitura.
- Depende do formato da mensagem de erro do firmware (`Node or attribute not found ... param[name=...]`); se o formato
  mudar, a leitura volta a falhar com a exceção do equipamento, sem erro silencioso.
