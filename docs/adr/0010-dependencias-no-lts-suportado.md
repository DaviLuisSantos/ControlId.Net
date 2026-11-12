# 0010. Dependências de runtime no LTS mais antigo ainda suportado

Status: Aceita (2026-11-12)

## Contexto

O alvo `netstandard2.0` depende de `System.Text.Json` e `Microsoft.Bcl.AsyncInterfaces`, e o pacote de DI depende de
`Microsoft.Extensions.Http` e `Microsoft.Extensions.Options.ConfigurationExtensions`. Todas estavam na 8.x, e o Dependabot
propôs subir para a 10.x.

Uma biblioteca define o **mínimo** das suas dependências: o NuGet resolve a versão mais alta pedida no grafo. Pedir uma
major mais nova do que o necessário arrasta quem usa o pacote para ela. Por outro lado, ficar numa major sem suporte deixa
quem usa o `netstandard2.0` (o .NET Framework) sem correções de segurança, porque nesse caso as dependências vêm do pacote,
não do framework.

O .NET 8 saiu de suporte em 2026-11-10 e o .NET 9 também. O único LTS suportado é o .NET 10, cujos pacotes 10.x continuam
com alvos `net8.0` e `netstandard2.0`, então funcionam em todos os ambientes que o ControlId.Net atende.

## Decisão

- As dependências de runtime das bibliotecas ficam na major do LTS mais antigo ainda suportado. Hoje é a 10.x.
- O Dependabot ignora atualizações major dessas dependências, e patches e minors continuam vindo. A major sobe à mão,
  quando o LTS de referência sair de suporte, com registro no CHANGELOG.
- Os analisadores de build e os pacotes de teste ficam sempre na versão mais recente.

## Consequências

- Quem usa o .NET Framework recebe a `System.Text.Json` com correções de segurança.
- Uma aplicação em .NET 8 que instale o pacote passa a usar as extensões 10.x, que suportam `net8.0`.
- Uma nova major do .NET (11, 12) não chega por PR automático: a troca da referência é uma decisão explícita.
- O alvo `net8.0` das bibliotecas continua, porque roda no .NET 10. Trocá-lo por `net10.0` é outra decisão.
