# Análise estrutural — quiz-fiel

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario quiz-fiel --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do cenário (`fatos.json` na pasta).

| Medida | Valor |
|---|---:|
| Perguntas | 300 |
| Com entidade da fonte (podem virar template) | 294 (98,0%) |
| Chaves distintas | 210 |
| Chaves distintas com entidade | 204 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **90 (30,0%)** |
| Chaves abstratas distintas (números → {n}) | 210 |
| Perguntas com a estrutura abstrata repetida | 90 (30,0%) |
| Intenções distintas (3 primeiras palavras) | 150 |
| Perguntas em intenções que aparecem 10+ vezes | 94 (31,3%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `onde {item} nasceu` | 14 |
| `genero {item}` | 12 |
| `nacionalidade {item}` | 9 |
| `de que pais {item}` | 7 |
| `quem nasceu {item}` | 6 |
| `em que posicao {item} joga` | 4 |
| `profissao {item}` | 4 |
| `quem dirigiu {item}` | 4 |
| `album {item}` | 3 |
| `etnia {item}` | 3 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `onde {item} nasceu` | 14 |
| `genero {item}` | 12 |
| `nacionalidade {item}` | 9 |
| `de que pais {item}` | 7 |
| `quem nasceu {item}` | 6 |
| `em que posicao {item} joga` | 4 |
| `profissao {item}` | 4 |
| `quem dirigiu {item}` | 4 |
| `album {item}` | 3 |
| `etnia {item}` | 3 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `qual e o` | 32 |
| `qual e a` | 19 |
| `de que pais` | 12 |
| `que tipo de` | 11 |
| `qual e um` | 10 |
| `quem e o` | 10 |
| `em que idioma` | 9 |
| `em que posicao` | 8 |
| `qual o nome` | 7 |
| `em que cidade` | 6 |


---

## Análise estrutural — quiz-fiel (inglês original)

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario quiz-fiel (inglês original) --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do quiz (Wikidata).

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

