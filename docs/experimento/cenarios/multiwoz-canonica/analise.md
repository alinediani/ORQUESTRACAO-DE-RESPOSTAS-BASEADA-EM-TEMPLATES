# Análise estrutural — multiwoz-canonica

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-canonica --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do cenário (`fatos.json` na pasta).

| Medida | Valor |
|---|---:|
| Perguntas | 466 |
| Com entidade da fonte (podem virar template) | 466 (100,0%) |
| Chaves distintas | 78 |
| Chaves distintas com entidade | 78 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **388 (83,3%)** |
| Chaves abstratas distintas (números → {n}) | 78 |
| Perguntas com a estrutura abstrata repetida | 388 (83,3%) |
| Intenções distintas (3 primeiras palavras) | 3 |
| Perguntas em intenções que aparecem 10+ vezes | 466 (100,0%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `endereco {atracao}` | 43 |
| `telefone {atracao}` | 39 |
| `cep {atracao}` | 34 |
| `endereco cep {atracao}` | 23 |
| `telefone {hotel}` | 20 |
| `preco da entrada {atracao}` | 16 |
| `endereco {restaurante}` | 15 |
| `cep {hotel}` | 14 |
| `endereco {hotel}` | 14 |
| `telefone {restaurante}` | 14 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `endereco {atracao}` | 43 |
| `telefone {atracao}` | 39 |
| `cep {atracao}` | 34 |
| `endereco cep {atracao}` | 23 |
| `telefone {hotel}` | 20 |
| `preco da entrada {atracao}` | 16 |
| `endereco {restaurante}` | 15 |
| `cep {hotel}` | 14 |
| `endereco {hotel}` | 14 |
| `telefone {restaurante}` | 14 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `qual e o` | 373 |
| `qual e a` | 70 |
| `diga se tem` | 23 |


---

## Análise estrutural — multiwoz-canonica (com a entidade do contexto)

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-canonica (com a entidade do contexto) --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do MultiWOZ.

| Medida | Valor |
|---|---:|
| Perguntas | 466 |
| Com entidade da fonte (podem virar template) | 466 (100,0%) |
| Chaves distintas | 78 |
| Chaves distintas com entidade | 78 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **388 (83,3%)** |
| Chaves abstratas distintas (números → {n}) | 78 |
| Perguntas com a estrutura abstrata repetida | 388 (83,3%) |
| Intenções distintas (3 primeiras palavras) | 0 |
| Perguntas em intenções que aparecem 10+ vezes | 0 (0,0%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `endereco {atracao}` | 43 |
| `telefone {atracao}` | 39 |
| `cep {atracao}` | 34 |
| `endereco cep {atracao}` | 23 |
| `telefone {hotel}` | 20 |
| `preco da entrada {atracao}` | 16 |
| `endereco {restaurante}` | 15 |
| `cep {hotel}` | 14 |
| `endereco {hotel}` | 14 |
| `telefone {restaurante}` | 14 |

## Estruturas abstratas que mais se repetem

Nenhuma chave se repete.

## Intenções que mais se repetem (3 primeiras palavras)

Nenhuma chave se repete.

