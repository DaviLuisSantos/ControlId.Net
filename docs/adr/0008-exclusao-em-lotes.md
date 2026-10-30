# 0008. Exclusão em lotes, encerrando no lote incompleto

Status: Aceita (2026-10-30)

## Contexto

O SDK oficial da Control iD passou a apagar usuários "de forma paginada" (PR 21, 2026-09): manda `limit` no
`destroy_objects.fcgi` e repete até `changes` voltar 0. A documentação não fala em `limit` nesse comando.

## Decisão

`DestroyObjectsAsync` manda `limit` = 200 (`AccessClient.DestroyPageSize`, o mesmo do SDK oficial) e repete enquanto o lote
vier cheio. Um lote com menos de 200 registros encerra o laço.

## Consequências

- Exclusões pequenas (a maioria, como apagar por id) continuam numa requisição só, sem a volta extra que o SDK oficial faz.
- Se um firmware ignorar o `limit` e apagar tudo de uma vez, a volta seguinte devolve 0 e o laço termina.
- Se um firmware recusar o `limit`, a exclusão falha com a exceção do equipamento; ainda não foi visto, e a correção seria
  tirar o parâmetro nesse caso, como na leitura de configuração (ADR 0007).
