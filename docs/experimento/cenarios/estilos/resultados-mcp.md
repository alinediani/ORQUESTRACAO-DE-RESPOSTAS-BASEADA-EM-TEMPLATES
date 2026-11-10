# Resultados do experimento — cenário estilos

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "estilos"}` · 50 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 10 | 50 | 80,0% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 80,0% (40) | — | |
| Tokens | 957 | 4.675 | 79,5% |
| Custo estimado | US$ 0,0080 | US$ 0,04 | 78,9% |
| Respostas corretas (gabarito) | 100,0% (50) | 100,0% (50) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 0, linha de base melhor em 0, empate em 50 de 50.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| cordel/capital | 5 | 1 | 4 (80,0%) |
| cordel/simbolo | 5 | 1 | 4 (80,0%) |
| crianca/capital | 5 | 1 | 4 (80,0%) |
| crianca/simbolo | 5 | 1 | 4 (80,0%) |
| novela/capital | 5 | 1 | 4 (80,0%) |
| novela/simbolo | 5 | 1 | 4 (80,0%) |
| pirata/capital | 5 | 1 | 4 (80,0%) |
| pirata/simbolo | 5 | 1 | 4 (80,0%) |
| teleprompter/capital | 5 | 1 | 4 (80,0%) |
| teleprompter/simbolo | 5 | 1 | 4 (80,0%) |

Templates salvos na base chave-valor: 10.

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Responda como numa cena de novela: Qual é a capital da Itália? | Ah, Giovanna, não me olhe assim… a capital da Itália é Roma, a cidade onde nosso amor começou e onde tudo vai terminar! | llm | Ah, Giovanna, não me olhe assim… a capital da Itália é Roma, a cidade onde nosso amor começou e onde tudo vai terminar! |
| 2 | Explique para uma criança de 5 anos: Qual é a capital da França? | Sabe a França, aquele país lá longe? A cidade mais importante dela, onde fica o governo, se chama Paris, e tem uma torre gigante de ferro! | llm | Sabe a França, aquele país lá longe? A cidade mais importante dela, onde fica o governo, se chama Paris, e tem uma torre gigante de ferro! |
| 3 | Explique para uma criança de 5 anos: Qual é o símbolo químico do ferro? | Cada coisinha do mundo tem um apelido curtinho, e o apelido do ferro é Fe, de um nome antigo em latim. | llm | Cada coisinha do mundo tem um apelido curtinho, e o apelido do ferro é Fe, de um nome antigo em latim. |
| 4 | Responda em estilo teleprompter, como um âncora de telejornal: Qual é o símbolo químico do sódio? | Boa noite. O símbolo químico do sódio é Na, do latim natrium. Voltamos já com mais informações. | llm | Boa noite. O símbolo químico do sódio é Na, do latim natrium. Voltamos já com mais informações. |
| 5 | Responda como um pirata: Qual é o símbolo químico do ouro? | Arr, marujo! O símbolo químico do ouro é Au, o mesmo tesouro que enche meu baú! | llm | Arr, marujo! O símbolo químico do ouro é Au, o mesmo tesouro que enche meu baú! |
| 6 | Responda como numa cena de novela: Qual é a capital do Canadá? | Ah, Giovanna, não me olhe assim… a capital do Canadá é Ottawa, a cidade onde nosso amor começou e onde tudo vai terminar! | template | Você mentiu pra mim esse tempo todo, Ricardo! A capital do Canadá não é Toronto, é Ottawa… e eu descobri tudo! |
| 7 | Responda em estilo teleprompter, como um âncora de telejornal: Qual é a capital do Canadá? | Boa noite. A capital do Canadá é Ottawa, na província de Ontário. Esta foi a notícia, até a próxima. | llm | Boa noite. A capital do Canadá é Ottawa, na província de Ontário. Esta foi a notícia, até a próxima. |
| 8 | Responda como numa cena de novela: Qual é o símbolo químico do ferro? | Eu sei o seu segredo, Helena… o símbolo químico do ferro é Fe, e essa verdade vai mudar a nossa família para sempre! | llm | Eu sei o seu segredo, Helena… o símbolo químico do ferro é Fe, e essa verdade vai mudar a nossa família para sempre! |
| 9 | Responda em forma de cordel: Qual é o símbolo químico do ferro? | Lá no sertão do saber / o ferro tem seu sinal: / é Fe que se vai escrever, / do latim, que é original. | llm | Lá no sertão do saber / o ferro tem seu sinal: / é Fe que se vai escrever, / do latim, que é original. |
| 10 | Responda como numa cena de novela: Qual é a capital do Japão? | Ah, Giovanna, não me olhe assim… a capital do Japão é Tóquio, a cidade onde nosso amor começou e onde tudo vai terminar! | template | Não adianta fugir, Mariana: o avião parte amanhã para Tóquio, a capital do Japão, e eu vou estar nele! |
| 11 | Responda em forma de cordel: Qual é a capital do Canadá? | Vou contar com alegria / o que o Canadá contém: / Ottawa é a capital / e o povo a recebe bem. | llm | Vou contar com alegria / o que o Canadá contém: / Ottawa é a capital / e o povo a recebe bem. |
| 12 | Responda como numa cena de novela: Qual é a capital da França? | Ah, Giovanna, não me olhe assim… a capital da França é Paris, a cidade onde nosso amor começou e onde tudo vai terminar! | template | Eu vou embora pra Paris, a capital da França, e você nunca mais vai me ver, Alberto! |
| 13 | Explique para uma criança de 5 anos: Qual é o símbolo químico do sódio? | Cada coisinha do mundo tem um apelido curtinho, e o apelido do sódio é Na, de um nome antigo em latim. | template | O sódio tem um nome curtinho que os cientistas usam: Na, como se fosse o apelido dele. |
| 14 | Responda como um pirata: Qual é a capital da França? | Arr! A capital da França é Paris, marujo, terra de queijo fino e vinho que não falta no porão! | llm | Arr! A capital da França é Paris, marujo, terra de queijo fino e vinho que não falta no porão! |
| 15 | Responda em forma de cordel: Qual é a capital do Japão? | Vou contar com alegria / o que o Japão contém: / Tóquio é a capital / e o povo a recebe bem. | template | No Japão do sol nascente / tem cidade de primeira: / Tóquio é a capital / iluminada a noite inteira. |
| 16 | Responda em estilo teleprompter, como um âncora de telejornal: Qual é o símbolo químico do ferro? | Boa noite. O símbolo químico do ferro é Fe, do latim natrium. Voltamos já com mais informações. | template | Boa noite. O símbolo químico do ferro é Fe, do latim ferrum. A seguir, outros destaques da ciência. |
| 17 | Explique para uma criança de 5 anos: Qual é a capital do Canadá? | Sabe o Canadá, aquele país lá longe? A cidade mais importante dela, onde fica o governo, se chama Ottawa, e tem uma torre gigante de ferro! | template | O Canadá é um país bem grande e friozinho, e a cidade que manda nele, a capital, se chama Ottawa. |
| 18 | Responda como numa cena de novela: Qual é o símbolo químico da prata? | Eu sei o seu segredo, Helena… o símbolo químico da prata é Ag, e essa verdade vai mudar a nossa família para sempre! | template | Não chore, Beatriz… o símbolo químico da prata é Ag, e a aliança que eu te dei é de prata pura, como o nosso amor! |
| 19 | Responda como um pirata: Qual é a capital do Japão? | Arr! A capital do Japão é Tóquio, marujo, terra de queijo fino e vinho que não falta no porão! | template | Arr, grumete! A capital do Japão é Tóquio, onde ancoramos para trocar ouro por seda! |
| 20 | Responda em estilo teleprompter, como um âncora de telejornal: Qual é a capital da Itália? | Boa noite. A capital da Itália é Roma, na província de Ontário. Esta foi a notícia, até a próxima. | template | Boa noite. A capital da Itália é Roma, conhecida como a Cidade Eterna. Fique agora com a previsão do tempo. |
