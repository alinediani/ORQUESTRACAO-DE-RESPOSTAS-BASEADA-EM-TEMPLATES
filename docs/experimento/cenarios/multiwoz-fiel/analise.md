# Análise estrutural — multiwoz-fiel

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-fiel --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do cenário (`fatos.json` na pasta).

| Medida | Valor |
|---|---:|
| Perguntas | 466 |
| Com entidade da fonte (podem virar template) | 72 (15,5%) |
| Chaves distintas | 444 |
| Chaves distintas com entidade | 71 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **1 (0,2%)** |
| Chaves abstratas distintas (números → {n}) | 444 |
| Perguntas com a estrutura abstrata repetida | 22 (4,7%) |
| Intenções distintas (3 primeiras palavras) | 245 |
| Perguntas em intenções que aparecem 10+ vezes | 82 (17,6%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `endereco` | 4 |
| `cep` | 3 |
| `dar endereco` | 3 |
| `telefone` | 3 |
| `tem preco de entrada` | 3 |
| `ainda nao sei classificacao em estrelas dele cep` | 2 |
| `dar endereco cep` | 2 |
| `eles tem estacionamento gratuito` | 2 |
| `em que regiao fica {atracao}` | 2 |
| `nao faixa de preco cep` | 2 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `endereco` | 4 |
| `cep` | 3 |
| `dar endereco` | 3 |
| `telefone` | 3 |
| `tem preco de entrada` | 3 |
| `ainda nao sei classificacao em estrelas dele cep` | 2 |
| `dar endereco cep` | 2 |
| `eles tem estacionamento gratuito` | 2 |
| `em que regiao fica {atracao}` | 2 |
| `nao faixa de preco cep` | 2 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `qual e o` | 29 |
| `voce pode me` | 21 |
| `posso obter o` | 17 |
| `posso ter o` | 15 |
| `isso parece otimo` | 9 |
| `sim por favor` | 8 |
| `por favor me` | 7 |
| `voce poderia me` | 7 |
| `na verdade eu` | 6 |
| `que tipo de` | 6 |


---

## Análise estrutural — multiwoz-fiel (com a entidade do contexto)

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-fiel (com a entidade do contexto) --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do MultiWOZ.

| Medida | Valor |
|---|---:|
| Perguntas | 466 |
| Com entidade da fonte (podem virar template) | 466 (100,0%) |
| Chaves distintas | 450 |
| Chaves distintas com entidade | 450 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **16 (3,4%)** |
| Chaves abstratas distintas (números → {n}) | 450 |
| Perguntas com a estrutura abstrata repetida | 16 (3,4%) |
| Intenções distintas (3 primeiras palavras) | 0 |
| Perguntas em intenções que aparecem 10+ vezes | 0 (0,0%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `dar endereco {atracao}` | 4 |
| `em que regiao fica {atracao}` | 3 |
| `endereco {atracao}` | 3 |
| `telefone {atracao}` | 3 |
| `tem preco de entrada {atracao}` | 3 |
| `cep {atracao}` | 2 |
| `eles tem estacionamento gratuito {hotel}` | 2 |
| `obrigado preco da entrada {atracao}` | 2 |
| `posso ter telefone {atracao}` | 2 |
| `se eles tem estacionamento gratuito {hotel}` | 2 |

## Estruturas abstratas que mais se repetem

Nenhuma chave se repete.

## Intenções que mais se repetem (3 primeiras palavras)

Nenhuma chave se repete.

