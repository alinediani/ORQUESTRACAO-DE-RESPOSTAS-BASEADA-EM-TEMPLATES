# Resultados do experimento — cenário quiz-simplificada

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "quiz-simplificada"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **Com a trava (validador).** Cada template novo passou por um validador (`Orquestrador.SystemValidacao`) antes de ser salvo: os vereditos são os do Claude Haiku 5.5, gravados em `validacao-haiku.tsv`. Template recusado não é salvo, e as perguntas da chave vão ao LLM. O custo do validador usa o preço do Haiku 5.5 e entra no custo do protótipo.

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 25 | 30 | 16,7% |
| Chamadas ao LLM do parser | 0 | — | |
| Chamadas ao validador (Claude Haiku 5.5) | 4 (3 recusados; 929 tokens, US$ 0,0001) | — | |
| Taxa de acerto de template | 16,7% (5) | — | |
| Tokens | 2.747 | 2.167 | -26,8% |
| Custo estimado | US$ 0,01 | US$ 0,01 | 15,1% |
| Respostas corretas (gabarito) | 63,3% (19) | 50,0% (15) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 4, linha de base melhor em 0, empate em 26 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| P136 | 6 | 1 | 0 (0,0%) |
| P19 | 6 | 1 | 5 (83,3%) |
| P27 | 6 | 1 | 0 (0,0%) |
| P495 | 6 | 1 | 0 (0,0%) |
| R19 | 6 | 1 | 0 (0,0%) |

Templates salvos na base chave-valor: 1.

## Respostas que não trazem o gabarito (para conferir na avaliação humana)

| # | Pergunta | Gabarito | Protótipo | Linha de base |
|---:|---|---|---|---|
| 5 | Qual é o gênero de Why Is There Air?? | comédia stand-up \|\| Stand-up \|\| Comédia stand up \|\| Stand-up Comedy \|\| Stand Up Comedy \|\| stand-up comedy | Why Is There Air? é um álbum de comédia. ❌ | Why Is There Air? é um álbum de comédia. ❌ |
| 8 | Quem nasceu em Phalaborwa? | Dale Steyn | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ |
| 9 | Quem nasceu em Helsinque? | Abba Gindin | Linus Torvalds nasceu em Helsinque. ❌ | Linus Torvalds nasceu em Helsinque. ❌ |
| 11 | Qual é o gênero de Saturday Night? | filme mudo \|\| cinema mudo \|\| filmes mudos \|\| silent film | Saturday Night é um filme de drama. ❌ | Saturday Night é um filme de drama. ❌ |
| 13 | Qual é o gênero de Colloquio? | música experimental \|\| Experimental music \|\| Musica experimental \|\| experimental music | Não sei a que gênero musical Colloquio está ligado. ❌ | Não sei a que gênero musical Colloquio está ligado. ❌ |
| 16 | Qual é o gênero de Theocracy? | estratégia em tempo real \|\| estratégia \|\| real-time strategy | Theocracy é uma banda de power metal. ❌ | Theocracy é uma banda de power metal. ❌ |
| 17 | Onde Charles Koppelman nasceu? | Brooklyn \|\| Condado de Kings | Charles Koppelman nasceu em Brooklyn. | Charles Koppelman nasceu em Nova York. ❌ |
| 19 | Qual é o país de origem de El Greco? | Espanha \|\| Reino da Espanha \|\| Reino de Espanha \|\| Spain | O filme El Greco é da Grécia. ❌ | O filme El Greco é da Grécia. ❌ |
| 20 | Quem nasceu em Stoke-on-Trent? | Clint Boulton | Robbie Williams nasceu em Stoke-on-Trent. ❌ | Robbie Williams nasceu em Stoke-on-Trent. ❌ |
| 23 | Onde Hans Günther van Allen nasceu? | Haan | Hans Günther van Allen nasceu em Haan. | Não sei onde Hans Günther van Allen nasceu. ❌ |
| 24 | Quem nasceu em Woolwich? | Peter Jones | Não sei citar com segurança uma pessoa nascida em Woolwich. ❌ | Não sei citar com segurança uma pessoa nascida em Woolwich. ❌ |
| 25 | Onde Sergio Spina nasceu? | Milão \|\| Milan | Sergio Spina nasceu em Milão. | Não sei com segurança onde Sergio Spina nasceu. ❌ |
| 26 | Quem nasceu em Nancy? | Henri Cartan | Hubert Lyautey nasceu em Nancy. ❌ | Hubert Lyautey nasceu em Nancy. ❌ |
| 27 | Quem nasceu em Srinagar? | Amitabh Mattoo | Farooq Abdullah nasceu em Srinagar. ❌ | Farooq Abdullah nasceu em Srinagar. ❌ |
| 28 | Onde Barry Ferguson nasceu? | Dublin | Barry Ferguson nasceu em Dublin. | Barry Ferguson nasceu em Glasgow. ❌ |

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Qual é o país de origem de The Rapture? | The Rapture (El rapto) é um filme do México. | llm | The Rapture (El rapto) é um filme do México. |
| 2 | Qual é o país de origem de Victoria No. 203? | Victoria No. 203 é um filme da Índia. | llm | Victoria No. 203 é um filme da Índia. |
| 3 | Qual é o gênero de Something for the Boys? | Something for the Boys é um filme musical. | llm | Something for the Boys é um filme musical. |
| 4 | Qual é o país de origem de The New Fred and Barney Show? | O país de origem de The New Fred and Barney Show é os Estados Unidos. | llm | O país de origem de The New Fred and Barney Show é os Estados Unidos. |
| 5 | Qual é o gênero de Why Is There Air?? | Why Is There Air? é um álbum de comédia. ❌ | llm | Why Is There Air? é um álbum de comédia. ❌ |
| 6 | Qual é a nacionalidade de Malouf Abraham, Sr.? | Malouf Abraham, Sr. é dos Estados Unidos. | llm | Malouf Abraham, Sr. é dos Estados Unidos. |
| 7 | Qual é o país de origem de TV Patrol Northwestern Mindanao? | TV Patrol Northwestern Mindanao é exibido nas Filipinas. | llm | TV Patrol Northwestern Mindanao é exibido nas Filipinas. |
| 8 | Quem nasceu em Phalaborwa? | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ | llm | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ |
| 9 | Quem nasceu em Helsinque? | Linus Torvalds nasceu em Helsinque. ❌ | llm | Linus Torvalds nasceu em Helsinque. ❌ |
| 10 | Onde Fawzi al-Mulki nasceu? | Fawzi al-Mulki nasceu em Irbid. | llm | Fawzi al-Mulki nasceu em Irbid. |
| 11 | Qual é o gênero de Saturday Night? | Saturday Night é um filme de drama. ❌ | llm | Saturday Night é um filme de drama. ❌ |
| 12 | Qual é a nacionalidade de Mary Lou Fallis? | Mary Lou Fallis é do Canadá. | llm | Mary Lou Fallis é do Canadá. |
| 13 | Qual é o gênero de Colloquio? | Não sei a que gênero musical Colloquio está ligado. ❌ | llm | Não sei a que gênero musical Colloquio está ligado. ❌ |
| 14 | Onde Q2614262 nasceu? | Q2614262 nasceu em Trenton. | template | Vernee Watson-Johnson nasceu em Trenton. |
| 15 | Qual é o gênero de The Good Companions? | The Good Companions é um romance. | llm | The Good Companions é um romance. |
| 16 | Qual é o gênero de Theocracy? | Theocracy é uma banda de power metal. ❌ | llm | Theocracy é uma banda de power metal. ❌ |
| 17 | Onde Charles Koppelman nasceu? | Charles Koppelman nasceu em Brooklyn. | template | Charles Koppelman nasceu em Nova York. ❌ |
| 18 | Qual é o país de origem de Crusader? | O país de origem de Crusader é os Estados Unidos. | llm | O país de origem de Crusader é os Estados Unidos. |
| 19 | Qual é o país de origem de El Greco? | O filme El Greco é da Grécia. ❌ | llm | O filme El Greco é da Grécia. ❌ |
| 20 | Quem nasceu em Stoke-on-Trent? | Robbie Williams nasceu em Stoke-on-Trent. ❌ | llm | Robbie Williams nasceu em Stoke-on-Trent. ❌ |
