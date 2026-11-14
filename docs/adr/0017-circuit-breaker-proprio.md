# 0017. Circuit breaker próprio por equipamento

Status: Aceita (2026-11-14)

## Contexto

Um equipamento fora do ar faz cada chamada esperar o timeout inteiro. Com vários equipamentos e requisições em
paralelo, as chamadas a um equipamento morto seguram threads e a vaga do limite de requisições. A biblioteca já tem
novas tentativas só em leituras ([ADR 0013](0013-novas-tentativas-so-em-leituras.md)).

O caminho usual seria `Microsoft.Extensions.Http.Resilience` (Polly v8). Mas o núcleo roda em `netstandard2.0` e
`net48`, e o transporte decide o que é seguro pelo nome do comando, não pelo método HTTP.

## Decisão

- O circuit breaker fica no transporte do núcleo, por cliente (um cliente é um equipamento), sem dependência nova.
- Abre após `CircuitBreakerFailureThreshold` falhas de transporte seguidas (rede, timeout, HTTP 5xx); `0` desliga, que é o
  padrão. O limite máximo é `MaxCircuitBreakerFailureThreshold` (100).
- Aberto, falha na hora com `ControlIdCircuitOpenException` (com `RetryAfter`), sem tocar na rede. Passado
  `CircuitBreakerBreakDuration`, deixa passar uma chamada de teste (meio aberto): se der certo, fecha; senão, reabre.
- O estado aparece em `AccessClient.CircuitState` e `RepClient.CircuitState` (`ControlIdCircuitState`).
- Erros do equipamento (HTTP 4xx, sessão inválida) não contam como falha do circuito.

## Consequências

- Sem Polly, sem pacote novo, igual nos três alvos.
- Fica desligado por padrão, então nada muda para quem atualiza.
- Não há *hedging*: o equipamento atende uma requisição por vez.
