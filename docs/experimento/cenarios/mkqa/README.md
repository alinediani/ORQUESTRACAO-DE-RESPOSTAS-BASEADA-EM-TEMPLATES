# Cenário 2 — Perguntas factuais reais (MKQA)

**Hipótese:** o cenário fica no meio do caminho. As perguntas são factuais, e o dado pode ser buscado numa fonte. Mas
são buscas reais, de cauda longa: cada uma fala de um assunto diferente, e a estrutura quase nunca se repete.

## Base

- **MKQA** (Longpre, Lu e Daiber, 2021, *MKQA: A Linguistically Diverse Benchmark for Multilingual Open Domain Question
  Answering*, TACL), [apple/ml-mkqa](https://github.com/apple/ml-mkqa). São 10.000 perguntas reais de busca do Google
  (vindas do Natural Questions), traduzidas por humanos para 26 línguas, com respostas tipadas.
- **Licença:** CC BY-SA 3.0. O arquivo `mkqa-pt.jsonl` traz só o recorte em português (`query_en`, `query_pt` e
  `answers_pt`), tirado de `dataset/mkqa.jsonl.gz` em 09/11/2026. Como é material derivado, **fica sob a mesma licença
  CC BY-SA 3.0**.
- **Gabarito:** o texto e os apelidos da resposta, juntos por ` || `; qualquer um deles vale. A amostra só usa perguntas
  com resposta curta que dá para conferir: entidade, número, número com unidade e ano. Ficam de fora 3.242 perguntas
  sem resposta curta ("unanswerable" e "long_answer") e as datas completas.

## Como reproduzir

```bash
dotnet run --project src/Orquestracao.Experimento -- --cenario mkqa --analisar          # as 10.000 perguntas, sem LLM
dotnet run --project src/Orquestracao.Experimento -- --cenario mkqa --preparar --perguntas 30
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"mkqa"}'
```

## Resultados

**Análise estrutural** ([analise.md](analise.md)), sobre as 10.000 perguntas, com a fonte padrão do protótipo (58 entidades):

| Medida | Valor |
|---|---:|
| Perguntas com entidade da fonte | 860 (8,6%) |
| Chaves distintas | 9.944 |
| **Teto de reaproveitamento** | **24 perguntas (0,2%)** |
| A chave que mais se repete | `idade legal para beber {pais}`, 5 vezes |
| Intenções (3 primeiras palavras) que aparecem 10 vezes ou mais | 37,3% das perguntas ("quem canta a": 162; "quem escreveu a": 111) |

**Experimento real** ([resultados-mcp.md](resultados-mcp.md)), com 30 perguntas e o Claude (Opus 5.5) como LLM via MCP:

| Métrica | Protótipo | Linha de base |
|---|---:|---:|
| Chamadas ao LLM de geração | 30 | 30 |
| Respostas pelo template | 0 | — |
| Templates salvos | 0 | — |
| Corretas pela correção automática | 20/30 | 20/30 |

**As 10 respostas que não batem com o gabarito**, conferidas uma a uma:

| Tipo | Perguntas | Comentário |
|---|---|---|
| Formato ou sinônimo (a resposta está certa) | 1, 7, 8, 17 | "134 °F" × "134.1"; "quarta temporada" × "4"; "60 por 80" × "6080"; "barragens" × "represa" |
| Gabarito desatualizado | 26, 27 | O último impeachment foi o de Trump (2019 e 2021), mas a base, de 2019, diz Bill Clinton. A idade do vocalista muda todo ano |
| Divergência real | 2, 10, 24, 25 | Duas perguntas ambíguas ("That's What I Like About You" tem mais de uma canção; "o reencontro" tem mais de um filme) e duas estimativas (calorias, velocidade do som) |

Descontando formato e desatualização, o acerto ajustado fica em cerca de 26 de 30. O número é o mesmo nas duas
estratégias, porque o protótipo nunca saiu do caminho do LLM.

## Conclusões

1. **A cauda longa anula o reaproveitamento.** Mesmo nas perguntas com entidade da fonte, só 0,2% repetem a chave.
   Perguntas reais de busca quase nunca voltam com a mesma estrutura sobre a mesma família de dados.
2. **A intenção se repete, a entidade não.** "Quem canta", "quem escreveu" e "quem interpreta" voltam centenas de vezes,
   mas sobre músicas, livros e filmes sem fim. Para o template funcionar, a fonte precisaria cobrir esse mundo aberto (algo
   como o Wikidata), e o parser precisaria reconhecer entidades de tipos abertos. As duas coisas estão fora do protótipo.
3. **O gabarito desatualizado mostra a vantagem da fonte externa.** Uma resposta "congelada", seja do LLM ou do gabarito
   de 2019, envelhece. O desenho do protótipo, em que o template guarda a forma e o dado vem da fonte, responderia certo
   assim que a fonte fosse atualizada. Isso só vale quando o dado está numa fonte que alguém mantém, o que não é o caso
   de perguntas abertas.
4. **A correção automática por gabarito subestima o acerto** em perguntas abertas, por formato, sinônimo e tradução.
   Para T06 neste cenário, a avaliação humana (`avaliacao-humana.csv`) é necessária.

> **Viés:** quem respondeu foi o próprio Claude, a partir do próprio conhecimento, sem ver o gabarito. As respostas das
> duas estratégias são iguais porque o protótipo encaminhou as 30 perguntas ao LLM.
