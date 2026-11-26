<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/informacoes-catraca/ -->

# Informações da catraca

Os dispositivos iDBlock V2 e iDBlock Next IHM são catracas fabricadas pela Control iD que permitem monitorar o controle de acesso de usuários que atravessam o equipamento. O controle de acesso é gerenciado pelo equipamento por meio das informações armazenadas pela catraca, permitindo um controle mais preciso do fluxo de pessoas pelo sistema.

## Obter informações de giro da catraca

Retorna as informações de giro das catracas registradas no equipamento. Os dados não são persistidos após um *reset* de fábrica.

### GET get_catra_info.fcgi

*Parâmetros:*

- Esta chamada não possui parâmetros.

*Resposta:*

- **left_turns**: Número de giros para a esquerda.
- **right_turns**: Número de giros para a direita.
- **entrance_turns**: Número de giros para a entrada (na convenção adotada).
- **exit_turns**: Número de giros para a saída.
- **total_turns**: Número total de giros.
