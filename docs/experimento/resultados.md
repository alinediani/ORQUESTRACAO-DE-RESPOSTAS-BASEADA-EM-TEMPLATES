# Resultados do experimento

Gerado por `dotnet run --project src/Orquestracao.Experimento -- --llm simulado` · semente 2026 · 150 perguntas em 6 famílias de mesma estrutura.

> **LLM simulado.** O LLM de geração responde pela base de fatos e erra 5,0% das vezes (troca o dado pelo de outra entidade, simulando alucinação). Os tokens são estimados pelo tamanho do texto (≈ 4 caracteres por token), o custo usa o preço do Claude Sonnet 5.5 (US$ 2 / US$ 10 por milhão de tokens) e a latência soma 1200 ms por chamada ao LLM ao tempo medido do código. A comparação cega usa um juiz pelo gabarito. Para os números com o modelo real, rode com `--llm api` (precisa de credencial da Anthropic).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 6 | 150 | 96,0% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 96,0% (144) | — | |
| Tokens | 413 | 10.212 | 96,0% |
| Custo estimado | US$ 0,0012 | US$ 0,03 | 95,9% |
| Latência média estimada | 49 ms | 1201 ms | 95,9% |
| Respostas corretas (gabarito) | 100,0% (150) | 94,0% (141) | |

**Comparação cega (T06):** protótipo melhor em 9, linha de base melhor em 0, empate em 141 de 150.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| capital | 51 | 1 | 50 (98,0%) |
| moeda | 26 | 1 | 25 (96,2%) |
| idioma | 15 | 1 | 14 (93,3%) |
| continente | 15 | 1 | 14 (93,3%) |
| simbolo | 24 | 1 | 23 (95,8%) |
| numero | 19 | 1 | 18 (94,7%) |

Templates salvos na base chave-valor: 6.

## Testes do Quadro 2

| Teste | Resultado obtido | Situação |
|---|---|---|
| T01 | 6 famílias, uma chave por família (capital: 1, idioma: 1, simbolo: 1, continente: 1, moeda: 1, numero: 1) | Aprovado |
| T02 | A primeira pergunta de cada família foi ao LLM e gerou template com peso 0,5 (6 templates salvos para 6 famílias) | Aprovado |
| T03 | 144 de 150 perguntas (96,0%) montadas pelo template, sem o LLM de geração | Aprovado |
| T04 | Coberto pelos testes automatizados (OrquestradorTests.T04): o peso cai 0,50 → 0,35 → 0,245 e o template é descartado no 3º feedback ruim (0,17 < 0,20); a pergunta seguinte volta ao LLM | Aprovado |
| T05 | 6 chamadas ao LLM no protótipo contra 150 na linha de base (redução de 96,0%) | Aprovado |
| T06 | Comparação cega: protótipo melhor em 9, linha de base em 0, empate em 141; corretas: 100,0% × 94,0% | Aprovado |

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Qual a capital da Argentina? | A capital da Argentina é Buenos Aires. | llm | A capital da Argentina é Buenos Aires. |
| 2 | Qual é a capital da Alemanha? | A capital da Alemanha é Berlim. | template | A capital da Alemanha é Berlim. |
| 3 | Que língua se fala no México? | No México fala-se espanhol. | llm | No México fala-se espanhol. |
| 4 | Qual é o símbolo químico do oxigênio? | O símbolo químico do oxigênio é O. | llm | O símbolo químico do oxigênio é O. |
| 5 | Me diga a capital da Polônia. | A capital da Polônia é Varsóvia. | template | A capital da Polônia é Varsóvia. |
| 6 | Em que continente fica a Colômbia? | A Colômbia fica na América do Sul. | llm | A Colômbia fica na América do Sul. |
| 7 | Qual o símbolo químico da prata? | O símbolo químico da prata é Ag. | template | O símbolo químico da prata é Sn. ❌ |
| 8 | Qual é a capital do Vietnã? | A capital do Vietnã é Hanói. | template | A capital do Vietnã é Hanói. |
| 9 | Qual a moeda da Nigéria? | A moeda da Nigéria é o naira. | llm | A moeda da Nigéria é o naira. |
| 10 | Qual o símbolo químico do ouro? | O símbolo químico do ouro é Au. | template | O símbolo químico do ouro é Au. |
| 11 | Qual é a capital da Itália? | A capital da Itália é Roma. | template | A capital da Itália é Roma. |
| 12 | Qual é o número atômico do enxofre? | O número atômico do enxofre é 16. | llm | O número atômico do enxofre é 16. |
| 13 | Qual é o símbolo químico do hidrogênio? | O símbolo químico do hidrogênio é H. | template | O símbolo químico do hidrogênio é H. |
| 14 | Em que continente fica a China? | A China fica na Ásia. | template | A China fica na Ásia. |
| 15 | Qual o símbolo químico do ouro? | O símbolo químico do ouro é Au. | template | O símbolo químico do ouro é Au. |
| 16 | Qual é a capital do México? | A capital do México é Cidade do México. | template | A capital do México é Cidade do México. |
| 17 | Que língua se fala na China? | Na China fala-se mandarim. | template | Na China fala-se mandarim. |
| 18 | Qual é o número atômico do enxofre? | O número atômico do enxofre é 16. | template | O número atômico do enxofre é 16. |
| 19 | Qual é a capital do Egito? | A capital do Egito é Cairo. | template | A capital do Egito é Pequim. ❌ |
| 20 | Qual é a moeda do Canadá? | A moeda do Canadá é o dólar canadense. | template | A moeda do Canadá é o dólar canadense. |
