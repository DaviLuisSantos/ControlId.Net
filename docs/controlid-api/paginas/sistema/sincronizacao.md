<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/sistema/sincronizacao/ -->

# Importar/Exportar dados

É possível importar e exportar dados de um equipamento a fim de realizar uma sincronização entre disposistivos. É possível exportar somente dados de usuários, somente logs de acesso, realizar sincronização ou um backup completo. É possível também importar através de todas essas opções com exceção de "somente logs de acesso". O processo de importação/exportação pode ser realizado através da interface Web ou da GUI do dispositivo. Essas formas de sincronização são descritas a seguir:

- **Só usuários:** Exportar/Importar somente os dados dos usuários, digitais e cartões.
- **Só logs de acesso:** Exportar somente os logs de acesso.
- **Sincronização:** Exportar/Importar todos os dados contidos no equipamento, exceto logs de acesso. Essa opção deve ser utilizada para transferir dados de um equipamento para outro.
- **Backup completo:** Exportar/Importar todos os dados contidos no equipamento. Esta opção deve ser utilizada como backup dos dados de um mesmo equipamento.

Ao exportar usuários, sincronização ou backup completo será gerado um arquivo zip (*users.zip*, *sync.zip*,*logs.zip*) contendo uma pasta com o nome *user_images* e um arquivo csv correspondente. A pasta *user_images* contém as imagens dos usuários cadastrados. O arquivo csv terá o nome *users.csv* caso seja um arquivo de dados de usuários, *sync.csv* caso seja um arquivo de sincronização, ou *backup.csv* caso seja um arquivo de backup. A exportação de logs de acesso gera um arquivo csv nomeado *logs.csv*. Tais arquivos zip gerados na exportação de dados de um equipamento devem ser usados no processo de importação correspondente no dispositivo alvo.

**Atenção: É muito importante sempre atualizar o dispositivo antes de realizar a exportação de seus arquivos de sincronização. Não há garantia de que um dispositivo com uma versão mais recente de firmware consiga importar um arquivo de sincronização gerado por um dispositivo em uma versão mais antiga.**

Desta forma, para salvaguardar os dados de um dispositivo, pode-se exportar um arquivo de backup e, caso o dispositivo tenha perdido os dados, é possível utilizar o arquivo gerado previamente na exportação para recuperar os dados ao importá-lo. Já o processo de sincronização é mais adequado para passar as configurações de um dispositivo para outro, já que os logs de acesso não são passados. Basta então exportar o arquivo de sincronização do dispositivo base e fazer com que o dispositivo alvo importe esse arquivo.

É importante ressaltar que os arquivos de exportação/importação (*users.zip*, *sync.zip*,*logs.zip*) não devem ser alterados manualmente. Qualquer não conformidade com a formatação esperada pode prejudicar o processo de importação no dispositivo alvo.

Os arquivos zip de exportação/importação tem a mesma estrutura de caminho e nomes de arquivos para os dispositivos de firmware linhas V5 e V6. Contudo, os arquivos csv da linha V6 são diferentes da linha V5. Outra diferença é que na linha V5 pode ser que nenhum dos usuários tenha foto, fazendo com que a pasta *user_images* fique vazia, o que geralmente não ocorre na linha V6.

# Sincronização

Para gerar um arquivo *.csv* de exportação, é necessário utilizar o endpoint *export_objects*.

### POST /export_objects.fcgi

*Parâmetros*

- **objects** *(array de objetos JSON)* : Lista de objetos a serem coletados e incluídos no arquivo de exportação.
- **object** *(string)* : Tipo do objeto a ser criado. Consulte o tópico [Lista de Objetos](../objetos/lista-de-objetos.md) para uma lista completa dos tipos de objeto disponíveis.
- **columns** *(objeto JSON)* : Esse campo determina as colunas que serão exportadas. As colunas deverão ser descritas em conjunto com o objeto, como em `{"object":"c_users","columns":["id","user_id","cpf"]}`.

*Resposta*

#### Exemplo de requisição

Exportando dados.

```
$.ajax({
  url: "/export_objects.fcgi?session=" + session,
  type: 'POST',
  contentType: 'application/json',
  data: JSON.stringify({
      "objects":[
          {"object":"users"},
          {"object":"cards","columns":["value","user_id"]},{"object":"user_roles"},
          {"object":"c_users","columns":["id","user_id","cpf"]}
        ]
    })
});
```
