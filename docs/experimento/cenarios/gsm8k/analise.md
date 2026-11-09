# Análise estrutural — gsm8k

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario gsm8k --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos padrão do protótipo (`dados/fatos.json`, 58 entidades).

| Medida | Valor |
|---|---:|
| Perguntas | 1318 |
| Com entidade da fonte (podem virar template) | 17 (1,3%) |
| Chaves distintas | 1318 |
| Chaves distintas com entidade | 17 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **0 (0,0%)** |
| Chaves abstratas distintas (números → {n}) | 1318 |
| Perguntas com a estrutura abstrata repetida | 0 (0,0%) |
| Intenções distintas (3 primeiras palavras) | 1283 |
| Perguntas em intenções que aparecem 10+ vezes | 0 (0,0%) |

## Chaves que mais se repetem

Nenhuma chave se repete.

## Estruturas abstratas que mais se repetem

Nenhuma chave se repete.

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `uma empresa de` | 4 |
| `james compra um` | 3 |
| `john decide fazer` | 3 |
| `na segunda feira` | 3 |
| `um vendedor de` | 3 |
| `uma loja de` | 3 |
| `a cidade de` | 2 |
| `a familia de` | 2 |
| `a idade de` | 2 |
| `a mae do` | 2 |

