# 0025. Uma requisição por vez por equipamento

Status: Aceita (2026-11-20)

## Contexto

O `ControlIdDeviceOptions.MaxConcurrentRequests` limitava as chamadas simultâneas a um equipamento em 4. O servidor HTTP
embarcado, porém, é pequeno: um iDFace ocupado com o reconhecimento facial atrasa as respostas, e várias chamadas em
paralelo viram timeouts. Numa escrita, o timeout é o pior caso, porque não se sabe se o comando foi executado (ADR 0026).
Além disso, a sessão é única por equipamento, e um re-login no meio de chamadas paralelas misturava respostas de sessões
diferentes.

## Decisão

- `ControlIdDeviceOptions.DefaultMaxConcurrentRequests` passa de 4 para 1. O cliente envia uma requisição por vez a cada
  equipamento; as demais esperam na fila interna (`RequestSlots`).
- Quem mede que o seu equipamento aguenta mais sobe o valor por equipamento, em código ou no `appsettings.json`. O
  `ControlIdRepOptions` herda o mesmo padrão.
- O limite continua sendo por cliente: vários equipamentos seguem em paralelo entre si.

## Consequências

- Menos timeouts e menos desfechos desconhecidos em equipamentos lentos, ao custo de latência maior para quem disparava
  chamadas em paralelo contra o mesmo equipamento.
- É uma mudança de comportamento, não de assinatura: a constante pública muda de valor, o que a validação de pacote
  (ADR 0006) não acusa. Por isso entra no CHANGELOG e no `PublicAPI.Unshipped.txt`.
- Quem dependia do paralelismo precisa declarar o valor. Os testes que assumiam 4 foram ajustados.
