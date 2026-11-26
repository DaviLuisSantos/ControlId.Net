# 0029. Política de compatibilidade e critérios para a 1.0

Status: Aceita (2026-11-20). Complementa a 0006.

## Contexto

O pacote está em `0.10.0-alpha.1`. Na faixa 0.x o SemVer não promete nada, mas há quem use a biblioteca em produção, e as
mudanças da 0.10 (padrão de concorrência, exceções por desfecho, hora local) mostram que o contrato precisa estar escrito.
A validação de pacote e o PublicApiAnalyzers (ADR 0006) detectam a quebra, mas não dizem o que conta como quebra.

## Decisão

**SemVer.** Na 0.x, a versão menor pode quebrar, sempre com entrada no CHANGELOG e nas supressões da validação de pacote.
A partir da 1.0, só a versão maior quebra.

**O que conta como quebra:**
- remover ou renomear símbolo público, ou mudar assinatura, tipo de retorno ou valor de constante pública;
- mudar o tipo de exceção lançado numa condição documentada;
- mudar um padrão de opção que altera o comportamento (como o ADR 0025);
- mudar o significado de um valor devolvido (como a hora local, ADR 0028).

Não é quebra: símbolo novo, exceção nova que derive do tipo já lançado, correção de um comportamento que contradizia a
documentação.

**Prazo do `[Obsolete]`.** Um símbolo marcado `[Obsolete]` fica pelo menos uma versão menor com o aviso antes de sair. Na
1.0 ou depois, só sai na versão maior seguinte. A mensagem aponta o substituto.

**Crescimento de `IAccessClient`.** Adicionar membro a uma interface pública quebra quem a implementa (um mock, um
decorador). Como `AccessClient` é `sealed` e o uso esperado é consumir a interface, a política é: membros novos entram na
`IAccessClient` na versão menor, com aviso no CHANGELOG, e quem implementa a interface por conta própria assume o custo.
Para a 1.0, a interface deixa de crescer: capacidades novas vão para interfaces separadas por área, como já foi feito com
`IAccessProvisioningClient` (ADR 0024), e o `AccessClient` as implementa. As sobrecargas com parâmetros opcionais seguem o padrão do
ADR 0006.

**Critérios de saída do alpha (para a 1.0):**
1. Cassetes gravados nos firmwares 6.9.12 e 6.13.1 e conferidos com o simulador (ADR 0011). Issue #31.
2. Exceções por desfecho estáveis e sem tipo exato no consumo (ADR 0026). Issues #32 e #34.
3. Versões mínimas das dependências no `netstandard2.0` validadas no job `min-deps` (ADR 0030). Issue #38.
4. Uma rodada de validação num iDFace real e a medição com 1 mil e 10 mil usuários. Issues #33 e #35.
5. Investigação do PIN `UNKNOWN` e teste no firmware V6.26. Issue #36.
6. Validação em outros firmwares e modelos da linha de acesso. Issue #40.
7. `PublicAPI.Unshipped.txt` revisado e sem `[Obsolete]` remanescente da 0.x que a política não preveja. Issue a abrir.
8. Pelo menos uma versão beta sem quebra de API. Issue a abrir.

## Consequências

- Quem consome sabe o que esperar de cada tipo de versão, e o CHANGELOG ganha um critério para listar quebras.
- A 1.0 depende de hardware: os itens 1, 4, 5 e 6 não se fecham sem equipamento real.
- A regra da interface limita o crescimento de `IAccessClient`: o custo é separar por área o que for novo, e o ganho é
  não quebrar quem a implementa.
