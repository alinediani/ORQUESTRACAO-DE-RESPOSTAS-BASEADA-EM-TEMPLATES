# Análise estrutural — mkqa

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario mkqa --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos padrão do protótipo (`dados/fatos.json`, 58 entidades).

| Medida | Valor |
|---|---:|
| Perguntas | 10000 |
| Com entidade da fonte (podem virar template) | 860 (8,6%) |
| Chaves distintas | 9944 |
| Chaves distintas com entidade | 836 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **24 (0,2%)** |
| Chaves abstratas distintas (números → {n}) | 9936 |
| Perguntas com a estrutura abstrata repetida | 64 (0,6%) |
| Intenções distintas (3 primeiras palavras) | 4935 |
| Perguntas em intenções que aparecem 10+ vezes | 3732 (37,3%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `idade legal para beber {pais}` | 5 |
| `maior estado {pais}` | 3 |
| `altura media de homem {pais}` | 2 |
| `codigo postal {pais}` | 2 |
| `crazy eyes de orange is the new black` | 2 |
| `estado mais pobre {pais}` | 2 |
| `hino nacional {pais}` | 2 |
| `maior cidade {pais}` | 2 |
| `maior feira {pais}` | 2 |
| `maior parque nacional {pais}` | 2 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `idade legal para beber {pais}` | 5 |
| `era populacao {pais} em {n}` | 3 |
| `maior estado {pais}` | 3 |
| `altura media de homem {pais}` | 2 |
| `codigo postal {pais}` | 2 |
| `crazy eyes de orange is the new black` | 2 |
| `estado mais pobre {pais}` | 2 |
| `hino nacional {pais}` | 2 |
| `maior cidade {pais}` | 2 |
| `maior feira {pais}` | 2 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `qual e o` | 329 |
| `quem e o` | 300 |
| `qual e a` | 295 |
| `quem canta a` | 162 |
| `quando foi a` | 154 |
| `quem escreveu a` | 111 |
| `quais sao os` | 79 |
| `quem foi o` | 79 |
| `quando foi lancado` | 74 |
| `quem e a` | 67 |

