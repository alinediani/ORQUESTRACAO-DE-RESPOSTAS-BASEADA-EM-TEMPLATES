# Resultados do experimento — cenário quiz-fiel

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "quiz-fiel"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 26 | 30 | 13,3% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 13,3% (4) | — | |
| Tokens | 1.886 | 2.181 | 13,5% |
| Custo estimado | US$ 0,01 | US$ 0,01 | 14,1% |
| Respostas corretas (gabarito) | 56,7% (17) | 50,0% (15) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 2, linha de base melhor em 0, empate em 28 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| P136 | 6 | 6 | 0 (0,0%) |
| P19 | 6 | 4 | 2 (33,3%) |
| P27 | 6 | 4 | 2 (33,3%) |
| P495 | 6 | 6 | 0 (0,0%) |
| R19 | 6 | 3 | 0 (0,0%) |

Templates salvos na base chave-valor: 11.

## Respostas que não trazem o gabarito (para conferir na avaliação humana)

| # | Pergunta | Gabarito | Protótipo | Linha de base |
|---:|---|---|---|---|
| 5 | qual é o gênero do álbum Why Is There Air?? | comédia stand-up \|\| Stand-up \|\| Comédia stand up \|\| Stand-up Comedy \|\| Stand Up Comedy \|\| stand-up comedy | Why Is There Air? é um álbum de comédia. ❌ | Why Is There Air? é um álbum de comédia. ❌ |
| 8 | Quem nasceu em Phalaborwa | Dale Steyn | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ |
| 9 | quem nasceu em Helsinque | Abba Gindin | Linus Torvalds nasceu em Helsinque. ❌ | Linus Torvalds nasceu em Helsinque. ❌ |
| 11 | Qual o gênero do filme Saturday Night? | filme mudo \|\| cinema mudo \|\| filmes mudos \|\| silent film | Saturday Night é um filme de drama. ❌ | Saturday Night é um filme de drama. ❌ |
| 13 | a que tipo de música Colloquio está ligado | música experimental \|\| Experimental music \|\| Musica experimental \|\| experimental music | Não sei a que gênero musical Colloquio está ligado. ❌ | Não sei a que gênero musical Colloquio está ligado. ❌ |
| 16 | Em que gênero está Theocracy? | estratégia em tempo real \|\| estratégia \|\| real-time strategy | Theocracy é uma banda de power metal. ❌ | Theocracy é uma banda de power metal. ❌ |
| 17 | onde Charles Koppelman nasceu | Brooklyn \|\| Condado de Kings | Charles Koppelman nasceu em Brooklyn. | Charles Koppelman nasceu em Nova York. ❌ |
| 19 | De onde é o filme El Greco | Espanha \|\| Reino da Espanha \|\| Reino de Espanha \|\| Spain | O filme El Greco é da Grécia. ❌ | O filme El Greco é da Grécia. ❌ |
| 20 | cite uma pessoa que nasceu em Stoke-on-Trent. | Clint Boulton | Robbie Williams nasceu em Stoke-on-Trent. ❌ | Robbie Williams nasceu em Stoke-on-Trent. ❌ |
| 23 | onde Hans Günther van Allen nasceu | Haan | Hans Günther van Allen nasceu em Haan. | Não sei onde Hans Günther van Allen nasceu. ❌ |
| 24 | quem foi um jogador de críquete famoso nascido em Woolwich | Peter Jones | Não sei citar com segurança uma pessoa nascida em Woolwich. ❌ | Não sei citar com segurança uma pessoa nascida em Woolwich. ❌ |
| 25 | qual é o local de nascimento de Sergio Spina | Milão \|\| Milan | Não sei com segurança onde Sergio Spina nasceu. ❌ | Não sei com segurança onde Sergio Spina nasceu. ❌ |
| 26 | Quem nasceu em Nancy? | Henri Cartan | Hubert Lyautey nasceu em Nancy. ❌ | Hubert Lyautey nasceu em Nancy. ❌ |
| 27 | quem nasceu em Srinagar | Amitabh Mattoo | Farooq Abdullah nasceu em Srinagar. ❌ | Farooq Abdullah nasceu em Srinagar. ❌ |
| 28 | em que cidade Barry Ferguson nasceu | Dublin | Barry Ferguson nasceu em Glasgow. ❌ | Barry Ferguson nasceu em Glasgow. ❌ |

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | The Rapture é do México ou do Brasil | The Rapture (El rapto) é um filme do México. | llm | The Rapture (El rapto) é um filme do México. |
| 2 | onde Victoria No. 203 foi feito? | Victoria No. 203 é um filme da Índia. | llm | Victoria No. 203 é um filme da Índia. |
| 3 | que categoria de filme é Something for the Boys | Something for the Boys é um filme musical. | llm | Something for the Boys é um filme musical. |
| 4 | Qual é o país de origem do programa The New Fred and Barney Show | O país de origem de The New Fred and Barney Show é os Estados Unidos. | llm | O país de origem de The New Fred and Barney Show é os Estados Unidos. |
| 5 | qual é o gênero do álbum Why Is There Air?? | Why Is There Air? é um álbum de comédia. ❌ | llm | Why Is There Air? é um álbum de comédia. ❌ |
| 6 | Em que país Malouf Abraham, Sr. nasceu | Malouf Abraham, Sr. é dos Estados Unidos. | llm | Malouf Abraham, Sr. é dos Estados Unidos. |
| 7 | em qual país asiático TV Patrol Northwestern Mindanao é exibido | TV Patrol Northwestern Mindanao é exibido nas Filipinas. | llm | TV Patrol Northwestern Mindanao é exibido nas Filipinas. |
| 8 | Quem nasceu em Phalaborwa | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ | llm | Não sei citar com segurança uma pessoa nascida em Phalaborwa. ❌ |
| 9 | quem nasceu em Helsinque | Linus Torvalds nasceu em Helsinque. ❌ | llm | Linus Torvalds nasceu em Helsinque. ❌ |
| 10 | Onde Fawzi al-Mulki nasceu | Fawzi al-Mulki nasceu em Irbid. | llm | Fawzi al-Mulki nasceu em Irbid. |
| 11 | Qual o gênero do filme Saturday Night? | Saturday Night é um filme de drama. ❌ | llm | Saturday Night é um filme de drama. ❌ |
| 12 | de que país é Mary Lou Fallis | Mary Lou Fallis é do Canadá. | llm | Mary Lou Fallis é do Canadá. |
| 13 | a que tipo de música Colloquio está ligado | Não sei a que gênero musical Colloquio está ligado. ❌ | llm | Não sei a que gênero musical Colloquio está ligado. ❌ |
| 14 | em que cidade Vernee Watson-Johnson nasceu | Vernee Watson-Johnson nasceu em Trenton. | llm | Vernee Watson-Johnson nasceu em Trenton. |
| 15 | que tipo de livro é The Good Companions? | The Good Companions é um romance. | llm | The Good Companions é um romance. |
| 16 | Em que gênero está Theocracy? | Theocracy é uma banda de power metal. ❌ | llm | Theocracy é uma banda de power metal. ❌ |
| 17 | onde Charles Koppelman nasceu | Charles Koppelman nasceu em Brooklyn. | template | Charles Koppelman nasceu em Nova York. ❌ |
| 18 | qual é o país de origem de Crusader | O país de origem de Crusader é os Estados Unidos. | llm | O país de origem de Crusader é os Estados Unidos. |
| 19 | De onde é o filme El Greco | O filme El Greco é da Grécia. ❌ | llm | O filme El Greco é da Grécia. ❌ |
| 20 | cite uma pessoa que nasceu em Stoke-on-Trent. | Robbie Williams nasceu em Stoke-on-Trent. ❌ | llm | Robbie Williams nasceu em Stoke-on-Trent. ❌ |
