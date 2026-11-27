# 0031. Sem camada `Result<T>`/`CommandOutcome`

Status: Aceita (2026-11-27). Complementa a 0026.

## Contexto

A issue #68 pede uma superfície que não lance exceções e devolva um valor para `Ok`, `DeviceError`, `NotSupported`,
`Timeout`, `ConnectionFailed`, `OutcomeUnknown` e `Cancelled`. A proposta é avaliar métodos `TryXxxAsync` e/ou um tipo
`Result<T>`/`CommandOutcome`. Hoje a API comunica o desfecho só por exceções (ADR 0026), e a pergunta é se uma camada
intermediária simplifica o consumo ou apenas duplica o contrato.

## Decisão

**Não adicionar `TryXxxAsync` nem `Result<T>`/`CommandOutcome` por ora.** `IAccessClient` continua com os métodos atuais,
lançando as exceções por desfecho. Quem quiser um desfecho como enum converte no próprio código, como no exemplo abaixo.

As sete situações já são distinguíveis sem uma camada nova:

| Desfecho | Como aparece hoje |
| --- | --- |
| `Ok` | retorno normal, sem exceção |
| `DeviceError` | `ControlIdDeviceErrorException` |
| `NotSupported` | `ControlIdNotSupportedException` (subtipo de `ControlIdDeviceErrorException`) |
| `Timeout` | `ControlIdTimeoutException` |
| `ConnectionFailed` | `ControlIdConnectionException` |
| `OutcomeUnknown` | `ControlIdOutcomeUnknownException` (com `Command` e `TimedOut`) |
| `Cancelled` | `OperationCanceledException` do token de quem chamou |

A decisão se sustenta em quatro pontos:

- **A segunda superfície dobraria a API antes da 1.0.** Cada comando novo precisaria de duas formas, com documentação,
  testes e baseline de API pública em paralelo; a chance de as duas divergirem é maior que o ganho. A 0029 já fixa que,
  na 1.0, `IAccessClient` deixa de crescer e o que for novo vai para interfaces separadas.
- **Os lotes já devolvem resultado por item.** `SyncUsersAsync` devolve `IReadOnlyList<UserSyncResult>`, com `Success` e
  `Errors` por pessoa, e `SetUserImagesAsync` devolve `IReadOnlyList<FaceImageResult>`, com `Success = false` e os erros do
  item, sem derrubar o lote. O que sobra para a camada de resultado são as chamadas de um item só.
- **Cancelamento não é desfecho do equipamento.** `OperationCanceledException` é controle de fluxo cooperativo e precisa
  continuar visível; transformá-lo em mais um valor de `Result` convida quem chama a ignorá-lo.
- **O caminho de sucesso não paga por exceção.** Exceções só custam quando há falha, e as propriedades úteis
  (`DeviceMessage`, `Command`, `TimedOut`, `ImageRejectionReason`) já vêm no tipo lançado.

### Exemplo de adaptação no consumidor

```csharp
public enum ControlIdCallOutcome
{
    Ok,
    DeviceError,
    NotSupported,
    Timeout,
    ConnectionFailed,
    OutcomeUnknown,
    Cancelled,
}

public static async Task<ControlIdCallOutcome> RunAsync(
    Func<CancellationToken, Task> call,
    CancellationToken cancellationToken)
{
    try
    {
        await call(cancellationToken).ConfigureAwait(false);
        return ControlIdCallOutcome.Ok;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return ControlIdCallOutcome.Cancelled;
    }
    catch (ControlIdNotSupportedException)
    {
        return ControlIdCallOutcome.NotSupported;
    }
    catch (ControlIdDeviceErrorException)
    {
        return ControlIdCallOutcome.DeviceError;
    }
    catch (ControlIdTimeoutException)
    {
        return ControlIdCallOutcome.Timeout;
    }
    catch (ControlIdConnectionException)
    {
        return ControlIdCallOutcome.ConnectionFailed;
    }
    catch (ControlIdOutcomeUnknownException)
    {
        return ControlIdCallOutcome.OutcomeUnknown;
    }
}
```

A ordem importa: `ControlIdNotSupportedException` tem de vir antes de `ControlIdDeviceErrorException`, porque é subtipo
dele. Exceções que o enum não cobre, como `ControlIdCircuitOpenException`, `ControlIdAuthenticationException` e
`ControlIdImageRejectedException`, seguem para quem chamou; quem precisar de mais uma categoria acrescenta no próprio
adaptador. Quem precisa dos detalhes do erro deve capturar antes de converter, porque o enum sozinho os descarta.

### Quando reavaliar

- Depois da 1.0, quando as exceções por desfecho estiverem estáveis (critério 2 da 0029) e houver pedido concreto e
  recorrente de quem consome, com um caso em que `try`/`catch` não atenda bem — por exemplo, um caminho em que toda falha
  precise atravessar uma fronteira que só aceita dados serializáveis.
- Se um caminho de lote ainda lançar por item e o custo da exceção for medido como relevante. Hoje os lotes de usuários e
  de imagens já devolvem resultado por item; a chamada inteira ainda lança quando o comando não é suportado.
- Se a conclusão for que só os casos sem ambiguidade precisam de `TryXxx`, a 0026 já deixou essa porta aberta. `TryXxx` e
  `Result<T>` não precisam ser aceitos juntos.

Se a reavaliação aceitar a camada, ela vai para um **pacote ou namespace separado** (por exemplo, `ControlId.Net.Results`
ou `ControlId.Results`), como adaptadores ou métodos de extensão sobre `IAccessClient`. Ela não altera assinaturas nem
adiciona membros a `IAccessClient`, então não quebra implementações, mocks ou decoradores existentes. A decisão nova ganha
seu próprio ADR.

## Consequências

- Quem consome mantém `try`/`catch`; para uma visão uniforme, usa um adaptador como o acima. O custo é que cada consumidor
  pode escrever o seu, então a documentação deve trazer esse exemplo canônico.
- O contrato público não muda: nenhum símbolo novo, nenhuma quebra e o baseline de API pública fica intacto.
- `Cancelled` continua sendo cancelamento cooperativo. O adaptador devolve o enum em vez de relançar, então quem chamou
  precisa parar o fluxo ao recebê-lo; engolir o cancelamento continua sendo erro de quem consome.
- Falhas parciais de itens em lote continuam em `UserSyncResult.Errors` e `FaceImageResult.Success`/`Errors`, sem exceção
  por item. Falhas da chamada inteira, como "não suportado", continuam lançando.
- Se a camada for aceita no futuro, ela nasce opcional e separada; `IAccessClient` não quebra.
