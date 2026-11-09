# Análise estrutural — quiz-simplificada

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario quiz-simplificada --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do cenário (`fatos.json` na pasta).

| Medida | Valor |
|---|---:|
| Perguntas | 300 |
| Com entidade da fonte (podem virar template) | 300 (100,0%) |
| Chaves distintas | 62 |
| Chaves distintas com entidade | 62 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **238 (79,3%)** |
| Chaves abstratas distintas (números → {n}) | 62 |
| Perguntas com a estrutura abstrata repetida | 238 (79,3%) |
| Intenções distintas (3 primeiras palavras) | 87 |
| Perguntas em intenções que aparecem 10+ vezes | 179 (59,7%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `genero {item}` | 33 |
| `onde {item} nasceu` | 25 |
| `pais de origem {item}` | 20 |
| `nacionalidade {item}` | 18 |
| `quem nasceu {item}` | 16 |
| `sexo {item}` | 15 |
| `cite obra do genero {item}` | 13 |
| `em que posicao {item} joga` | 13 |
| `onde {item} morreu` | 13 |
| `quem interprete {item}` | 12 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `genero {item}` | 33 |
| `onde {item} nasceu` | 25 |
| `pais de origem {item}` | 20 |
| `nacionalidade {item}` | 18 |
| `quem nasceu {item}` | 16 |
| `sexo {item}` | 15 |
| `cite obra do genero {item}` | 13 |
| `em que posicao {item} joga` | 13 |
| `onde {item} morreu` | 13 |
| `quem interprete {item}` | 12 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `qual e o` | 81 |
| `qual e a` | 28 |
| `quem e o` | 21 |
| `cite uma obra` | 20 |
| `quem nasceu em` | 16 |
| `em que posicao` | 13 |
| `cite um filme` | 7 |
| `quem compos a` | 7 |
| `cite um lugar` | 6 |
| `cite um jogo` | 5 |


---

## Análise estrutural — quiz-simplificada (inglês original)

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario quiz-simplificada (inglês original) --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do quiz (Wikidata).

| Medida | Valor |
|---|---:|
| Perguntas | 300 |
| Com entidade da fonte (podem virar template) | 276 (92,0%) |
| Chaves distintas | 247 |
| Chaves distintas com entidade | 223 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **53 (17,7%)** |
| Chaves abstratas distintas (números → {n}) | 247 |
| Perguntas com a estrutura abstrata repetida | 53 (17,7%) |
| Intenções distintas (3 primeiras palavras) | 196 |
| Perguntas em intenções que aparecem 10+ vezes | 34 (11,3%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `where was {item} born` | 12 |
| `what is {item} s gender` | 6 |
| `who was born in {item}` | 6 |
| `who directed {item}` | 4 |
| `what country is {item} from` | 3 |
| `what gender is {item}` | 3 |
| `what is the nationality of {item}` | 3 |
| `what is {item} s nationality` | 3 |
| `what position does {item} play` | 3 |
| `which country is {item} from` | 3 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `where was {item} born` | 12 |
| `what is {item} s gender` | 6 |
| `who was born in {item}` | 6 |
| `who directed {item}` | 4 |
| `what country is {item} from` | 3 |
| `what gender is {item}` | 3 |
| `what is the nationality of {item}` | 3 |
| `what is {item} s nationality` | 3 |
| `what position does {item} play` | 3 |
| `which country is {item} from` | 3 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `what is the` | 24 |
| `who is the` | 10 |
| `what country is` | 7 |
| `what type of` | 7 |
| `what position does` | 6 |
| `who is a` | 6 |
| `who was born` | 6 |
| `what is an` | 5 |
| `who wrote the` | 5 |
| `what genre is` | 4 |


## Custo estimado da simplificação

300 chamadas ao normalizador (uma por pergunta), 22590 tokens de entrada e 2751 de saída (≈ 4 caracteres por token).
