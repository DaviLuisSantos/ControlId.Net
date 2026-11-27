# Migrando para a 0.11

A `0.11.0-alpha.1` reúne as mudanças do [CHANGELOG](../CHANGELOG.md). Três delas pedem atenção: o limite de requisições
simultâneas caiu para 1, as propriedades `*Utc` dos eventos ficaram obsoletas e a remoção de cartões e grupos na
sincronização passou a ser opt-in. Cada seção mostra o antes e o depois.

## 1. `MaxConcurrentRequests`: de 4 para 1

O padrão de `ControlIdDeviceOptions.MaxConcurrentRequests` passou de 4 para 1, contando o login. O
`ControlIdRepOptions.MaxConcurrentRequests` segue o mesmo padrão. O equipamento atende uma requisição por vez, então
paralelizar dentro de um equipamento não trazia ganho ([ADR 0025](adr/0025-uma-requisicao-por-vez.md)). Vários
equipamentos continuam sendo atendidos em paralelo, por exemplo em `ControlIdDevices.RunAsync`.

**Antes**

```csharp
var device = new AccessClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.168.0.129"),
    MaxConcurrentRequests = 4, // antigo padrão
});
```

```json
"portaria": { "BaseAddress": "http://192.168.0.129", "MaxConcurrentRequests": 4 }
```

**Depois**

```csharp
var device = new AccessClient(new ControlIdDeviceOptions
{
    BaseAddress = new Uri("http://192.168.0.129"),
    // MaxConcurrentRequests = 1 é o padrão; não precisa declarar.
});
```

Quem precisar do comportamento anterior define o valor explicitamente, no código ou no `appsettings.json`:

```csharp
MaxConcurrentRequests = 4,
```

```json
"portaria": { "BaseAddress": "http://192.168.0.129", "MaxConcurrentRequests": 4 }
```

## 2. Propriedades `*Utc` dos eventos: use `Local*`

O equipamento grava a **hora local dele**, não UTC. As propriedades `*Utc` abaixo tratavam essa hora de parede como UTC
e por isso saíam deslocadas pelo fuso. Elas ficaram `[Obsolete]` (o compilador avisa com CS0618); troque pelas
propriedades `Local*`, que devolvem `DateTime` com `Kind = Unspecified`
([ADR 0028](adr/0028-hora-local-dos-eventos.md)).

| Antes (`*Utc`, obsoleta) | Depois (`Local*`) | Modelo |
| --- | --- | --- |
| `TimestampUtc` | `LocalTimestamp` | `AccessDeviceEvent` |
| `TimeUtc` | `LocalTime` | `AlarmLog` |
| `TimestampUtc` | `LocalTimestamp` | `ChangeLog` |
| `TimeUtc` | `LocalTime` | `ExitButtonLog` |
| `TimeUtc` (`DateTimeOffset?`) | `LocalTime` (`DateTime?`) | `OnlineIdentificationEvent` |

O `AccessLog` também ganhou `LocalTime`; ele não tinha propriedade `*Utc`.

**Antes**

```csharp
AccessDeviceEvent evento = ...;
DateTimeOffset quando = evento.TimestampUtc; // obsoleta: tratava a hora local como UTC
```

**Depois**

```csharp
AccessDeviceEvent evento = ...;
DateTime quando = evento.LocalTimestamp; // hora de parede do equipamento, Kind = Unspecified

// Para comparar com o relógio do servidor, converta usando o fuso do equipamento:
var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
var instante = new DateTimeOffset(quando, fuso.GetUtcOffset(quando));
```

Não use `ToUniversalTime()` numa data `Unspecified`: ela seria interpretada como o fuso da máquina, não o do
equipamento. O mesmo vale para `OnlineIdentificationEvent`, cuja `LocalTime` é `DateTime?`.

## 3. Cartões e grupos: remoção só com `RemoveUnlisted`

Por padrão, `SyncUserAsync` e `SyncUsersAsync` agora só criam o que falta. Cartões e vínculos de grupo que estão no
equipamento e não aparecem em `DesiredUser.Cards`/`DesiredUser.GroupIds` continuam lá; antes eram apagados. Para retomar
a remoção, use `UserSyncOptions.RemoveUnlisted = true`.

**Antes**

```csharp
// Removia os cartões e grupos que não estavam na lista.
await device.SyncUsersAsync(desejados);
```

**Depois**

```csharp
// Padrão: só cria o que falta; não remove cartões nem grupos.
await device.SyncUsersAsync(desejados);
```

```csharp
// Comportamento anterior: remove o que sobra.
await device.SyncUsersAsync(desejados, new UserSyncOptions
{
    RemoveUnlisted = true,
});
```

Detalhes que mudam o resultado:

- Com `RemoveUnlisted = true`, uma lista vazia de `Cards` ou `GroupIds` remove **todos** os cartões ou vínculos de
  grupo daquela pessoa. `null` continua significando "não gerenciar".
- `RemoveUnlisted` não remove pessoas. Para isso existe `RemoveMissing`, que continua exigindo
  `DataLossConfirmation.Confirmed` e não é afetado por `RemoveUnlisted`.
- `DryRun = true` só compara e relata o que mudaria, sem escrever no equipamento. Com ele, `RemoveMissing` não exige
  confirmação. Use `UserSyncResult.Status` (`Unchanged`, `Created`, `Changed`, `Removed`, `Failed` ou
  `OutcomeUnknown`) e as listas `CardsRemoved`/`GroupsRemoved` para revisar a prévia:

```csharp
var previa = await device.SyncUsersAsync(desejados, new UserSyncOptions
{
    RemoveUnlisted = true,
    RemoveMissing = true,
    DryRun = true,
});

foreach (var r in previa)
{
    Console.WriteLine($"{r.UserId}: {r.Status}");
    Console.WriteLine($"  cartões que seriam removidos: {string.Join(", ", r.CardsRemoved)}");
    Console.WriteLine($"  grupos que seriam removidos: {string.Join(", ", r.GroupsRemoved)}");
}
```

## Resumo

| Mudança | Antes | Depois |
| --- | --- | --- |
| Requisições simultâneas por equipamento | padrão 4 | padrão 1; defina `MaxConcurrentRequests` para mudar |
| Hora dos eventos | `*Utc`, tratada como UTC | `Local*`, hora de parede do equipamento |
| Cartões e grupos fora da lista | removidos sempre | removidos só com `RemoveUnlisted = true` |

Veja também o [guia de uso](guia.md), a [matriz de validação](matriz-validacao.md) e as
[decisões de arquitetura](adr/README.md).
