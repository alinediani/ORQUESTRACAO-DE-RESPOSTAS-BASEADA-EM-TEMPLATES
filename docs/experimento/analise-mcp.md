# Análise do experimento real (LLM via MCP)

Os números estão em [`resultados-mcp.md`](resultados-mcp.md); as 150 perguntas e as duas respostas de cada uma, em [`resultados-mcp.json`](resultados-mcp.json).

## Como foi feito

- **Conjunto:** as mesmas 150 perguntas do experimento simulado (semente 2026), em seis famílias de mesma estrutura: capital (51), moeda (26), símbolo químico (24), número atômico (19), idioma (15) e continente (15).
- **LLM de geração:** o Claude (Opus 5.5), numa sessão do Claude Code ligada ao servidor MCP do protótipo. O servidor rodou o experimento (`POST /api/experimento {"llm": "mcp"}`), e cada chamada ao LLM virou um turno pendente no `/mcp`. O modelo buscou os turnos com `proxima_vez` e respondeu com `responder`, a partir do próprio conhecimento, seguindo o system prompt de geração ("uma única frase completa e factual"). Não consultou a base de fatos para responder.
- **Ordem:** primeiro o protótipo, uma pergunta por vez, porque os templates se formam na ordem; depois a linha de base, com as 150 perguntas pendentes ao mesmo tempo.
- **Parser:** o heurístico (sem LLM).
- **Correção automática:** a resposta está correta quando traz o valor do gabarito (palavra exata, sem diferenciar maiúsculas e acentos).

## Resultados

| | Protótipo | Linha de base |
|---|---:|---:|
| Chamadas ao LLM de geração | **6** | 150 |
| Perguntas montadas pelo template | 144 (96%) | — |
| Tokens estimados | 413 | 10.254 |
| Custo estimado (preço do Opus 5.5) | US$ 0,0025 | US$ 0,06 |
| Respostas com o dado correto (gabarito) | 150 (100%) | 150 (100%) |

- **T01:** as seis famílias geraram uma chave cada.
- **T02:** a primeira pergunta de cada família foi ao LLM, e todas as seis respostas viraram template.
- **T05:** 6 chamadas contra 150, uma redução de 96%. É o mesmo número da simulação, porque ele depende só de a primeira resposta de cada família ser reaproveitável.

## O que o gabarito não vê: a forma da resposta

Pelo gabarito, as duas estratégias empataram nas 150 perguntas. Mas **33 das 144 respostas montadas pelo template diferem do texto que o LLM escreveu** para a mesma pergunta. Em todas elas o dado está certo; o que muda é a forma:

| Tipo | Quantas | Exemplo (protótipo × LLM) |
|---|---:|---|
| Concordância do artigo (moeda) | 20 | "A moeda do Japão é **a** iene." × "… é **o** iene." |
| Artigo omitido (capital) | 5 | "A capital do Egito é Cairo." × "… é **o** Cairo." |
| Resposta menos detalhada | 4 | "Na Nigéria fala-se inglês." × "Na Nigéria a língua oficial é o inglês, ao lado de…" |
| Pontuação duplicada | 2 | "… é Washington, D.C.**.**" |
| Concordância verbal | 2 | "No Canadá **fala-se** inglês e francês." × "… **falam-se** …" |

**A causa:** o template guarda a frase da primeira resposta da família, com as palavras que cercam o dado. Na família da moeda, a primeira pergunta foi sobre a Nigéria ("A moeda da Nigéria é **a** naira."). O artigo feminino ficou fixo no template e passou para todas as moedas masculinas. O mesmo acontece com o ponto final de um valor que já termina em ponto ("D.C.") e com o artigo de capitais que o pedem ("o Cairo", "a Cidade do México").

**Melhorias possíveis** (não implementadas nesta versão):
- guardar na base de fatos o artigo (e o gênero e o número) de cada valor e incluí-lo no slot: `é {o_moeda}`;
- normalizar a pontuação no fim do preenchimento;
- usar o feedback humano (Pedir mudança) para descartar o template mal formado: o mecanismo de peso já faz isso, desde que o avaliador marque a resposta como ruim.

## A base SQLite do experimento (a prova)

[`experimento-mcp.db`](experimento-mcp.db) guarda o estado do protótipo ao fim do experimento:
- **`templates`:** os 6 templates, com o peso e os usos.
- **`perguntas`:** as 150 perguntas, com a resposta, o caminho, a chave, as chamadas, os tokens e o custo.
- **`feedbacks`:** os 150 feedbacks simulados.

A base foi gerada reproduzindo as respostas que o Claude deu no experimento, gravadas em `resultados-mcp.json`:

```bash
dotnet run --project src/Orquestracao.Experimento -- --llm gravado --respostas docs/experimento/resultados-mcp.json --banco docs/experimento/experimento-mcp.db
```

O fluxo do protótipo é determinístico, e a reprodução bate com o experimento original nas 150 respostas. A base abre em qualquer leitor de SQLite (por exemplo, o DB Browser for SQLite).

Ela também mostra um efeito do feedback simulado. Como ele é "bom" sempre que o dado confere com o gabarito, os pesos de todos os templates subiram para perto de 1,0, inclusive o da moeda, com o artigo errado. Um feedback que só olha o dado não pune erro de forma, e esse é mais um motivo para a avaliação humana.

## Para a avaliação humana

As respostas estão em [`avaliacao-humana.csv`](avaliacao-humana.csv), em pares A/B sorteados. Veja as instruções em [`AVALIACAO-HUMANA.md`](AVALIACAO-HUMANA.md).

**Pontos para o avaliador observar:**
- **Pares iguais:** 117 dos 150 pares têm texto idêntico e devem empatar.
- **Pares diferentes:** os 33 restantes são os casos da tabela acima.

## Limitações

- **Viés da base:** a base de fatos e as respostas do LLM vêm do mesmo modelo, o que favorece a concordância entre o dado e o gabarito.
- **Escopo:** perguntas factuais de estrutura simples.
- **Latência:** não foi medida, porque a resposta passa pela sessão do Claude Code.
- **Tokens e custo:** são estimados pelo tamanho do texto.
