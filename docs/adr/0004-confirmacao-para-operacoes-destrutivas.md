# 0004. Confirmação explícita para apagar dados sem volta

Status: Aceita (2026-10-30)

## Contexto

Reset de fábrica, apagar administradores, apagar todas as fotos e apagar todos os templates faciais eram uma chamada comum,
igual a qualquer leitura. Nos testes num iDFace real, um reset de fábrica (de origem não identificada) apagou usuários,
fotos, logs e configuração de um equipamento em uso; só um backup feito minutos antes permitiu restaurar.

Opções consideradas: só documentar; um analisador Roslyn; mudar a assinatura dos métodos.

## Decisão

Esses quatro métodos ganham sobrecargas que exigem `DataLossConfirmation.Confirmed`. O enum tem `None = 0`, e qualquer
valor diferente de `Confirmed` (inclusive o padrão de um `default` esquecido) é recusado com `ArgumentException` antes de
qualquer requisição. As sobrecargas antigas ficam `[Obsolete]` e continuam funcionando.

## Consequências

- Apagar dados exige escrever a intenção no ponto da chamada, e a revisão de código enxerga isso.
- Quem usa as sobrecargas antigas recebe um aviso de compilação, mas não quebra; elas saem numa versão futura.
- Acrescentar membros a `IAccessClient` quebra quem implementa a interface por conta própria; está registrado em
  `CompatibilitySuppressions.xml` (ADR 0006).
- Um analisador Roslyn continua possível para casos que o tipo não cobre, como `DestroyObjectsAsync` sem filtro.
