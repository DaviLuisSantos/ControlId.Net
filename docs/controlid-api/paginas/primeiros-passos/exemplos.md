<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/primeiros-passos/exemplos/ -->

# Exemplos

Os exemplos descritos abaixo são apenas uma fração dos contidos nesta documentação, e exemplos em diferentes linguagens também pode ser encontrados em nosso repositório no GitHub:
<https://github.com/controlid/integracao/tree/master/Controle%20de%20Acesso>

## Coletar logs

O método [load_objects](../objetos/carregar-objetos.md) é utilizado para coletar informações do dispositivo, nesse exemplo ele foi utilizado para coletar os logs de acesso que estavam armazenados no equipamento.

#### Exemplo **C#**

Para o desenvolvimento do exemplo está sendo utilizado a biblioteca [RestSharp](http://restsharp.org/ "Simple REST and HTTP API Client for .NET").

```
public string GetAccessLogs()
{
  var client = new RestClient("http://192.168.2.183/load_objects.fcgi");
  var request = new RestRequest(Method.POST);
  request.AddHeader("cookie", "session=o7JRuZQckN10lz66MHfX69iY");
  request.AddHeader("content-type", "application/json");
  request.AddParameter("application/json", "{\n  \"object\":\"access_logs\"\n}", ParameterType.RequestBody);
  IRestResponse response = client.Execute(request);
  return response.Content;
}
```

#### Exemplo **NodeJS**

Para o desenvolvimento do exemplo está sendo utilizado o módulo [Request](https://www.npmjs.com/package/request "Request - Simplified HTTP client").

```
var request = require("request");
var options = { 
method: 'POST',
url: 'http://192.168.2.183/load_objects.fcgi',
headers: 
{ 
  cookie: 'session=QnnlmLcEBCE06mwKkh/7SOEM',
  'content-type': 'application/json' 
},
body: 
{ 
  object: 'access_logs'
},
json: true 
};
request(options, function (error, response, body) {
  if (error) throw new Error(error);
      console.log(body);
});
```

## Acionar relê

O método [execute_actions](../acoes/abertura-remota-porta-e-catraca.md#executar-acao) é responsável por realizar a execução de comandos no dispositivo, neste exemplo ele foi utilizado para abrir a porta ao liberar o *relê 1*.
O exemplo abaixo não é aplicável para o iDFlex, iDBlock, iDAccess Pro e iDAccess Nano, porque para estes produtos os parâmetros são diferentes.

#### Exemplo **C#**

Para o desenvolvimento do exemplo está sendo utilizado a biblioteca [RestSharp](http://restsharp.org/ "Simple REST and HTTP API Client for .NET").

```
public string ExecuteAction()
{
  var client = new RestClient("http://192.168.2.183/execute_actions.fcgi?session=zm3WJhwbFawo1wX9B4YRnLY4");
  var request = new RestRequest(Method.POST);
  request.AddHeader("content-type", "application/json");
  request.AddParameter("application/json", "{\n\t\n\t\"actions\": [ { \"action\": \"door\", \"parameters\": \"door=1\" } ]\n\t\n}", ParameterType.RequestBody);
  IRestResponse response = client.Execute(request);
  return response.Content;
}
```

#### Exemplo **NodeJS**

Para o desenvolvimento do exemplo está sendo utilizado o módulo [request](https://www.npmjs.com/package/request "Request - Simplified HTTP client").

```
var request = require("request");
var options = { 
method: 'POST',
url: 'http://192.168.2.183/execute_actions',
headers: 
{ 
  cookie: 'session=QnnlmLcEBCE06mwKkh/7SOEM',
  'content-type': 'application/json'
},
body: 
{
  "actions": [ { "action": "door", "parameters": "door=1" } ]
},
json: true
};
request(options, function (error, response, body) {
  if (error) throw new Error(error);
    console.log(body);
});
```
