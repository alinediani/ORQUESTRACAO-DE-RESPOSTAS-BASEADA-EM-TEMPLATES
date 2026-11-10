# Resultados do experimento — cenário multiwoz-fiel

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "multiwoz-fiel"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 30 | 30 | 0,0% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 0,0% (0) | — | |
| Tokens | 3.966 | 3.966 | 0,0% |
| Custo estimado | US$ 0,02 | US$ 0,02 | 0,0% |
| Respostas corretas (gabarito) | 100,0% (30) | 100,0% (30) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 0, linha de base melhor em 0, empate em 30 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| atracao/address | 3 | 3 | 0 (0,0%) |
| atracao/area | 2 | 2 | 0 (0,0%) |
| atracao/area+address | 1 | 1 | 0 (0,0%) |
| atracao/entrancefee | 1 | 1 | 0 (0,0%) |
| atracao/phone | 5 | 5 | 0 (0,0%) |
| atracao/postcode | 1 | 1 | 0 (0,0%) |
| atracao/type | 1 | 1 | 0 (0,0%) |
| hotel/address | 1 | 1 | 0 (0,0%) |
| hotel/address+phone | 1 | 1 | 0 (0,0%) |
| hotel/parking | 1 | 1 | 0 (0,0%) |
| hotel/phone | 2 | 2 | 0 (0,0%) |
| hotel/postcode | 2 | 2 | 0 (0,0%) |
| restaurante/address | 3 | 3 | 0 (0,0%) |
| restaurante/area | 1 | 1 | 0 (0,0%) |
| restaurante/food | 1 | 1 | 0 (0,0%) |
| restaurante/food+phone+address | 1 | 1 | 0 (0,0%) |
| restaurante/phone | 1 | 1 | 0 (0,0%) |
| restaurante/postcode | 1 | 1 | 0 (0,0%) |
| restaurante/pricerange | 1 | 1 | 0 (0,0%) |

Templates salvos na base chave-valor: 29.

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Obrigado. Posso obter também o telefone? | O telefone da a and b guest house é 01223315702. | llm | O telefone da a and b guest house é 01223315702. |
| 2 | Posso ter o telefone da All Saints Church, por favor? | O telefone da all saints church é 01223452587. | llm | O telefone da all saints church é 01223452587. |
| 3 | Qual é a faixa de preço do Meghna? | A faixa de preço do Meghna é moderada. | llm | A faixa de preço do Meghna é moderada. |
| 4 | Qual é o endereço? | O endereço do La Mimosa é Thompsons Lane Fen Ditton. | llm | O endereço do La Mimosa é Thompsons Lane Fen Ditton. |
| 5 | não, obrigado. Eu só preciso do endereço e do telefone deles | O endereço do alexander bed and breakfast é 56 saint barnabas road, e o telefone é 01223525725. | llm | O endereço do alexander bed and breakfast é 56 saint barnabas road, e o telefone é 01223525725. |
| 6 | Você pode me dizer o endereço também? | O endereço do whipple museum of the history of science é free school lane. | llm | O endereço do whipple museum of the history of science é free school lane. |
| 7 | Eu escolho o ashley hotel. Qual é o endereço deles, por favor? | O endereço do ashley hotel é 74 chesterton road. | llm | O endereço do ashley hotel é 74 chesterton road. |
| 8 | Só preciso saber em que região fica. | A regency gallery fica na região centro. | llm | A regency gallery fica na região centro. |
| 9 | Sim, por favor, reserve o hotel e dê o endereço com o CEP. Obrigado. | O CEP do express by holiday inn cambridge é cb13lh, no endereço 15-17 norman way, coldhams business park. | llm | O CEP do express by holiday inn cambridge é cb13lh, no endereço 15-17 norman way, coldhams business park. |
| 10 | Eles têm estacionamento gratuito? | Sim, o hobsons house tem estacionamento gratuito. | llm | Sim, o hobsons house tem estacionamento gratuito. |
| 11 | Parece bom. Eles têm preço de entrada, e por favor me forneça o CEP. | O CEP do camboats é cb58sx; o cadastro não informa o preço da entrada. | llm | O CEP do camboats é cb58sx; o cadastro não informa o preço da entrada. |
| 12 | Na verdade, eu só preciso do telefone deles, por favor. | O telefone do gonville hotel é 01223366611. | llm | O telefone do gonville hotel é 01223366611. |
| 13 | sim, vou precisar do tipo de atração, por favor. | A all saints church é uma atração do tipo arquitetura. | llm | A all saints church é uma atração do tipo arquitetura. |
| 14 | Poderia ter o código postal? | O CEP do carolina bed and breakfast é cb13nx. | llm | O CEP do carolina bed and breakfast é cb13nx. |
| 15 | Qual é o endereço, por favor? | O endereço do wandlebury country park é wandlebury ring, gog magog hills, babraham. | llm | O endereço do wandlebury country park é wandlebury ring, gog magog hills, babraham. |
| 16 | Isso seria bom. Posso obter o endereço e o telefone? | O endereço do Bedouin é 100 Mill Road City Centre, e o telefone é 01223367660. | llm | O endereço do Bedouin é 100 Mill Road City Centre, e o telefone é 01223367660. |
| 17 | Você poderia também me dizer o endereço da Abbey Pool? | O endereço do abbey pool and astroturf pitch é pool way, whitehill road, off newmarket road. | llm | O endereço do abbey pool and astroturf pitch é pool way, whitehill road, off newmarket road. |
| 18 | Não preciso de uma reserva agora. Que tipo de comida eles servem? | O curry garden serve comida indiana. | llm | O curry garden serve comida indiana. |
| 19 | Você pode me dizer o tipo de comida, assim como o telefone e o endereço? | O Graffiti serve comida britânica; o telefone é 01223277977 e o endereço é Hotel Felix Whitehouse Lane Huntingdon Road. | llm | O Graffiti serve comida britânica; o telefone é 01223277977 e o endereço é Hotel Felix Whitehouse Lane Huntingdon Road. |
| 20 | Posso ter uma segunda recomendação com o telefone e o CEP deles? | O sesame restaurant and bar fica em 17 Hills Road City Centre, com telefone 01223358899 e CEP cb21nw. | llm | O sesame restaurant and bar fica em 17 Hills Road City Centre, com telefone 01223358899 e CEP cb21nw. |
