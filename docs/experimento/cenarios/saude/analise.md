# Análise estrutural — saude

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario saude --analisar`, sem LLM: o parser heurístico do protótipo extrai a chave de cada pergunta com a fonte de fatos do cenário (`fatos.json` na pasta).

| Medida | Valor |
|---|---:|
| Perguntas | 17747 |
| Com entidade da fonte (podem virar template) | 2769 (15,6%) |
| Chaves distintas | 17723 |
| Chaves distintas com entidade | 2753 |
| **Teto de reaproveitamento** (repetições de chave com entidade) | **16 (0,1%)** |
| Chaves abstratas distintas (números → {n}) | 17723 |
| Perguntas com a estrutura abstrata repetida | 24 (0,1%) |
| Intenções distintas (3 primeiras palavras) | 8340 |
| Perguntas em intenções que aparecem 10+ vezes | 6653 (37,5%) |

## Chaves que mais se repetem

| Chave | Perguntas |
|---|---:|
| `{condicao} tem cura` | 12 |
| `boa tarde pergunto minha filha tem 14 anos vai fazer 15 em junho 2017 tem mama desproporcional cresceu demais esta ate …` | 2 |
| `boa tarde tenho 19 anos especialista devo procurar para investigar suposta perda de peso preocupante seguida de varios …` | 2 |
| `codeina da sono` | 2 |
| `como tratar sindrome do panico` | 2 |
| `de sobre tratamento {condicao}` | 2 |
| `estou com {condicao} que fazer` | 2 |
| `melhor tratamento para {condicao}` | 2 |
| `normal menina de 7 anos apresentar pelos pubianos idade ossea avancada` | 2 |
| `pessoa da minha familia tem seguinte comportamento 1 nunca assume seus erros foi sempre outro 2 incapaz de expressar gr…` | 2 |

## Estruturas abstratas que mais se repetem

| Chave | Perguntas |
|---|---:|
| `{condicao} tem cura` | 12 |
| `boa tarde pergunto minha filha tem {n} anos vai fazer {n} em junho {n} tem mama desproporcional cresceu demais esta ate…` | 2 |
| `boa tarde tenho {n} anos especialista devo procurar para investigar suposta perda de peso preocupante seguida de varios…` | 2 |
| `codeina da sono` | 2 |
| `como tratar sindrome do panico` | 2 |
| `de sobre tratamento {condicao}` | 2 |
| `estou com {condicao} que fazer` | 2 |
| `melhor tratamento para {condicao}` | 2 |
| `normal menina de {n} anos apresentar pelos pubianos idade ossea avancada` | 2 |
| `pessoa da minha familia tem seguinte comportamento {n} nunca assume seus erros foi sempre outro {n} incapaz de expressa…` | 2 |

## Intenções que mais se repetem (3 primeiras palavras)

| Chave | Perguntas |
|---|---:|
| `meu filho tem` | 432 |
| `minha filha tem` | 315 |
| `boa tarde tenho` | 269 |
| `boa tarde minha` | 201 |
| `boa tarde meu` | 199 |
| `bom dia tenho` | 169 |
| `gostaria de saber` | 169 |
| `bom dia meu` | 168 |
| `ola boa tarde` | 157 |
| `boa tarde estou` | 155 |

