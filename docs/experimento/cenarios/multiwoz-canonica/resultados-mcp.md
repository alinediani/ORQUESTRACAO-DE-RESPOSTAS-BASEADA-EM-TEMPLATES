# Resultados do experimento — cenário multiwoz-canonica

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "multiwoz-canonica"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 19 | 30 | 36,7% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 36,7% (11) | — | |
| Tokens | 2.468 | 3.899 | 36,7% |
| Custo estimado | US$ 0,01 | US$ 0,02 | 37,1% |
| Respostas corretas (gabarito) | 100,0% (30) | 100,0% (30) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 0, linha de base melhor em 0, empate em 30 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| atracao/address | 3 | 1 | 2 (66,7%) |
| atracao/area | 2 | 1 | 1 (50,0%) |
| atracao/area+address | 1 | 1 | 0 (0,0%) |
| atracao/entrancefee | 1 | 1 | 0 (0,0%) |
| atracao/phone | 5 | 1 | 4 (80,0%) |
| atracao/postcode | 1 | 1 | 0 (0,0%) |
| atracao/type | 1 | 1 | 0 (0,0%) |
| hotel/address | 1 | 1 | 0 (0,0%) |
| hotel/address+phone | 1 | 1 | 0 (0,0%) |
| hotel/parking | 1 | 1 | 0 (0,0%) |
| hotel/phone | 2 | 1 | 1 (50,0%) |
| hotel/postcode | 2 | 1 | 1 (50,0%) |
| restaurante/address | 3 | 1 | 2 (66,7%) |
| restaurante/area | 1 | 1 | 0 (0,0%) |
| restaurante/food | 1 | 1 | 0 (0,0%) |
| restaurante/food+phone+address | 1 | 1 | 0 (0,0%) |
| restaurante/phone | 1 | 1 | 0 (0,0%) |
| restaurante/postcode | 1 | 1 | 0 (0,0%) |
| restaurante/pricerange | 1 | 1 | 0 (0,0%) |

Templates salvos na base chave-valor: 18.

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Qual é o telefone de a and b guest house? | O telefone da a and b guest house é 01223315702. | llm | O telefone da a and b guest house é 01223315702. |
| 2 | Qual é o telefone de all saints church? | O telefone da all saints church é 01223452587. | llm | O telefone da all saints church é 01223452587. |
| 3 | Qual é a faixa de preço de meghna? | A faixa de preço do Meghna é moderada. | llm | A faixa de preço do Meghna é moderada. |
| 4 | Qual é o endereço de la mimosa? | O endereço do La Mimosa é Thompsons Lane Fen Ditton. | llm | O endereço do La Mimosa é Thompsons Lane Fen Ditton. |
| 5 | Qual é o endereço e o telefone de alexander bed and breakfast? | O endereço do alexander bed and breakfast é 56 saint barnabas road, e o telefone é 01223525725. | llm | O endereço do alexander bed and breakfast é 56 saint barnabas road, e o telefone é 01223525725. |
| 6 | Qual é o endereço de whipple museum of the history of science? | O endereço do whipple museum of the history of science é free school lane. | llm | O endereço do whipple museum of the history of science é free school lane. |
| 7 | Qual é o endereço de ashley hotel? | O endereço do ashley hotel é 74 chesterton road. | llm | O endereço do ashley hotel é 74 chesterton road. |
| 8 | Qual é a região de regency gallery? | A regency gallery fica na região centro. | llm | A regency gallery fica na região centro. |
| 9 | Qual é o CEP de express by holiday inn cambridge? | O CEP do express by holiday inn cambridge é cb13lh, no endereço 15-17 norman way, coldhams business park. | llm | O CEP do express by holiday inn cambridge é cb13lh, no endereço 15-17 norman way, coldhams business park. |
| 10 | Diga se tem estacionamento de hobsons house? | Sim, o hobsons house tem estacionamento gratuito. | llm | Sim, o hobsons house tem estacionamento gratuito. |
| 11 | Qual é o CEP de camboats? | O CEP do camboats é cb58sx; o cadastro não informa o preço da entrada. | llm | O CEP do camboats é cb58sx; o cadastro não informa o preço da entrada. |
| 12 | Qual é o telefone de gonville hotel? | O telefone da gonville hotel é 01223366611. | template | O telefone do gonville hotel é 01223366611. |
| 13 | Qual é o tipo de all saints church? | A all saints church é uma atração do tipo arquitetura. | llm | A all saints church é uma atração do tipo arquitetura. |
| 14 | Qual é o CEP de carolina bed and breakfast? | O CEP do carolina bed and breakfast é cb13nx, no endereço 138 perne road. | template | O CEP do carolina bed and breakfast é cb13nx. |
| 15 | Qual é o endereço de wandlebury country park? | O endereço do wandlebury country park é wandlebury ring, gog magog hills, babraham. | template | O endereço do wandlebury country park é wandlebury ring, gog magog hills, babraham. |
| 16 | Qual é o endereço de bedouin? | O endereço do bedouin é 100 Mill Road City Centre. | template | O endereço do Bedouin é 100 Mill Road City Centre, e o telefone é 01223367660. |
| 17 | Qual é o endereço de abbey pool and astroturf pitch? | O endereço do abbey pool and astroturf pitch é pool way, whitehill road, off newmarket road. | template | O endereço do abbey pool and astroturf pitch é pool way, whitehill road, off newmarket road. |
| 18 | Qual é o tipo de comida de curry garden? | O curry garden serve comida indiana. | llm | O curry garden serve comida indiana. |
| 19 | Qual é o tipo de comida e o telefone e o endereço de graffiti? | O Graffiti serve comida britânica; o telefone é 01223277977 e o endereço é Hotel Felix Whitehouse Lane Huntingdon Road. | llm | O Graffiti serve comida britânica; o telefone é 01223277977 e o endereço é Hotel Felix Whitehouse Lane Huntingdon Road. |
| 20 | Qual é o endereço de sesame restaurant and bar? | O endereço do sesame restaurant and bar é 17 Hills Road City Centre. | template | O sesame restaurant and bar fica em 17 Hills Road City Centre, com telefone 01223358899 e CEP cb21nw. |
