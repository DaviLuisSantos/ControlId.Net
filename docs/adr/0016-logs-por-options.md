# 0016. Logs pelo ILoggerFactory, escolhido nas options ou no container

Status: Aceita (2026-11-13)

## Contexto

O pedido (#25) é ter logs estruturados nos clientes. O núcleo (`ControlId.Net`) não depende do container de DI e roda
em netstandard2.0, então não pode exigir `Microsoft.Extensions.DependencyInjection`. Só a abstração
`Microsoft.Extensions.Logging.Abstractions` é aceitável como dependência.

## Decisão

- `ControlIdDeviceOptions.LoggerFactory` (padrão `null`, que vira `NullLogger`) é o ponto de entrada no núcleo; a
  categoria é `ControlId.Net` e as mensagens são `LoggerMessage` geradas (EventIds 1001 a 1006), sem sessão, login,
  senha nem corpo.
- No pacote de DI, `ControlIdClientFactory` recebe o `ILoggerFactory` do container (opcional) e o passa aos clientes
  por construtor interno, sem alterar a instância de options do `IOptionsMonitor`. Um `LoggerFactory` definido
  explicitamente nas options vence o do container.
- As rotas do ASP.NET Core usam `LoggerMessage` (EventIds 2001 a 2004), e o CA1848 deixa de ser suprimido.

## Consequências

- Quem usa o núcleo sem DI liga os logs com uma linha nas options; quem usa DI não configura nada.
- Mudar o `ILoggerFactory` exige recriar o cliente, como qualquer outra mudança de configuração.
- Os EventIds são parte do contrato observável: não reutilizar nem renumerar.
