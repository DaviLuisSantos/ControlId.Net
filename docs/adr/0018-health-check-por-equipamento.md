# 0018. Health check por equipamento, com último contato

Status: Aceita (2026-11-14)

## Contexto

O `AddControlIdDevices` devolve um único resultado para todos os equipamentos. Quem monitora quer saber qual caiu, e
equipamentos com Push ou Monitor já avisam que estão vivos a cada chamada, o que dispensa uma leitura ativa.

## Decisão

- `AddControlIdDeviceChecks` registra um health check por equipamento, com nome `{namePrefix}:access:{nome}` ou `{namePrefix}:rep:{nome}`.
- O `DeviceContactTracker` guarda o último contato de cada equipamento e a origem (`DeviceContactSource`: Push, Monitor
  ou Online). As rotas do pacote ASP.NET Core o alimentam.
- `ControlIdHealthCheckOptions.MaxContactAge` define a idade máxima do último contato. Só degrada o equipamento que tem
  contato registrado e vencido; quem nunca registrou contato não é penalizado por isso, e a leitura ativa continua
  valendo.

## Consequências

- O status de cada equipamento sai separado, com o circuito e o último contato nos dados.
- `AddControlIdDevices` (agregado) continua existindo.
- O contato é mantido em memória; reiniciar o servidor zera o histórico até o próximo contato.
