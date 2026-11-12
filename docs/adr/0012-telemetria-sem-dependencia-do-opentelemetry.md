# 0012. Telemetria pelas APIs do .NET, sem depender do OpenTelemetry

Status: Aceita (2026-11-12)

## Contexto

Quem integra vários equipamentos precisa saber qual está lento, qual reiniciou e se o limite de requisições está
represando chamadas. O `HttpClient` já gera spans de cada requisição, mas não mostra o que é próprio do pacote: o comando
lógico, a espera na fila, o login refeito no meio de uma chamada.

## Decisão

- A biblioteca usa `System.Diagnostics.ActivitySource` e `System.Diagnostics.Metrics.Meter`, que fazem parte do .NET (no
  `netstandard2.0`, pelo pacote `System.Diagnostics.DiagnosticSource`). Não referencia pacotes do OpenTelemetry: quem
  usa escolhe o SDK e o exportador. Os nomes ficam em `ControlIdTelemetry`.
- Um span por **comando lógico**, não por requisição HTTP: ele inclui a fila, o re-login e a nova tentativa. As
  requisições HTTP aparecem como filhas pela instrumentação do `HttpClient`, sem duplicar o que ela já faz.
- Nomes de span e atributos com baixa cardinalidade (o comando, sem query string) e as convenções semânticas do
  OpenTelemetry onde existem (`server.address`, `server.port`, `error.type`, `http.response.status_code`).
- A sessão e as credenciais nunca vão para a telemetria. Isso é garantido por teste.
- Métricas de fila e de requisições ativas saem do próprio limitador (`RequestSlots`), que é onde a espera acontece.

## Consequências

- Nenhuma dependência nova no `net8.0`; uma no `netstandard2.0`, da própria Microsoft e alinhada à ADR 0010.
- Sem listener registrado, o custo é o de um `StartActivity` que devolve `null` e de instrumentos sem assinantes.
- Os logs estruturados (`ILogger` com `LoggerMessage`) ficam para uma etapa própria: exigem decidir como o `ILogger` chega
  a um cliente criado sem DI.
