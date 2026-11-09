# Resultados do experimento — cenário mkqa

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "mkqa"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 30 | 30 | 0,0% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 0,0% (0) | — | |
| Tokens | 2.494 | 2.494 | 0,0% |
| Custo estimado | US$ 0,02 | US$ 0,02 | 0,0% |
| Respostas corretas (gabarito) | 66,7% (20) | 66,7% (20) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 0, linha de base melhor em 0, empate em 30 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| date | 1 | 1 | 0 (0,0%) |
| entity | 22 | 22 | 0 (0,0%) |
| number | 3 | 3 | 0 (0,0%) |
| number_with_unit | 4 | 4 | 0 (0,0%) |

Templates salvos na base chave-valor: 0.

## Respostas que não trazem o gabarito (para conferir na avaliação humana)

| # | Pergunta | Gabarito | Protótipo | Linha de base |
|---:|---|---|---|---|
| 1 | qual é o recorde de temperatura mais quente na terra | 134.1 | O recorde de temperatura mais quente na Terra é de 56,7 °C (134 °F), registrado no Vale da Morte, na Califórnia, em 1913. ❌ | O recorde de temperatura mais quente na Terra é de 56,7 °C (134 °F), registrado no Vale da Morte, na Califórnia, em 1913. ❌ |
| 2 | quem canta that's what i like about you | The Romantics | "That's What I Like About You" é cantada por Trisha Yearwood. ❌ | "That's What I Like About You" é cantada por Trisha Yearwood. ❌ |
| 7 | em qual temporada os amigos Mônica e Chandler se juntam | Friends (4.ª temporada) \|\| 4.0 \|\| 4 | Mônica e Chandler ficam juntos no final da quarta temporada de Friends, no casamento de Ross em Londres. ❌ | Mônica e Chandler ficam juntos no final da quarta temporada de Friends, no casamento de Ross em Londres. ❌ |
| 8 | qual é a medida de um colchão queen size | 6080 | Um colchão queen size mede 60 por 80 polegadas (cerca de 152 por 203 cm). ❌ | Um colchão queen size mede 60 por 80 polegadas (cerca de 152 por 203 cm). ❌ |
| 10 | quem atuou no filme o reencontro | William Hurt \|\| Glenn Close \|\| Jeff Goldblum \|\| Kevin Kline \|\| Tom Berenger | O filme The Reunion (2011) tem no elenco John Cena, Ethan Embry, Boyd Holbrook e Amy Smart. ❌ | O filme The Reunion (2011) tem no elenco John Cena, Ethan Embry, Boyd Holbrook e Amy Smart. ❌ |
| 17 | barragens no rio missouri em dakota do sul | Represa de Fort Randall \|\| Represa de Big Bend \|\| represa de Oahe | No rio Missouri, em Dakota do Sul, ficam as barragens de Oahe, Big Bend, Fort Randall e Gavins Point. ❌ | No rio Missouri, em Dakota do Sul, ficam as barragens de Oahe, Big Bend, Fort Randall e Gavins Point. ❌ |
| 24 | quantas calorias tem em um soft serve de sorvete | 191 | Uma porção de sorvete soft serve (uma xícara) tem cerca de 220 calorias. ❌ | Uma porção de sorvete soft serve (uma xícara) tem cerca de 220 calorias. ❌ |
| 25 | qual é a velocidade do som ao nível do mar | 344 | Ao nível do mar, a 15 °C, a velocidade do som é de cerca de 340 m/s (1.225 km/h, ou 761 mph). ❌ | Ao nível do mar, a 15 °C, a velocidade do som é de cerca de 340 m/s (1.225 km/h, ou 761 mph). ❌ |
| 26 | quem foi o último presidente a sofrer impeachment | Bill Clinton | O último presidente a sofrer impeachment foi Donald Trump, em 2019 e de novo em 2021. ❌ | O último presidente a sofrer impeachment foi Donald Trump, em 2019 e de novo em 2021. ❌ |
| 27 | quantos anos tem o vocalista do walk the moon | 32 | Nicholas Petricca, vocalista do Walk the Moon, nasceu em 1987 e tem cerca de 38 anos. ❌ | Nicholas Petricca, vocalista do Walk the Moon, nasceu em 1987 e tem cerca de 38 anos. ❌ |

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | qual é o recorde de temperatura mais quente na terra | O recorde de temperatura mais quente na Terra é de 56,7 °C (134 °F), registrado no Vale da Morte, na Califórnia, em 1913. ❌ | llm | O recorde de temperatura mais quente na Terra é de 56,7 °C (134 °F), registrado no Vale da Morte, na Califórnia, em 1913. ❌ |
| 2 | quem canta that's what i like about you | "That's What I Like About You" é cantada por Trisha Yearwood. ❌ | llm | "That's What I Like About You" é cantada por Trisha Yearwood. ❌ |
| 3 | qual é o maior rio dos estados unidos | O maior rio dos Estados Unidos é o Rio Missouri. | llm | O maior rio dos Estados Unidos é o Rio Missouri. |
| 4 | quem escreveu i can't make you love me | "I Can't Make You Love Me" foi escrita por Mike Reid e Allen Shamblin. | llm | "I Can't Make You Love Me" foi escrita por Mike Reid e Allen Shamblin. |
| 5 | quem interpretou o pantera negra no filme pantera negra | O Pantera Negra foi interpretado por Chadwick Boseman no filme Pantera Negra. | llm | O Pantera Negra foi interpretado por Chadwick Boseman no filme Pantera Negra. |
| 6 | quem venceu o Miami Dolphins em 1972 no Super Bowl | No Super Bowl VII, da temporada de 1972, o Miami Dolphins venceu o Washington Redskins. | llm | No Super Bowl VII, da temporada de 1972, o Miami Dolphins venceu o Washington Redskins. |
| 7 | em qual temporada os amigos Mônica e Chandler se juntam | Mônica e Chandler ficam juntos no final da quarta temporada de Friends, no casamento de Ross em Londres. ❌ | llm | Mônica e Chandler ficam juntos no final da quarta temporada de Friends, no casamento de Ross em Londres. ❌ |
| 8 | qual é a medida de um colchão queen size | Um colchão queen size mede 60 por 80 polegadas (cerca de 152 por 203 cm). ❌ | llm | Um colchão queen size mede 60 por 80 polegadas (cerca de 152 por 203 cm). ❌ |
| 9 | quando foi a última vez que israel ganhou a eurovision | A última vez que Israel ganhou a Eurovision foi em 2018, com Netta. | llm | A última vez que Israel ganhou a Eurovision foi em 2018, com Netta. |
| 10 | quem atuou no filme o reencontro | O filme The Reunion (2011) tem no elenco John Cena, Ethan Embry, Boyd Holbrook e Amy Smart. ❌ | llm | O filme The Reunion (2011) tem no elenco John Cena, Ethan Embry, Boyd Holbrook e Amy Smart. ❌ |
| 11 | quem fez as vozes do rei leão | As vozes de O Rei Leão (1994) foram feitas por Matthew Broderick, James Earl Jones, Jeremy Irons, Nathan Lane, Ernie Sabella, Whoopi Goldberg, Rowan Atkinson e Moira Kelly, entre outros. | llm | As vozes de O Rei Leão (1994) foram feitas por Matthew Broderick, James Earl Jones, Jeremy Irons, Nathan Lane, Ernie Sabella, Whoopi Goldberg, Rowan Atkinson e Moira Kelly, entre outros. |
| 12 | personagem do john goodman em o grande lebowski | O personagem de John Goodman em O Grande Lebowski é Walter Sobchak. | llm | O personagem de John Goodman em O Grande Lebowski é Walter Sobchak. |
| 13 | Arizona é um estado vermelho ou azul? | O Arizona é tradicionalmente um estado vermelho (republicano), embora tenha votado no candidato democrata em 2020. | llm | O Arizona é tradicionalmente um estado vermelho (republicano), embora tenha votado no candidato democrata em 2020. |
| 14 | qual é a comida mais popular no Canadá | A comida mais popular do Canadá é a poutine. | llm | A comida mais popular do Canadá é a poutine. |
| 15 | qual o nome do cachorro da rca victor | O cachorro da RCA Victor se chama Nipper. | llm | O cachorro da RCA Victor se chama Nipper. |
| 16 | quais são os nomes das quatro tartarugas ninjas | As quatro Tartarugas Ninja são Leonardo, Michelangelo, Donatello e Raphael. | llm | As quatro Tartarugas Ninja são Leonardo, Michelangelo, Donatello e Raphael. |
| 17 | barragens no rio missouri em dakota do sul | No rio Missouri, em Dakota do Sul, ficam as barragens de Oahe, Big Bend, Fort Randall e Gavins Point. ❌ | llm | No rio Missouri, em Dakota do Sul, ficam as barragens de Oahe, Big Bend, Fort Randall e Gavins Point. ❌ |
| 18 | quem canta did i shave my legs for this | "Did I Shave My Legs for This?" é cantada por Deana Carter. | llm | "Did I Shave My Legs for This?" é cantada por Deana Carter. |
| 19 | quantas vitórias os dodgers tem nessa temporada | Não sei o número da temporada atual dos Dodgers; na temporada regular de 2019, eles tiveram 106 vitórias. | llm | Não sei o número da temporada atual dos Dodgers; na temporada regular de 2019, eles tiveram 106 vitórias. |
| 20 | quem canta you're just too good to be true | "Can't Take My Eyes Off You" ("You're just too good to be true") é cantada por Frankie Valli. | llm | "Can't Take My Eyes Off You" ("You're just too good to be true") é cantada por Frankie Valli. |
