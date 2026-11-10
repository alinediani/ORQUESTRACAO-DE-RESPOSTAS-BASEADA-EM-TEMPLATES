# Análise estrutural — multiwoz-original

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-original --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do cenário (`fatos.json` na pasta).

| Medida | Valor |
|---|---:|
| Perguntas | 466 |
| Com entidade da fonte (podem virar template) | 76 (16,3%) |
| Chaves distintas | 451 |
| Chaves distintas com entidade | 76 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **0 (0,0%)** |
| Chaves abstratas distintas (números → {n}) | 451 |
| Perguntas com a estrutura abstrata repetida | 15 (3,2%) |
| Intenções distintas (3 primeiras palavras) | 254 |
| Perguntas em intenções que aparecem 10+ vezes | 66 (14,2%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `is there an entrance fee` | 3 |
| `what is the phone number` | 3 |
| `can you give the address please` | 2 |
| `can you tell if they have free parking` | 2 |
| `do they have free parking` | 2 |
| `great long the restaurant is near the nightclub do you happen to know the postcode for the restaurant` | 2 |
| `i am not sure how many people will be coming so i will do that later but can you provide with their postcode and phone …` | 2 |
| `i don t know yet what is it s star rating and postcode` | 2 |
| `just the postcode please` | 2 |
| `no i dont need it booked but i do need to know if it has free internet the phone number and the post code` | 2 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `is there an entrance fee` | 3 |
| `what is the phone number` | 3 |
| `can you give the address please` | 2 |
| `can you tell if they have free parking` | 2 |
| `do they have free parking` | 2 |
| `great long the restaurant is near the nightclub do you happen to know the postcode for the restaurant` | 2 |
| `i am not sure how many people will be coming so i will do that later but can you provide with their postcode and phone …` | 2 |
| `i don t know yet what is it s star rating and postcode` | 2 |
| `just the postcode please` | 2 |
| `no i dont need it booked but i do need to know if it has free internet the phone number and the post code` | 2 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `what is the` | 27 |
| `can i get` | 18 |
| `can i have` | 11 |
| `can you tell` | 10 |
| `that sounds great` | 9 |
| `could i get` | 8 |
| `could i have` | 7 |
| `can you give` | 6 |
| `please give me` | 6 |
| `great can i` | 5 |


---

## Análise estrutural — multiwoz-original (com a entidade do contexto)

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-original (com a entidade do contexto) --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do MultiWOZ.

| Medida | Valor |
|---|---:|
| Perguntas | 466 |
| Com entidade da fonte (podem virar template) | 466 (100,0%) |
| Chaves distintas | 460 |
| Chaves distintas com entidade | 460 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **6 (1,3%)** |
| Chaves abstratas distintas (números → {n}) | 460 |
| Perguntas com a estrutura abstrata repetida | 6 (1,3%) |
| Intenções distintas (3 primeiras palavras) | 0 |
| Perguntas em intenções que aparecem 10+ vezes | 0 (0,0%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `is there an entrance fee {atracao}` | 3 |
| `can you give the address please {atracao}` | 2 |
| `can you tell if they have free parking {hotel}` | 2 |
| `do they have free parking {hotel}` | 2 |
| `what is the phone number {atracao}` | 2 |

## Estruturas abstratas que mais se repetem

Nenhuma chave se repete.

## Intenções que mais se repetem (3 primeiras palavras)

Nenhuma chave se repete.

