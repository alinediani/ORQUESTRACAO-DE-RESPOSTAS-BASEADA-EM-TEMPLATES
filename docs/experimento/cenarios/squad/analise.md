# Análise estrutural — squad

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario squad --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos padrão do protótipo (`dados/fatos.json`, 58 entidades).

| Medida | Valor |
|---|---:|
| Perguntas | 9808 |
| Com entidade da fonte (podem virar template) | 610 (6,2%) |
| Chaves distintas | 9803 |
| Chaves distintas com entidade | 609 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **1 (0,0%)** |
| Chaves abstratas distintas (números → {n}) | 9802 |
| Perguntas com a estrutura abstrata repetida | 6 (0,1%) |
| Intenções distintas (3 primeiras palavras) | 5760 |
| Perguntas em intenções que aparecem 10+ vezes | 2944 (30,0%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `como se chamava forca do marechal` | 2 |
| `era natureza primaria do interesse {pais} nas ilhas marshall` | 2 |
| `onde beyonce ficou gravida` | 2 |
| `quanto tempo durou hiato` | 2 |
| `que direito da uniao europeia` | 2 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `como se chamava forca do marechal` | 2 |
| `era natureza primaria do interesse {pais} nas ilhas marshall` | 2 |
| `era populacao de tuscon em {n}` | 2 |
| `onde beyonce ficou gravida` | 2 |
| `quanto tempo durou hiato` | 2 |
| `que direito da uniao europeia` | 2 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `qual e o` | 329 |
| `em que ano` | 280 |
| `que tipo de` | 238 |
| `qual e a` | 233 |
| `o que e` | 161 |
| `qual foi o` | 146 |
| `qual era o` | 112 |
| `qual foi a` | 105 |
| `quem foi o` | 87 |
| `o que o` | 83 |

