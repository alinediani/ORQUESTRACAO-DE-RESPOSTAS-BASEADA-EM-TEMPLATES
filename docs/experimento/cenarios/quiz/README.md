# Cenário 6 — Quiz (SimpleQuestions no Wikidata)

**Hipótese:** é o perfil em que o orquestrador deve encaixar. Num quiz, as perguntas são de uma relação só ("Onde X
nasceu?", "Qual é a nacionalidade de X?"), a estrutura se repete por relação e a resposta inteira é um dado de uma base de
conhecimento.

O cenário tem duas versões, sobre as mesmas perguntas:

- **6a — tradução fiel** (`../quiz-fiel/`): cada pergunta traduzida mantendo a redação original;
- **6b — tradução simplificada** (`../quiz-simplificada/`): cada pergunta reescrita na forma canônica da relação
  (`simplificacao.tsv`), como faria um LLM normalizador antes do parser. Num sistema real, essa simplificação é **uma
  chamada a mais por pergunta**, e o custo dela está estimado abaixo.

## Base

- **SimpleQuestions** (Bordes et al., 2015, *Large-scale Simple Question Answering with Memory Networks*), com 108.442
  perguntas escritas por pessoas, cada uma ligada a um fato (sujeito, relação, objeto). Foi usado o mapeamento para o
  Wikidata (Diefenbach et al., 2017, *Question Answering Benchmarks for Wikidata*,
  [askplatypus/wikidata-simplequestions](https://github.com/askplatypus/wikidata-simplequestions)).
- **Licença:** CC BY 3.0 (`LICENSE.txt`, a do SimpleQuestions original, mantida no mapeamento).
- **Arquivos:**
  - `annotated_wd_data_test_answerable.txt`: o teste respondível, com 5.621 perguntas e 117 relações.
  - `amostra.json`: 300 perguntas sorteadas (`random.seed(2026)`) e os rótulos em português e inglês, mais os apelidos em
    português, dos sujeitos, objetos e relações. Os rótulos foram consultados na API do Wikidata em 09/11/2026; 319 das
    1.042 entidades não têm rótulo em português, e nesses casos fica o rótulo em inglês.
  - `traducao-fiel.tsv`: a tradução fiel das 300 perguntas, feita pelo Claude.
  - `simplificacao.tsv`: a forma canônica de cada uma das 63 relações da amostra.
- **A fonte vem do Wikidata, e não do Claude.** Cada sujeito vira uma entidade, cada relação um atributo, e o rótulo do
  objeto é o valor (`../quiz-fiel/fatos.json`). É o primeiro cenário em que a fonte não foi escrita por quem respondeu.
- **Gabarito:** o rótulo do objeto e os seus apelidos.

## Como reproduzir

```bash
dotnet run --project src/Orquestracao.Experimento -- --cenario quiz-fiel --analisar --preparar --perguntas 30
dotnet run --project src/Orquestracao.Experimento -- --cenario quiz-simplificada --analisar --preparar --perguntas 30
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"quiz-fiel"}'
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"quiz-simplificada"}'
```

## Resultados

**Análise estrutural** das 300 perguntas, com a fonte do Wikidata ([6a](../quiz-fiel/analise.md), [6b](../quiz-simplificada/analise.md)):

| Versão | Chaves distintas | Teto de reaproveitamento |
|---|---:|---:|
| Inglês original | 247 | 17,7% |
| 6a — tradução fiel | 210 | **30,0%** |
| 6b — tradução simplificada | 62 | **79,3%** |

O teto da tradução fiel é maior que o do inglês por dois motivos que não dá para separar: o parser só remove palavras de
preenchimento em português, e a tradução pode ter aproximado redações parecidas. Na tradução fiel, "gênero" junta numa só
chave o sexo de uma pessoa (P21) e o gênero de uma obra (P136): é a mesma ambiguidade observada no cenário de saúde.

**Experimento real**, com 30 perguntas e o Claude (Opus 5.5) como LLM via MCP. A amostra tem 6 perguntas de cada uma das
5 relações mais frequentes (país de origem, gênero, nacionalidade, local de nascimento e "quem nasceu em"), o perfil de um
app de quiz.

| Métrica | 6a — fiel | 6b — simplificada | Linha de base |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 26 | **14** | 30 |
| Respostas pelo template | 4 | **16** | — |
| Corretas pela correção automática | 17 | **21** | 15 |
| Tokens de geração (estimados) | 1.886 | 995 | 2.181 |

**O template acertou mais que o LLM.** Nas respostas de template, o dado vem do Wikidata. Em seis delas, o LLM sozinho
errou, não sabia ou foi impreciso: Sergio Spina nasceu em Milão, El Greco (o filme) é da Espanha, Theocracy é um jogo de estratégia em
tempo real, e Hans Günther van Allen nasceu em Haan. Os outros dois são o Barry Ferguson nascido em Dublin (um homônimo
do jogador escocês que o LLM supôs) e o Charles Koppelman nascido no Brooklyn, que o LLM respondeu apenas como "Nova York".

**A forma continua sendo o problema.** O template herda palavras da primeira resposta:

- "Mary Lou Fallis é dos Canadá" (concordância);
- "Victoria No. 203 (El rapto) é um filme do Índia": o parêntese e "filme" vieram da primeira resposta, sobre o filme
  mexicano, e voltaram até para um programa de TV;
- "Q2614262 nasceu em Trenton": a entidade sem rótulo no Wikidata ficou com o identificador.

**Limite da correção automática:** nas 6 perguntas "quem nasceu em X?", há muitas respostas certas, e o gabarito aceita
só uma. "Linus Torvalds nasceu em Helsinque" e "Robbie Williams nasceu em Stoke-on-Trent" estão certas, mas contam como
erro nas duas estratégias. Nenhuma delas virou template, porque a resposta do LLM não trazia o dado da fonte.

## O custo da simplificação (6b)

Simplificar é uma chamada ao LLM por pergunta. Pela estimativa de ≈ 4 caracteres por token, com o prompt do normalizador
(`Cenarios.SystemNormalizacao`, 65 tokens), simplificar as 30 perguntas custa 2.248 tokens de entrada e 275 de saída. As
300 da análise custariam 22.590 de entrada e 2.751 de saída. Comparando com a economia na geração do experimento
(US$ 0,0140 na linha de base contra US$ 0,0064 no protótipo, com o Opus):

| Modelo do normalizador | Custo da simplificação (30 perguntas) | Economia na geração | Saldo |
|---|---:|---:|---:|
| Claude Opus 5.5 (o mesmo da geração) | US$ 0,0145 | US$ 0,0076 | **−US$ 0,0069** |
| Claude Sonnet 5.5 | US$ 0,0073 | US$ 0,0076 | +US$ 0,0004 |
| Claude Haiku 5.5 | US$ 0,0004 | US$ 0,0076 | **+US$ 0,0072** |

Preços por milhão de tokens (entrada/saída) da tabela do protótipo (`MotorApi`): Opus US$ 4/20, Sonnet US$ 2/10,
Haiku US$ 0,10/0,50.

## 6c — tradução fiel com o Claude Haiku como normalizador

Com o segundo fluxo (`"normalizador": "haiku"`), as perguntas da tradução fiel passaram por um normalizador genérico,
o Claude Haiku, com o prompt geral do protótipo e sem a tabela de formas canônicas. O resultado está em
`../quiz-fiel/resultados-mcp-haiku.md`: 24 chamadas de geração contra 30, 6 respostas pelo template (todas com o dado
certo), 18 corretas contra 15 e custo total 17% menor, já incluindo as 30 chamadas ao Haiku (US$ 0,0005). O Haiku
padronizou pouco: corrigiu a pontuação, mas manteve redações como "De que país é X?" e "Qual é a nacionalidade de X?",
que caem em chaves diferentes.

## Conclusões

1. **É o cenário em que o protótipo funciona,** desde que as perguntas sejam padronizadas. Com a tradução fiel, o ganho é
   pequeno (4 de 30 pelo template), porque as pessoas escrevem a mesma pergunta de muitos jeitos. Com a simplificada, o
   protótipo cortou 53% das chamadas e acertou mais que a linha de base.
2. **A simplificação só se paga com um modelo barato.** Simplificar uma pergunta curta custa quase o mesmo que
   respondê-la. Com o mesmo modelo da geração, o sistema gasta mais do que economiza. Com um modelo pequeno, como o Haiku,
   ele preserva cerca de 95% da economia. Em respostas longas, a economia na geração cresce e a simplificação pesa menos.
3. **A fonte externa reduz a alucinação na prática.** Em 6 das 16 respostas de template, o dado do Wikidata corrigiu um
   erro, uma lacuna ou uma imprecisão do LLM. Este cenário confirma, com dados reais, o resultado do experimento simulado.
4. **A forma herdada continua sem solução.** Concordância ("dos Canadá") e trechos da primeira resposta ("(El rapto)")
   aparecem em quase todas as respostas de template. É o mesmo problema do experimento original, e o feedback não o corrige
   valor a valor.

> **Viés:** o Claude traduziu as perguntas, escreveu as formas canônicas e respondeu como LLM. A fonte e o gabarito vêm do
> Wikidata e do SimpleQuestions.
