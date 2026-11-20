<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/primeiros-passos/diagrama-de-sequencia/ -->

# Diagrama de Sequência

A API de dados da linha Acesso permite a incorporação de funções normalmente executadas no próprio equipamento em seu próprio site ou aplicativo. A lista abaixo identifica alguns dos diferentes tipos de funções que você pode utilizar usando a API.
Esta API usa o protocolo HTTP para a transferência de dados - arquitetura [REST](http://pt.wikipedia.org/wiki/REST). Cada comando é uma chamada HTTP **POST** com **Content-Type application/json** e possui uma URL própria. Seus parâmetros e seus retornos são passados preferencialmente por JSON, exceto em menção contrária.

Esse diagrama demonstra de forma simples o fluxo de requisições realizadas ao equipamento.
O exemplo abaixo mostra o processo de liberação do relê 1, para mais informações sobre os objetos, consulte [lista de objects](../objetos/lista-de-objetos.md).

> > > > > > > Fluxo de Request/Response com os equipamentos

![](../img/8dec0e766dea.png)
