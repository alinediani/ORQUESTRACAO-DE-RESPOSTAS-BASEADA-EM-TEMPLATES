# Cenário 7a — Demanda real por estilos (WildChat)

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario estilos --analisar`, sem LLM. Um pedido é "de estilo" quando traz um marcador como "no estilo de", "como um/uma", "in the style of", "as if you were", "like a" ou "act as"; o estilo é o que vem depois do marcador (até 4 palavras). Os padrões são simples e contam também usos que não são pedidos de estilo ("como um todo"); os números são uma aproximação.

## Português (as 2.300 primeiras conversas da base, abr.–mai. de 2023)

| Medida | Valor |
|---|---:|
| Pedidos (primeiro turno de cada conversa) | 2300 |
| Pedidos de estilo | 30 (1,3%) |
| Estilos distintos | 26 |
| Fração dos pedidos de estilo nos 10 estilos mais pedidos | 46,7% |
| Pedidos idênticos a outro (cache puro, pelos primeiros 200 caracteres) | 290 (12,6%) |

**Estilos mais pedidos:** "bebe recem nascido em" (2); "email marketer profissional" (2); "india gigante ou outras" (2); "jogo interessante" (2); "advogado falaria" (1); "agencia de marketing digital" (1); "arquivo txt" (1); "das equipes mais desorganizadas" (1); "deus criador de mundo" (1); "dos elementos" (1); "enorme poliedro cosmico que" (1); "estrela" (1); "filmes como" (1); "filmes como fantastic voyage" (1); "india gigante cuidando da" (1).

**Pedidos mais repetidos (início):**

- 20× `descreva o projeto da rrt rodriguez racing team uma equipe mexicana que conseguiu entrar n…`
- 18× `ola`
- 14× `oi`
- 14× `descreva o projeto da laguna racing team uma equipe mexicana que tinha como o objetivo ent…`
- 11× `componha um micro conto sobre`
- 10× `hello`
- 7× `componha um micro conto com`
- 6× `priorizando a etica profissional leia o relato de uma vivencia e a intervencao adotada pel…`

| Mês | Pedidos | De estilo | Estilo mais pedido no mês |
|---|---:|---:|---|
| 2023-04 | 1061 | 12 | jogo interessante (2) |
| 2023-05 | 1239 | 18 | bebe recem nascido em (2) |

## Inglês (amostra de 100 blocos espalhados na base, abr.–jun. de 2023)

| Medida | Valor |
|---|---:|
| Pedidos (primeiro turno de cada conversa) | 1219 |
| Pedidos de estilo | 39 (3,2%) |
| Estilos distintos | 24 |
| Fração dos pedidos de estilo nos 10 estilos mais pedidos | 64,1% |
| Pedidos idênticos a outro (cache puro, pelos primeiros 200 caracteres) | 193 (15,8%) |

**Estilos mais pedidos:** "bright yellow sundress and" (4); "chatgpt with developer mode" (4); "dan" (3); "windows xp computer from" (3); "a roald dahl illustration" (2); "chatgpt with dan mode" (2); "contemporary writer" (2); "content writer very proficient" (2); "screenwriter" (2); "a computer game danganronpa" (1); "abeta reader for this" (1); "academic journal editor" (1); "cat" (1); "dataset without mentioning any" (1); "earthquake and leave big" (1).

**Pedidos mais repetidos (início):**

- 23× `in the betweenlands player wow this dimension is nightmare fuel player comes across a baby…`
- 10× `hi`
- 9× `hello`
- 8× `as i sat there in my room i began to work on a drawing of some of my fellow club members a…`
- 7× `the following boss fights in a game have the player fight multiple enemies at the same tim…`
- 7× `write a day 1 script for the arachnarch characters araneas the arachnarch player scene poi…`
- 7× `make a fake article about amy noonan heidmann aka qveen herby a rapper and singer from sew…`
- 6× `hey there are you familiar with reality shifting so i m refining a foolproof method for re…`

| Mês | Pedidos | De estilo | Estilo mais pedido no mês |
|---|---:|---:|---|
| 2023-04 | 278 | 10 | a roald dahl illustration (2) |
| 2023-05 | 643 | 26 | bright yellow sundress and (4) |
| 2023-06 | 298 | 3 | abeta reader for this (1) |

