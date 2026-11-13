# 0014. Manter o alvo net8.0 depois do fim do suporte ao .NET 8

Status: Aceita (2026-11-12)

## Contexto

As bibliotecas têm os alvos `net8.0` e `netstandard2.0`, e o pacote ASP.NET Core só `net8.0`. O .NET 8 saiu de suporte
em 2026-11-10; o único LTS suportado é o .NET 10. As dependências já foram para a 10.x ([ADR 0010](0010-dependencias-no-lts-suportado.md)).

As opções eram manter `net8.0`, somar `net10.0` ou trocar `net8.0` por `net10.0`.

## Decisão

Manter `net8.0` (e `netstandard2.0`) como alvos, sem somar `net10.0`.

- Um build `net8.0` roda no .NET 10 sem perda: os testes já rodam em `net10.0` contra ele, inclusive o pacote ASP.NET
  Core sobre o ASP.NET Core 10.
- Equipamentos de controle de acesso ficam anos em produção, e as aplicações que os integram nem sempre acompanham o
  runtime. Trocar o alvo deixaria quem ainda está no .NET 8 no build `netstandard2.0`, e o pacote ASP.NET Core passaria
  a exigir o .NET 10.
- Somar `net10.0` traria APIs novas (ex.: `ArgumentNullException.ThrowIfNull`), mas o código já precisa funcionar no
  `netstandard2.0`, então elas entrariam atrás de `#if` sem ganho para quem usa o pacote.

## Consequências

- Nenhuma mudança para quem usa o pacote.
- As supressões de análise que sugerem APIs ausentes no `netstandard2.0` continuam.
- A decisão é revista quando sair o .NET 12 (próximo LTS) ou quando uma API do .NET 10 trouxer ganho real para quem usa
  o pacote.
