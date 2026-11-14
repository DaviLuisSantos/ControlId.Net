# 0020. Fila de Push durável (`IPushCommandStore`)

Status: Aceita (2026-11-14)

## Contexto

Os comandos de Push pendentes ficavam em memória: reiniciar o servidor perdia a fila, e um resultado que chegava depois
do reinício não tinha de quem ser.

## Decisão

- A fila passa a ser o `IPushCommandStore`, com `InMemoryPushCommandStore` como padrão. O `PushServer` recebe o store e
  um `IPushResultHandler` opcional.
- O pacote `ControlId.Net.Push.EntityFrameworkCore` traz o store durável: `ApplyControlIdPush` no modelo e
  `AddControlIdPushEntityFrameworkStore<TContext>` no container.
- O payload é serializado no enqueue. Alterar o `Body` do comando depois de enfileirar **não tem mais efeito**.
- Um comando "em voo" cujo resultado nunca chegou (órfão) é recolhido na próxima consulta. Sem tarefa esperando, é
  reportado ao `IPushResultHandler` com `Error` e nunca reenviado, porque reenviar poderia executar duas vezes.
- Exceção ao [ADR 0014](0014-manter-alvo-net8.md): o pacote EF é só `net10.0`, por exigência do EF Core 10. O núcleo
  segue em `net8.0` e `netstandard2.0`.
- O teste do store EF usa um arquivo SQLite temporário, porque o SQLite em memória vive só enquanto a conexão está
  aberta e o store abre uma por operação.

## Consequências

- A fila sobrevive a reinício; o handler é o ponto para tratar resultados órfãos.
- Quem implementa `IPushCommandStore` por conta própria (outro banco) tem um contrato pequeno e testável.
- Quem usa o pacote EF precisa do .NET 10.
