# 0013. Novas tentativas só em comandos de leitura, desligadas por padrão

Status: Aceita (2026-11-12)

## Contexto

Equipamentos em rede local falham de forma passageira: Wi-Fi instável, um reinício, um iDFace ocupado com o
reconhecimento facial. Uma nova tentativa resolveria a maioria dessas falhas.

O caminho usual no .NET (`Microsoft.Extensions.Http.Resilience`, Polly) decide o que é seguro repetir pelo método HTTP:
GET sim, POST não. Na API da Control iD **tudo é POST**, inclusive as leituras (`load_objects.fcgi`). Repetir pelo método
desligaria tudo ou repetiria tudo. E repetir tudo é perigoso: um `execute_actions` repetido abre a porta duas vezes, um
`create_objects` cria o registro duas vezes, e um timeout não diz se o equipamento executou o comando ou não.

## Decisão

- As novas tentativas ficam no transporte da biblioteca, que conhece o comando, e não num handler HTTP genérico.
- Só comandos de **leitura**, numa lista explícita por nome (`load_objects`, `system_information`, `get_configuration`,
  `get_afd`...). Um comando fora da lista nunca é repetido. Comandos que leem ou alteram conforme o corpo
  (`alarm_status`) ficam fora.
- Só falhas passageiras: erro de rede, timeout do cliente e HTTP 5xx, 408 e 429. Um 4xx se repetiria igual. O
  cancelamento pedido por quem chamou interrompe a espera na hora.
- Espera exponencial (`RetryDelay`, padrão 200 ms, dobrando) com variação de ±20%, para vários clientes não repetirem
  juntos contra um equipamento que acabou de voltar.
- **Desligado por padrão** (`ReadRetries = 0`): repetir muda o tempo de resposta de quem já usa o pacote. Liga-se por
  equipamento, em código ou no `appsettings.json`.
- Na leitura em streaming (AFD), só se repete antes de os cabeçalhos chegarem.
- Cada nova tentativa vira o evento `controlid.retry` no span e a métrica `controlid.client.retries`.

## Consequências

- Uma leitura num equipamento instável não falha por um pacote perdido, e nenhuma escrita corre o risco de ser
  executada duas vezes.
- A lista precisa acompanhar os comandos novos: um comando de leitura que ficar de fora só perde a nova tentativa, sem
  risco.
- Fica de fora um circuit breaker por equipamento (parar de chamar um equipamento que está falhando). O health check
  (`AddControlIdDevices`) e o prazo por chamada cobrem o caso por ora; o breaker continua no roadmap.
