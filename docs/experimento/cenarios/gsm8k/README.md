# Cenário 1 — Matemática (GSM8K-pt)

**Hipótese:** é o cenário em que o orquestrador *não* se encaixa. A resposta é **calculada** a partir dos números
da própria pergunta, e não **buscada** numa fonte. Além disso, cada problema tem uma redação própria.

## Base

- **GSM8K-pt** ([marlosb/GSM8K-pt](https://huggingface.co/datasets/marlosb/GSM8K-pt)), tradução para o português do
  GSM8K (Cobbe et al., 2021, *Training Verifiers to Solve Math Word Problems*, [openai/grade-school-math](https://github.com/openai/grade-school-math)).
- **Licença:** MIT, a mesma do original. O arquivo `gsm8k-pt-test.jsonl` é o split de teste da configuração `main`, com 1.318
  problemas (o original tem 1.319; a tradução tem um a menos). Foi baixado em 09/11/2026 pela API `datasets-server`
  do Hugging Face, com os campos `question` e `answer` mantidos como vieram.
- **Gabarito:** o número que vem depois de `####` na resposta.

## Como reproduzir

```bash
# análise estrutural da base inteira (sem LLM) e a amostra de 30 perguntas (semente 2026)
dotnet run --project src/Orquestracao.Experimento -- --cenario gsm8k --analisar --preparar --perguntas 30
# protótipo × linha de base, com o Claude conectado ao /mcp respondendo
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"gsm8k"}'
```

## Resultados

**Análise estrutural** ([analise.md](analise.md)), sobre as 1.318 perguntas:

| Medida | Valor |
|---|---:|
| Chaves distintas | **1.318 (uma por pergunta)** |
| Estruturas repetidas, mesmo trocando os números por `{n}` | **0** |
| Perguntas com "entidade" da fonte | 17 (1,3%), todas falsos positivos ("moedas de *ouro*", "balões de *hélio*", "porquinhos-da-*índia*") |
| Teto de reaproveitamento | **0%** |

**Experimento real** ([resultados-mcp.md](resultados-mcp.md)), com 30 perguntas e o Claude (Opus 5.5) como LLM via MCP:

| Métrica | Protótipo | Linha de base |
|---|---:|---:|
| Chamadas ao LLM de geração | 30 | 30 |
| Respostas pelo template | 0 | — |
| Templates salvos | 0 | — |
| Tokens (estimados) | 3.586 | 3.586 |
| Respostas corretas | 30/30 | 30/30 |

## Conclusões

1. **Ganho nulo:** nenhuma pergunta repete a estrutura de outra, nem com os números abstraídos. Por isso, nenhuma chega ao
   caminho do template. O protótipo se comporta como a linha de base, mais o custo do parser.
2. **Com o parser híbrido, o protótipo fica pior que a linha de base.** O parser híbrido chama o LLM sempre que o
   heurístico não acha entidade, o que acontece em 29 das 30 perguntas. Seriam cerca de 59 chamadas contra 30, quase o
   dobro. Esse número é calculado, não medido, porque o experimento usou o parser heurístico.
3. **Risco de falso positivo.** Na pergunta 3 ("moedas de ouro… moedas de prata"), o parser tomou "prata" como o
   elemento químico. Nenhum template foi salvo só porque a resposta (70) não coincide com nenhum dado da prata. Se
   coincidisse (o número atômico da prata é 47), um template sem sentido teria entrado na base. Conclusão: a extração
   precisa conferir se o tipo da entidade combina com o que a pergunta pede.
4. **Por que não encaixa:** o template guarda a *forma* da resposta e busca o *dado* numa fonte. Em matemática o dado
   não está em fonte nenhuma, porque ele é o resultado de um cálculo sobre a própria pergunta. Faltam as duas condições
   do protótipo: a **estrutura repetida** e o **dado buscável**.

> **Viés:** quem respondeu foi o próprio Claude, que resolveu os 30 problemas sem consultar o gabarito. Como protótipo e
> linha de base chamam o mesmo LLM com o mesmo prompt, as respostas são iguais. Isso é esperado aqui, porque o protótipo
> nunca saiu do caminho do LLM.
