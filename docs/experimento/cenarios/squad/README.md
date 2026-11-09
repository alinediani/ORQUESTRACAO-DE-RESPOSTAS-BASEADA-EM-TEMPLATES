# Cenário 4 — Perguntas sobre textos (SQuAD-pt)

**Hipótese:** o dado está disponível, mas no **texto** que acompanha a pergunta, e não numa base chave-valor. Cada
pergunta pede um trecho diferente de um parágrafo diferente.

## Base

- **SQuAD-pt_BR v1.1** ([vsvasconcelos/SQuAD-pt_BR-V1.1](https://huggingface.co/datasets/vsvasconcelos/SQuAD-pt_BR-V1.1)),
  tradução para o português do SQuAD v1.1 (Rajpurkar et al., 2016, *SQuAD: 100,000+ Questions for Machine Comprehension
  of Text*). As perguntas foram escritas por pessoas sobre parágrafos da Wikipédia, e a resposta é um trecho do parágrafo.
- **Licença:** o card do Hugging Face diz Apache-2.0, mas o SQuAD original é **CC BY-SA 4.0**. Por ser derivado, este
  recorte é tratado como CC BY-SA 4.0.
- **O arquivo:** `squad-pt-validation.jsonl` traz o split `validation` (9.808 perguntas), baixado em 09/11/2026 e agrupado
  por parágrafo (`title`, `context`, `qas`). São 8.612 parágrafos de 490 artigos; a tradução foi feita pergunta a
  pergunta, por isso quase cada pergunta tem o seu parágrafo.
- **Gabarito:** o trecho do parágrafo.

## Como reproduzir

```bash
dotnet run --project src/Orquestracao.Experimento -- --cenario squad --analisar --preparar --perguntas 30
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"squad"}'
```

No experimento, o LLM recebe `Texto: <parágrafo>\n\nPergunta: <pergunta>`. Sem o parágrafo não dá para responder. A
análise estrutural usa só a pergunta.

## Resultados

**Análise estrutural** ([analise.md](analise.md)), sobre as 9.808 perguntas, sem o parágrafo, com a fonte padrão:

| Medida | Valor |
|---|---:|
| Perguntas com entidade da fonte | 610 (6,2%) |
| Chaves distintas | 9.803 |
| **Teto de reaproveitamento** | **1 pergunta (0,01%)** |
| Intenções (3 primeiras palavras) que aparecem 10 vezes ou mais | 30,0% ("em que ano": 280; "que tipo de": 238) |

As poucas chaves repetidas são a mesma pergunta sobre o mesmo artigo. "Quanto tempo durou o hiato?" aparece duas
vezes, ambas sobre a Beyoncé e com a mesma resposta ("nove meses"). Só nesses casos raros o reaproveitamento acertaria.

**Experimento real** ([resultados-mcp.md](resultados-mcp.md)), com 30 perguntas e o Claude (Opus 5.5) como LLM via MCP:

| Métrica | Protótipo | Linha de base |
|---|---:|---:|
| Chamadas ao LLM de geração | 30 | 30 |
| Respostas pelo template | 0 | — |
| Templates salvos | 0 | — |
| Tokens (estimados) | 8.445 | 8.445 |
| Corretas pela correção automática | 29/30 | 29/30 |

A única divergência (pergunta 1) é de formato. A resposta "Roderick Maclean não foi considerado culpado por insanidade"
não contém literalmente o gabarito "ele não foi…", mas está certa: são 30 de 30 na conferência manual.

Em 8 das 30 perguntas, o parser achou uma entidade da fonte **dentro do parágrafo**, e não no assunto da pergunta:
"Vietnã" numa pergunta sobre os Cholas; "Reino Unido" numa sobre o Ministério da Defesa. A chave dessas perguntas inclui
o parágrafo inteiro (772 caracteres em média) e nunca se repete. Nenhum template foi salvo, porque nenhuma resposta
continha um atributo dessas entidades.

## Conclusões

1. **Ganho nulo.** A pergunta é sempre nova e o texto também: a combinação dos dois não se repete.
2. **O dado é buscável, mas no lugar errado para o protótipo.** A fonte do protótipo é uma base chave-valor de
   entidades e atributos. Aqui, o dado é um trecho de um texto livre. O que resolve este cenário é a recuperação de
   trechos (*retrieval-augmented generation*, RAG) com leitura pelo LLM. O orquestrador por templates não substitui isso.
3. **O parser precisa separar a pergunta do material de apoio.** Com o parágrafo junto, ele procura entidades no texto
   todo e erra o assunto (8 de 30). Se a chave usasse só a pergunta, perguntas genéricas como "Quanto tempo durou o
   hiato?" ou "Que discurso ele deu?" juntariam textos diferentes na mesma chave. Nos dois casos, o reaproveitamento por
   chave não serve quando a resposta depende de um contexto que muda a cada pergunta.
4. **A qualidade do LLM é alta** (30 de 30) quando o dado está no texto. Neste cenário, o LLM faz bem justamente o que
   o template não sabe fazer.

> **Viés:** quem respondeu foi o próprio Claude, a partir do parágrafo, sem ver o gabarito.
