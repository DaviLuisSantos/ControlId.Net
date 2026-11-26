# 0023. Limites da fila de Push

Status: Aceita (2026-11-19). Complementa a [0020](0020-fila-de-push-duravel.md).

## Contexto

A [ADR 0020](0020-fila-de-push-duravel.md) tornou a fila de Push durável, mas sem prazos nem limites. A auditoria de
2026-11-19 achou: `SendAsync` sem prazo (um equipamento que some deixa a tarefa esperando para sempre) e envio "em voo"
eterno; fila sem tamanho máximo nem validade (um equipamento desligado por dias executaria ordens velhas ao voltar);
dicionários que cresciam com qualquer `deviceId` posto na query; um `IPushResultHandler` que lançava derrubava `/push` ou
`/result` e perdia o próximo comando; cancelamento que não desfazia o que já estava no store; e o store EF sem claim
atômico, então duas instâncias do servidor sobre o mesmo banco entregavam o mesmo comando.

## Decisão

- **Prazos e limites** em `PushServerOptions` (novo construtor `PushServer(store, options, handler)`; os construtores
  antigos usam os padrões): `CommandTimeout` 5 min (prazo de `SendAsync`/`SendBatchAsync`, contado do enfileiramento, que
  vale junto com o token do chamador; ao vencer, o envio sai da fila e a tarefa falha com `TimeoutException`),
  `CommandTtl` 24 h (pendente mais velho não é entregue e falha como expirado), `InFlightTimeout` 10 min (só para a
  varredura), `MaxPendingPerDevice` 1.000 (acima, `PushQueueFullException`), `MaxTrackedDevices` 10.000 (`GetLastSeen`),
  `RequireUuid`, `ResultHandlerTimeout` 30 s e `PurgeInterval` 1 min. `null` desliga os prazos opcionais.
- **Varredura.** `PushServer.PurgeExpiredAsync` remove pendentes vencidos e envios em voo sem resultado, pelo contrato
  novo `IPushCommandStoreMaintenance` (o `IPushCommandStore` publicado não muda; store sem a interface devolve 0). O pacote
  ASP.NET Core chama a varredura a cada `PurgeInterval` num serviço de fundo; sem ele, quem hospeda chama.
- **Handler isolado.** O `IPushResultHandler` recebe um token próprio que vence em `ResultHandlerTimeout`, sem ligação com
  a requisição do equipamento. Exceção vira log 1101 e a resposta segue: o próximo comando é entregue mesmo que o handler
  do órfão falhe. No ASP.NET Core, todos os handlers registrados rodam, em sequência, num escopo por resultado.
- **Memória limitada.** A serialização por equipamento usa 256 `SemaphoreSlim` fixos, escolhidos pelo hash do `deviceId`.
  Equipamentos que colidem no hash são atendidos em série entre si: custo pequeno, em troca de memória que não cresce com
  `deviceId` arbitrário. `DeviceContactTracker` também tem limite (10.000, configurável no construtor).
- **Cancelamento.** `CancelAsync` libera a tarefa na hora e remove do store em segundo plano (falha → log 1106); um
  `AddAsync` interrompido tenta remover o que entrou antes de relançar.
- **Claim atômico no EF, sem mudança de esquema.** O próximo envio é escolhido na ordem `(CreatedAtTicks, Id)` e tomado
  com um `UPDATE` condicional (`Id` e `Status = Pending`). Zero linhas afetadas quer dizer que outra instância levou: tenta
  o seguinte, até 5 vezes. Órfãos são apagados com `DELETE` condicional (`Status = InFlight`) e só reportados por quem
  apagou. Se a concorrência deixar vários `InFlight` do mesmo equipamento, todos saem e só o mais antigo é reportado.
- `AddAsync` continua acrescentando ao fim (`CreatedAtTicks` maior que o do último envio do equipamento, o "máximo + 1");
  dois `AddAsync` simultâneos em instâncias diferentes podem empatar, e o desempate por `Id` mantém a ordem determinística.

## Consequências

- **Quebra de comportamento:** `SendAsync` passa a falhar em 5 minutos, mesmo com token do chamador sem prazo; comandos
  pendentes por mais de 24 horas são descartados; a fila de cada equipamento tem 1.000 envios no máximo. Tudo configurável,
  e `null`/`0` desligam.
- Várias instâncias sobre o mesmo banco não entregam o mesmo envio, e `EnqueueAsync` + `IPushResultHandler` funcionam em
  qualquer instância. `SendAsync` ainda exige que o `/result` chegue à instância que enfileirou: em outra, o resultado vai
  ao `IPushResultHandler` dela e a tarefa original termina pelo `CommandTimeout`. Levar as tarefas em espera entre
  instâncias fica fora deste escopo.
- `ExecuteUpdate`/`ExecuteDelete` condicionais dependem do provedor do EF; os testes cobrem só o SQLite. Com SQLite e
  várias conexões, configure `Default Timeout` na connection string para as escritas esperarem o bloqueio.
- Métrica nova `controlid.push.commands` (tag `controlid.push.outcome`) e logs 1101–1107.
