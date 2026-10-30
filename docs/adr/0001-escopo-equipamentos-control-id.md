# 0001. O pacote cobre o que o equipamento Control iD suporta

Status: Aceita (2026-10-30)

## Contexto

A pesquisa para o plano do projeto levantou ideias de outros fabricantes (ZKTeco, Hikvision, Intelbras, Suprema, Anviz) e
integrações com sistemas de terceiros (Home Assistant, MQTT, iDSecure, RHiD, geração de AEJ). Sem um critério, o pacote
cresceria para além do que os equipamentos fazem.

## Decisão

O ControlId.Net cobre **tudo o que o equipamento Control iD suporta, e nada além**: a API HTTP inteira (linha de acesso e
REP), os recursos de cada firmware novo e os protocolos que o próprio equipamento fala (ONVIF, RTSP, SNMP). A meta é
cobertura completa, não uma seleção.

Ficam fora: sistemas de terceiros, tarefas do programa de tratamento de ponto (o REP não gera AEJ) e outras marcas.
Outras marcas terão pacotes próprios, com o mesmo critério para os seus equipamentos; só a infraestrutura comum (execução em
vários equipamentos, limite de requisições, resiliência, telemetria) poderá ir para um pacote neutro, e só quando houver
dois usos reais.

## Consequências

- Uma pergunta decide se algo entra: "o equipamento faz isso?".
- Nada de interface genérica de controle de acesso dentro do ControlId.Net: ela esconderia recursos específicos.
- O `docs/COBERTURA.md` é a medida do escopo, e o que a documentação da Control iD ou o firmware acrescentam vira lacuna.
