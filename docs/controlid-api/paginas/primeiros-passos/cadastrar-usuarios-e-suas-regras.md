<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/primeiros-passos/cadastrar-usuarios-e-suas-regras/ -->

# Cadastrar usuários e suas regras

Este tópico descreve como criar as regras de acesso que serão usadas pelo equipamento após uma tentativa de acesso dos modos possíveis (via leitor biométrico, cartão de proximidade, id e senha, etc). Note que estas regras de acesso aplicam-se somente quando o dispositivo estiver em modo *standalone*, pois ao operar em algum [modo online](../modos-de-operacao/introducao-aos-modos-de-operacao.md) as regras de acesso deverão ser gerenciadas pelo próprio servidor de acesso\* (exceto quando o dispositivo entrar em modo de contingência após falhas na comunicação com o servidor de acesso).

Um mesmo usuário pode possuir diversas regras de acesso de forma direta e indireta.
Regras de acesso diretas, são aquelas que são vinculadas diretamente ao usuário e regras de acesso indiretas são aquelas que são aplicadas ao usuário pelo fato do mesmo estar presente em um determinado grupo de acesso, como exemplo um departamento.

Para criar uma regra de acesso indireta, ou seja, criar um grupo de usuários e atribuir regras de acesso para esse grupo, será necessário seguir as etapas abaixo, e **este é o fluxo de trabalho recomendado** em aplicações de controle de acesso:

- Criar objetos do tipo **users** (usuários) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#76b4c5d7-e776-4569-bb19-341fdc1ccb7f).
- Criar objetos do tipo **groups** (grupos) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#55f143e7-00a9-4021-b5d8-a37c479d8602).
- Criar objetos do tipo **user_groups** (grupos de usuário), para adicionar o **usuário** a um **grupo**, conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#e544b4c3-a01d-43c1-a619-2b7303d75d98).
- Criar objetos do tipo **access_rules** (regras de acesso) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#6aaea205-9eec-4fb1-a8e1-e9e296c8bb3f).
- Criar objetos do tipo **group_access_rules** (regras de acesso --> grupo), que são responsáveis de vincular os objetos **groups** com os objetos **access_rules**, conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#fab9704a-4964-4b04-af1c-a934eb622f45).

Por fim, será necessário definir os intervalos de horário no qual a regra de acesso será válida, isso deverá ser feito através das três etapas descritas abaixo:

- Criar objetos do tipo **time_zones** (horários), um horário pode conter vários intervalos.
  Como criar objetos deste tipo está descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#6f69b26e-94d7-410d-ab9e-5c175397ca0b).
- Criar objetos do tipo **time_spans** (intervalos), estes objetos contém os intervalos de tempo, dias da semana e feriados que podem ser vinculados a um horário.
  Como criar objetos deste tipo está descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#85c50864-dff0-43b6-ba55-70db3d018bfd).
- Criar objetos do tipo **access_rule_time_zones** (regras de acesso -> horários), que são responsáveis de vincular os objetos **time_zones** com os objetos **access_rules**, conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#c525366d-685f-4466-8e67-2a9ae2b19a2a).

---

Para casos especiais, também é possível criar uma regra de acesso direta, ou seja, uma regra exclusiva para um usuário, no entanto isto não é recomendado e deve ser usado apenas para tratar exceções, se este for o caso basta seguir os passos abaixo:

- Criar **objetos** do tipo **users** (usuários) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#76b4c5d7-e776-4569-bb19-341fdc1ccb7f).
- Criar **objetos** do tipo **access_rules** (regras de acesso) conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#6aaea205-9eec-4fb1-a8e1-e9e296c8bb3f).
- E por fim criar **objetos** do tipo **user_access_rules** (regras de acesso --> usuário), que são responsáveis de vincular os objetos **users** com os objetos **access_rules**, conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#653206a2-8ee3-4eeb-9bcc-322391b6bf0d).

---

Em ambos os casos acima (regra de acesso direta e indireta), após criar as regras de acesso, será necessário vinculá-las a um **portal** para indicar qual porta deverá ser aberta quando a regra de acesso for atendida.

Para consultar os portais existentes em um determinado produto da linha de acesso, pode ser utilizado este [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#00155afb-f5aa-4615-b15b-ed43a57d5836).

- Por fim, para definir qual porta será aberta quando a regra de acesso for atendida, criar um objetos do tipo **portal_access_rules** conforme descrito neste [exemplo](https://documenter.getpostman.com/view/7260734/S1LvX9b1?version=latest#222cf33c-988f-48a0-9e87-20e31fc7571c).
