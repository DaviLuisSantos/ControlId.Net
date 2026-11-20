<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/liberacao-agendada/ -->

# Liberação agendada

É possível agendar a liberação de acesso no iDFace, desta forma o acesso será concedido a todos os usuários durante um período predeterminado em dias específicos, sem a necessidade de identificação dos mesmos. As [Liberações Agendadas são armazenadas como objetos](../objetos/lista-de-objetos.md) e devem ser criadas e modificadas seguindo as mesmas premissas dos demais objetos via API.

### Cadastrar uma liberação agendada

O processo para cadastrar uma liberação agendada se assemelha ao processo para a criação de um [departamento (grupo)](../primeiros-passos/cadastrar-usuarios-e-suas-regras.md).

- Criar objetos do tipo **scheduled_unlocks** (liberações agendadas) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/10800185/SztHW4xo#b2533547-2a57-47be-93d3-2c91e602b24c).
- Criar objetos do tipo **access_rules** (regras de acesso) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/10800185/SztHW4xo#23dcfce9-42f4-46b9-b2b1-a03e7fe0aeb1).
- Criar objetos do tipo **scheduled_unlock_access_rules** (regras de acesso --> liberação agendada), que são responsáveis de vincular os objetos **scheduled_unlocks** com os objetos **access_rules**, conforme descrito neste [exemplo](https://documenter.getpostman.com/view/10800185/SztHW4xo#4940e7f7-4484-4d85-8d2a-d3acce5153c2).

Por fim, será necessário definir os intervalos de horário no qual a regra de acesso será válida, isso deverá ser feito através das três etapas descritas abaixo:

- Criar objetos do tipo **time_zones** (horários), um horário pode conter vários intervalos.
  Como criar objetos deste tipo está descrito neste [exemplo](https://documenter.getpostman.com/view/10800185/SztHW4xo#3c0ee600-8307-4647-a2a2-bb43f19da7c1).
- Criar objetos do tipo **time_spans** (intervalos), estes objetos contêm os intervalos de tempo, dias da semana e feriados que podem ser vinculados a um horário.
  Como criar objetos deste tipo está descrito neste [exemplo](https://documenter.getpostman.com/view/10800185/SztHW4xo#5810f4ff-d34d-443f-9d37-057bc62c758f).
- Criar objetos do tipo **access_rule_time_zones** (regras de acesso -> horários), que são responsáveis de vincular os objetos **time_zones** com os objetos **access_rules**, conforme descrito neste [exemplo](https://documenter.getpostman.com/view/10800185/SztHW4xo#5ae30f1a-0255-41d7-93d2-5801b6d92eea).
