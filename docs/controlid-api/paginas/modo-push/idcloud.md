<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/modo-push/idcloud/ -->

# iDCloud

O iDCloud é uma plataforma que permite que os dados do equipamento tais como logs de acesso, cadastros, etc, sejam disponibilizados para o cliente através da internet. Assim, o usuário passa a ter acesso e o controle do sistema a partir de qualquer lugar em que ele se encontre, bastando que ele tenha uma conexão com a internet e utilize os softwares de integração da Control iD.

Os dados poderão ser visualizados em qualquer dispositivo com acesso à internet, tais como desktops, notebooks, smartphones, tablets, entre outros.

Logo, o iDCloud permite integração do [iDSecure SaaS](https://www.controlid.com.br/controle-de-acesso/software/) e [RHiD](https://www.controlid.com.br/relogio-de-ponto/software-apuracao-de-ponto/) com os dispositivos da linha de controle de acesso.

O acesso ao iDCloud requer uma assinatura a ser realizada em contato com o time comercial da Control iD. Para maiores informações sobre o iDCloud, consulte [o site da Control iD](https://www.controlid.com.br/).

## Conexão com o iDCloud

A conexão com o iDCloud utiliza o [Modo Push](../modo-push/introducao-ao-push.md). Dessa forma, a configuração é realizada de maneira semelhante ao mostrado na seção de [Configurações](../configuracoes/parametros-configuracao.md#push_server).

Nesse caso, para configurar a conexão com o iDCloud via API, é necessário definir o servidor para a URL "https://push.idsecure.com.br/api". A configuração pode ser feita com a requisição a seguir.

```
{
    "push_server":{
        "push_remote_address":"https://push.idsecure.com.br/api",
        "push_request_timeout":"30000",
        "push_request_period":"5"
    }
}
```

## Código de verificação

Para o acesso, é necessário um código de verificação pré-configurado no dispositivo automaticamente. Se necessário, um novo código pode ser gerado e gravado no equipamento por uma requisição ao endpoint change_idcloud_code.fcgi. É possível conferir o código de verificação do dispositivo, para isso, é preciso utilizar o endpoint system_information.fcgi conforme documentado em [Obter informações do sistema](../sistema/obter-informacoes-do-sistema.md#obter-informacoes-do-sistema).

### GET /change_idcloud_code.fcgi

*Parâmetros*

Essa requisição não possui parâmetros.

*Resposta*

Reposta vazia.
