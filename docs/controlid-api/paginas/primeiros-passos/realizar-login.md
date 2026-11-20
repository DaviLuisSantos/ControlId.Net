<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/primeiros-passos/realizar-login/ -->

# Realizar login

O login é o primeiro método que deve ser utilizado pois ele é responsável por gerar a sessão que será utilizada em todas as requisições para o dispositivo. Neste exemplo, a requisição irá criar a sessão e armazená-la na variável *session*.

Como exemplo, o comando **login** pode possuir a URL:

```
http://192.168.0.129/login.fcgi
```

e os seguintes parâmetros:

```
{
    login: 'admin',
    password: 'admin'
}
```

O retorno do método de login é um json como o exemplo abaixo:

```
{
    "session":"apx7NM2CErTcvXpuvExuzaZ"
}
```

Os seguintes requisitos aplicam-se as solicitações de dados da API da linha Acesso:

*Todos os comandos requerem uma sessão válida, exceto [session_is_valid](../gerenciamento-secao/verificar-validade-da-sessao.md) e [login](../gerenciamento-secao/fazer-login.md). Essa sessão deve ser reaproveitada para todas as requisições que você realizar para o equipamento.*

*O corpo da requisição deve possuir a codificação UTF-8. Caso os caracteres enviados não possuam nenhum caractere especial (ASCII), o corpo da requisição não precisa ser codificado.*

*Por padrão, a codificação utilizada por sistemas Windows é a Windows-1252 (também conhecida como CP-1252). Verificar como utilizar a codificação correta.*

#### Exemplo de login em **JavaScript**

O exemplo abaixo pode ser usados para a familiarização utilizando as ferramentas de desenvolvedor - **developer tools** - do navegador:

```
$.ajax({
  url: "/login.fcgi",
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
      login: 'admin',
      password: 'admin'
  }),
  success: function(data) {
      session = data.session;
  }
});
```

Para maiores detalhes sobre login, favor verificar o tópico [**GERENCIAMENTO SESSÃO**](../gerenciamento-secao/fazer-login.md).

Segue abaixo alguns exemplos que ilustram como a API pode ser usada em outras linguagens de programação. Nesses exemplos, o endereço **192.168.0.129**, quando aparecer, representa o endereço **IP** do equipamento.

*Atenção: a opção HTTP "[Expect: 100- continue](http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html)" deve estar desabilitada para as chamadas funcionarem. O único dos exemplos abaixo que o faz explicitamente é o em C#. Por favor, verifique a necessidade de fazê-lo em sua linguagem de preferência.*

#### Exemplo de login em **Delphi**

```
procedure TFrmTTWebserviceTester.Button1Click(Sender: TObject);
var
    lJSO : ISuperObject;
    lRequest: TStringStream;
    lResponse: String;
begin
    lJSO := SO('{"login": "admin", "password": "admin"}');
    lRequest := TStringStream.Create(lJSO.AsString, TEncoding.UTF8);
    try
        IdHTTP.Request.ContentType := 'application/json';
        IdHTTP.Request.Charset := 'utf-8';
        try
            lResponse := IdHTTP.Post('http://192.168.0.129/login.fcgi', lRequest);
            ShowMessage(lResponse);
        except
            on E: Exception do
                ShowMessage('Error on request:'#13#10 + E.Message);
        end;
    finally
        lRequest.Free;
    end;
    lJSO := nil;
end;
```

*Esse exemplo em Delphi utiliza a biblioteca de código aberto [Indy](http://www.indyproject.org).*

Este manual tem por objetivo auxiliar o usuário na configuração e operação de seu equipamento.

#### Exemplo de login em **Java**

```
try {
  URL url = new URL("http://192.168.0.129/login.fcgi");
  HttpURLConnection conn = (HttpURLConnection) url.openConnection();
  conn.setRequestMethod("POST");
  conn.setRequestProperty("Content-type", "application/json");
  conn.setDoInput(true);
  conn.setDoOutput(true);

  OutputStream os = conn.getOutputStream();
  os.write("{\"login\":\"admin\",\"password\":\"admin\"}".getBytes());

  if (conn.getResponseCode() != 200) {
  BufferedReader br = new BufferedReader(new InputStreamReader(conn.getErrorStream()));
    String output, result = "";
    System.out.println("Output from Server .... \n");
    while ((output = br.readLine()) != null) {
      result += output;
    }
    throw new RuntimeException("Failed : " + result);
  }

  BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream()));

  String output;
  System.out.println("Output from Server .... \n");
  while ((output = br.readLine()) != null) {
    System.out.println(output);
  }
  conn.disconnect();
} catch (Exception e) {
  e.printStackTrace();
}
```

#### Exemplo de login em **C#**

```
System.Net.ServicePointManager.Expect100Continue = false;
try
{
  var request = (HttpWebRequest)WebRequest.Create("http://192.168.0.129/login.fcgi");
  request.ContentType = "application/json";
  request.Method = "POST";

  using (var streamWriter = new StreamWriter(request.GetRequestStream()))
  {
    streamWriter.Write("{\"login\":\"admin\",\"password\":\"admin\"}");
  }

  var response = (HttpWebResponse)request.GetResponse();
  using (var streamReader = new StreamReader(response.GetResponseStream()))
  {
    Console.WriteLine(streamReader.ReadToEnd());
  }
}
catch (WebException e)
{
  using (WebResponse response = e.Response)
  {
    HttpWebResponse httpResponse = (HttpWebResponse)response;
    Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
    using (Stream data = response.GetResponseStream())
    using (var reader = new StreamReader(data))
    {
      string text = reader.ReadToEnd();
      Console.WriteLine(text);
    }
  }
}
```

#### Exemplo de login em **C#** c/ RestSharp

Para o desenvolvimento do exemplo está sendo utilizado a biblioteca [RestSharp](http://restsharp.org/ "Simple REST and HTTP API Client for .NET").

```
using System;
using RestSharp;
namespace ExampleToDevelopment
{
    class Program
    {
        public string Session()
        {
            var client = new RestClient("http://192.168.2.183/login.fcgi");
            var request = new RestRequest(Method.POST);
            request.AddHeader("content-type", "application/json");
            request.AddParameter("application/json", "{\"login\": \"admin\", \"password\": \"admin\"}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return response.Content;
        }
    }
}
```
