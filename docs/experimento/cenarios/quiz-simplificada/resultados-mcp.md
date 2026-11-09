# Resultados do experimento — cenário quiz-simplificada

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "quiz-simplificada"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 14 | 30 | 53,3% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 53,3% (16) | — | |
| Tokens | 995 | 2.167 | 54,1% |
| Custo estimado | US$ 0,0064 | US$ 0,01 | 54,4% |
| Respostas corretas (gabarito) | 70,0% (21) | 50,0% (15) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 6, linha de base melhor em 0, empate em 24 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| P136 | 6 | 1 | 1 (16,7%) |
| P19 | 6 | 1 | 5 (83,3%) |
| P27 | 6 | 1 | 5 (83,3%) |
| P495 | 6 | 1 | 5 (83,3%) |
| R19 | 6 | 1 | 0 (0,0%) |

Templates salvos na base chave-valor: 4.

## Respostas que não trazem o gabarito (para conferir na avaliação humana)

| # | Pergunta | Gabarito | Protótipo | Linha de base |
|---:|---|---|---|---|
| 5 | Qual é o gênero de Why Is There Air?? | comédia stand-up \|\| Stand-up \|\| Comédia stand up \|\| Stand-up Comedy \|\| Stand Up Comedy \|\| stand-up comedy | Why Is There Air? é um álbum de comédia. ❌ | Why Is There Air? é um álbum de comédia. ❌ |
| 8 | Quem nasceu em Phalaborwa? | Dale Steyn | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ |
| 9 | Quem nasceu em Helsinque? | Abba Gindin | Linus Torvalds nasceu em Helsinque. ❌ | Linus Torvalds nasceu em Helsinque. ❌ |
| 11 | Qual é o gênero de Saturday Night? | filme mudo \|\| cinema mudo \|\| filmes mudos \|\| silent film | Saturday Night é um filme de drama. ❌ | Saturday Night é um filme de drama. ❌ |
| 13 | Qual é o gênero de Colloquio? | música experimental \|\| Experimental music \|\| Musica experimental \|\| experimental music | Não sei a que gênero musical Colloquio está ligado. ❌ | Não sei a que gênero musical Colloquio está ligado. ❌ |
| 16 | Qual é o gênero de Theocracy? | estratégia em tempo real \|\| estratégia \|\| real-time strategy | Theocracy é um estratégia em tempo real. | Theocracy é uma banda de power metal. ❌ |
| 17 | Onde Charles Koppelman nasceu? | Brooklyn \|\| Condado de Kings | Charles Koppelman nasceu em Brooklyn. | Charles Koppelman nasceu em Nova York. ❌ |
| 19 | Qual é o país de origem de El Greco? | Espanha \|\| Reino da Espanha \|\| Reino de Espanha \|\| Spain | El Greco (El rapto) é um filme do Espanha. | O filme El Greco é da Grécia. ❌ |
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
| 2 | Qual é o país de origem de Victoria No. 203? | Victoria No. 203 (El rapto) é um filme do Índia. | template | Victoria No. 203 é um filme da Índia. |
| 3 | Qual é o gênero de Something for the Boys? | Something for the Boys é um filme musical. | llm | Something for the Boys é um filme musical. |
| 4 | Qual é o país de origem de The New Fred and Barney Show? | The New Fred and Barney Show (El rapto) é um filme do Estados Unidos. | template | O país de origem de The New Fred and Barney Show é os Estados Unidos. |
| 5 | Qual é o gênero de Why Is There Air?? | Why Is There Air? é um álbum de comédia. ❌ | llm | Why Is There Air? é um álbum de comédia. ❌ |
| 6 | Qual é a nacionalidade de Malouf Abraham, Sr.? | Malouf Abraham, Sr. é dos Estados Unidos. | llm | Malouf Abraham, Sr. é dos Estados Unidos. |
| 7 | Qual é o país de origem de TV Patrol Northwestern Mindanao? | TV Patrol Northwestern Mindanao (El rapto) é um filme do Filipinas. | template | TV Patrol Northwestern Mindanao é exibido nas Filipinas. |
| 8 | Quem nasceu em Phalaborwa? | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ | llm | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ |
| 9 | Quem nasceu em Helsinque? | Linus Torvalds nasceu em Helsinque. ❌ | llm | Linus Torvalds nasceu em Helsinque. ❌ |
| 10 | Onde Fawzi al-Mulki nasceu? | Fawzi al-Mulki nasceu em Irbid. | llm | Fawzi al-Mulki nasceu em Irbid. |
| 11 | Qual é o gênero de Saturday Night? | Saturday Night é um filme de drama. ❌ | llm | Saturday Night é um filme de drama. ❌ |
| 12 | Qual é a nacionalidade de Mary Lou Fallis? | Mary Lou Fallis é dos Canadá. | template | Mary Lou Fallis é do Canadá. |
| 13 | Qual é o gênero de Colloquio? | Não sei a que gênero musical Colloquio está ligado. ❌ | llm | Não sei a que gênero musical Colloquio está ligado. ❌ |
| 14 | Onde Q2614262 nasceu? | Q2614262 nasceu em Trenton. | template | Vernee Watson-Johnson nasceu em Trenton. |
| 15 | Qual é o gênero de The Good Companions? | The Good Companions é um romance. | llm | The Good Companions é um romance. |
| 16 | Qual é o gênero de Theocracy? | Theocracy é um estratégia em tempo real. | template | Theocracy é uma banda de power metal. ❌ |
| 17 | Onde Charles Koppelman nasceu? | Charles Koppelman nasceu em Brooklyn. | template | Charles Koppelman nasceu em Nova York. ❌ |
| 18 | Qual é o país de origem de Crusader? | Crusader (El rapto) é um filme do Estados Unidos. | template | O país de origem de Crusader é os Estados Unidos. |
| 19 | Qual é o país de origem de El Greco? | El Greco (El rapto) é um filme do Espanha. | template | O filme El Greco é da Grécia. ❌ |
| 20 | Quem nasceu em Stoke-on-Trent? | Robbie Williams nasceu em Stoke-on-Trent. ❌ | llm | Robbie Williams nasceu em Stoke-on-Trent. ❌ |
