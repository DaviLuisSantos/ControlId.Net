# 0003. Sem strong naming

Status: Aceita (2026-10-30)

## Contexto

O pacote tem alvo `netstandard2.0` e roda no .NET Framework 4.8. A Microsoft ainda recomenda strong naming para bibliotecas
usadas no .NET Framework, porque um assembly assinado só pode referenciar assemblies assinados. Por outro lado, adotar
depois é uma quebra para quem já usa, e tirar também é: a decisão é praticamente definitiva.

## Decisão

Não assinar os assemblies.

## Consequências

- O pacote funciona normalmente no .NET Framework, exceto em projetos que exigem que todas as referências sejam assinadas.
- Nenhum usuário pediu isso até agora. Se alguém precisar, a decisão é revista e a mudança sai numa versão que sobe o número
  do meio, com aviso no CHANGELOG.
- Strong naming não é segurança (a chave fica no repositório); a integridade dos pacotes vem da publicação por OIDC e da
  atestação de proveniência (ADR 0002).
