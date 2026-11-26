# 0028. Hora local dos eventos

Status: Aceita (2026-11-20)

## Contexto

Os eventos do equipamento (`AccessLog`, `AlarmLog`, `ChangeLog`, `ExitButtonLog` e os eventos online) trazem um Unix
timestamp. As propriedades `*Utc` da biblioteca o liam como UTC, mas o equipamento grava a hora de parede local dele nesse
campo, sem fuso. Um relógio em UTC-3 mostrava um acesso às 08:00 como 11:00, e `DateTimeKind.Utc` induzia quem consome a
converter de novo.

## Decisão

- Propriedades novas devolvem a hora como o equipamento a gravou, via `DeviceLocalTime.FromEpoch`, com
  `DateTimeKind.Unspecified`: `AccessLog.LocalTime`, `AccessDeviceEvent.LocalTimestamp`, `AlarmLog.LocalTime`,
  `ChangeLog.LocalTimestamp`, `ExitButtonLog.LocalTime` e `OnlineIdentificationEvent.LocalTime` (`DateTime?`).
- Os `*Utc` correspondentes ficam e ganham `[Obsolete("O equipamento grava a hora local dele, não UTC. Use Local...")]`.
  Não são removidos: a política do ADR 0029 pede ao menos uma versão menor com o aviso.
- A conversão inversa (filtros por data) usa a mesma convenção: a hora de parede vira o epoch, sem fuso.
- A biblioteca não adivinha o fuso do equipamento. Quem precisa de um instante absoluto aplica o fuso conhecido do
  local (`TimeZoneInfo`) sobre o valor `Unspecified`.

## Consequências

- O valor deixa de induzir uma conversão errada: `Unspecified` obriga a escolher o fuso.
- Quem usa `*Utc` recebe o aviso CS0618 e o mesmo valor de antes até migrar.
- O fuso do equipamento pode divergir do servidor ou mudar com o horário de verão. Isso continua fora da biblioteca.
