<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/particularidade-dos-produtos/particulariade-terminais-control-id/ -->

# Particularidade Terminais Control iD

A API descrita nesta documentação aplicasse a todos os dispositivos de controle de acesso da Control iD, portanto, algumas das funções aqui descritas serão aplicáveis apenas para alguns produtos.
Este tópico visa descrever de forma geral as particularidades de cada produto, o que não for particularidade de alguma produto, será aplicável para todos os demais.

## iDAccess e iDFit

Nestes produtos, a ação para se abrir as portas é "door". Maiores detalhes em [Abertura Remota Porta e Catraca](../acoes/abertura-remota-porta-e-catraca.md).
O evento gerado pelo monitor para reportar que a porta foi aberta será notificado no *endpoint* **hostname:port/api/notifications/door**

## iDBlock

Neste produto, a ação para se liberar o giro da catraca para um dos lados é "catra". Maiores detalhes em [Abertura Remota Porta e Catraca](../acoes/abertura-remota-porta-e-catraca.md).
O evento gerado pelo monitor para reportar giro na catraca será notificado no *endpoint* **hostname:port/api/notifications/catra_event**

## iDBox

Neste produto, a ação para se abrir as portas é "door" e o mesmo possui 4 (quatro) portais no total. Maiores detalhes em [Abertura Remota Porta e Catraca](../acoes/abertura-remota-porta-e-catraca.md).
O evento gerado pelo monitor para reportar que a porta foi aberta será notificado no *endpoint* **hostname:port/api/notifications/door**

## iDFlex, iDAccess Pro e iDAccess Nano

Nestes produtos, a ação para se abrir as portas é "sec_box". Maiores detalhes em [Abertura Remota Porta e Catraca](../acoes/abertura-remota-porta-e-catraca.md).
O evento gerado pelo monitor para reportar que a porta foi aberta será notificado no *endpoint* **hostname:port/api/notifications/secbox**

## iDFlex e iDAccess Nano

Nestes produtos, para se habilitar o modo enterprise, será necessário fazer o upgrade conforme descrito em [upgrade](../particularidade-dos-produtos/update-idflex-idaccess-nano.md).

## iDUHF

Nestes produtos, a ação para se abrir o relay interno é "door" e para se abrir o relay externo (MAE) é "sec_box". Maiores detalhes em [Abertura Remota Porta e Catraca](../acoes/abertura-remota-porta-e-catraca.md).
O evento gerado pelo monitor para reportar que o relay interno foi aberto será notificado no *endpoint* **hostname:port/api/notifications/door** e o evento gerado pelo monitor para reportar que o relay externo foi aberto será notificado no *endpoint* **hostname:port/api/notifications/secbox**

## iDFace

Neste produto, a ação para se abrir as portas é "sec_box". Maiores detalhes em [Abertura Remota Porta e Catraca](../acoes/abertura-remota-porta-e-catraca.md).

No iDFace, é possível configurar o período entre ativações do relé enquanto o usuário reconhecido continuar à frente do equipamento. Para isso, basta enviar uma requisição ***set_configuration*** ([Modificar Configurações](../acoes/abertura-remota-porta-e-catraca.md)) modificando o parâmetro *max_identified_duration* do módulo *face_id* para o valor em milissegundos de intervalo desejado. Não é recomadável aplicar valores abaixo do intervalo em que o relé já se mantém aberto. Quando o parâmetro é configurado em zero, o relé não será aberto periodicamente, sendo necessário que o usuário saia da frente da câmera e espere o fim do streaming para uma nova identificação e abertura de porta.

Também é possível configurar o iDFace para desligar a tela quando o equipamento está ocioso. Para isso basta modificar o parâmetro *screen_always_on* para "0", fazendo com que a tela desligue até que alguém apareça à frente do display ou toque na tela. Para que a tela permaneça sempre ativa, basta modificar o parâmetro para "1". Isso pode ser feito com uma requisição ***set_configuration*** ([Modificar Configurações](../configuracoes/modificar-configuracoes.md)).

## iDFace Max

Neste produto, é possível redimencionar a a captura de tela. Essa funcionalidade permite definir uma escala para a imagem capturada pela câmera através da API, o que pode otimizar o tempo de resposta e o tamanho do arquivo. Para isso, basta fazer uma requisição com o parâmetro ***screenshot_resize***, o qual define este comportamento. Ele aceita um número de ponto flutuante que funciona como um multiplicador para ambas as dimensões da imagem. O valor padrão é 0.42.

### Exemplo de requisição

Esta requisição configura o redimensionamento da imagem para 75% do seu tamanho original.

```
$.ajax({
    url: "set_configuration.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(
        {
            "general": {
                "screenshot_resize": "0.75"
            }
        }
    )
});
```

## Limitação do número de templates

Número de templates (digitais) que podem ser armazenadas no equipamento (em modo standalone e contingência):

- **iDAccess, iDFit, iDBlock**: 2000 templates;
- **iDFlex, iDAccess Pro, iDAccess Nano**: 6000 templates;
