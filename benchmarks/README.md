# Benchmarks

Medições com [BenchmarkDotNet](https://benchmarkdotnet.org) do que roda em volume. Rodar:

```bash
dotnet run -c Release --project benchmarks/ControlId.Net.Benchmarks -- --filter '*'
```

## Parser de AFD

O AFD de um mês de uma empresa média passa de 100 mil marcações, e o REP entrega o arquivo inteiro de uma vez. O
benchmark gera um AFD válido da Portaria 671 (cabeçalho, empresa, empregado, N marcações com CRC e trailer) e compara:

- `ParseWholeFile`: `AfdParser.Parse(stream)`, com o arquivo na memória, todos os registros tipados e as conferências
  do trailer;
- `StreamRecords`: `AfdParser.ReadRecords(reader)`, registro a registro, como o `EnumerateAfdRecordsAsync` faz direto do
  REP.

### Otimização

A primeira medição mostrou cerca de 830 bytes alocados para cada linha de 50 caracteres e 1,3 µs por linha. A
linha passava por uma dúzia de substrings, pelo `DateTime.TryParseExact` (que interpreta o formato a cada chamada) e
por um CRC calculado bit a bit sobre uma cópia da linha. O que mudou:

- Os campos numéricos, de data e de hora são lidos por índice direto na linha, sem substrings. As strings das
  mensagens de erro só são montadas quando há erro.
- Data e hora são validadas dígito a dígito, com as mesmas regras do `TryParseExact`.
- O CRC-16/KERMIT usa uma tabela de 256 entradas, uma consulta por caractere em vez de 8 passos por bit, e é calculado
  sobre o trecho da linha, sem copiá-lo.

Antes de mexer no parser, 30 testes de casos de borda fixaram o comportamento, inclusive as mensagens de erro. Um teste
diferencial compara as datas com o `DateTime.TryParseExact` em 20 mil entradas aleatórias, e outro compara o CRC com
tabela com a definição bit a bit. Os mesmos testes passam no parser antigo e no novo.

### Resultados

AMD Ryzen 7 5700X, Windows 11, .NET 10.0.12, job padrão do BenchmarkDotNet 0.15.8.

| Método | Marcações | Antes | Depois | Ganho | Alocado antes | Alocado depois |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `ParseWholeFile` | 10.000 | 16,80 ms | 3,59 ms | 4,7× | 10,72 MB | 5,83 MB |
| `StreamRecords` | 10.000 | 13,55 ms | 2,14 ms | 6,3× | 8,33 MB | 3,44 MB |
| `ParseWholeFile` | 100.000 | 222,69 ms | 61,51 ms | 3,6× | 106,54 MB | 57,70 MB |
| `StreamRecords` | 100.000 | 133,48 ms | 20,63 ms | 6,5× | 83,17 MB | 34,34 MB |

No streaming, cada linha custa cerca de 0,2 µs. O que resta alocado é o mínimo do modelo: a linha lida, o objeto
`AfdPunch` e as strings que ele expõe (CPF e CRC). Por isso a memória do streaming não cresce com o arquivo: o total
alocado é o que o coletor recolhe no caminho, sem nada sobreviver à geração 0.
