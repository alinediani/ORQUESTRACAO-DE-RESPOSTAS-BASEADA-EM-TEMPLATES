# Resultados do experimento

Gerado por `POST /api/experimento {"llm": "mcp"}` (com o Claude Code respondendo pelo /mcp) · semente 2026 · 150 perguntas em 6 famílias de mesma estrutura.

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) numa sessão do Claude Code conectada ao servidor MCP do protótipo: cada chamada ao LLM virou um turno pendente, respondido pela ferramenta `responder` a partir do conhecimento do próprio modelo (sem consultar a base de fatos). Os tokens são estimados pelo tamanho do texto (≈ 4 caracteres por token) e o custo usa o preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão de tokens). A latência não é comparável (a resposta passa pela sessão do Claude Code) e fica fora da tabela. A correção automática compara a resposta com o gabarito da base de fatos (palavra exata); as divergências estão listadas abaixo, e a comparação cega humana fica em `avaliacao-humana.csv`.

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 31 | 150 | 79,3% |
| Chamadas ao LLM do parser | 0 | — | |
| Chamadas ao validador (Claude Haiku 5.5) | 6 (1 recusados; 1.394 tokens, US$ 0,0002) | — | |
| Taxa de acerto de template | 79,3% (119) | — | |
| Tokens | 3.510 | 10.254 | 65,8% |
| Custo estimado | US$ 0,01 | US$ 0,06 | 78,4% |
| Respostas corretas (gabarito) | 100,0% (150) | 100,0% (150) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 0, linha de base melhor em 0, empate em 150 de 150.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| capital | 51 | 1 | 50 (98,0%) |
| moeda | 26 | 1 | 0 (0,0%) |
| idioma | 15 | 1 | 14 (93,3%) |
| continente | 15 | 1 | 14 (93,3%) |
| simbolo | 24 | 1 | 23 (95,8%) |
| numero | 19 | 1 | 18 (94,7%) |

Templates salvos na base chave-valor: 5.

## Testes do Quadro 2

| Teste | Resultado obtido | Situação |
|---|---|---|
| T01 | 6 famílias, uma chave por família (capital: 1, idioma: 1, simbolo: 1, continente: 1, moeda: 1, numero: 1) | Aprovado |
| T02 | A primeira pergunta de cada família foi ao LLM e gerou template com peso 0,5 (5 templates salvos para 6 famílias) | Reprovado |
| T03 | 119 de 150 perguntas (79,3%) montadas pelo template, sem o LLM de geração | Aprovado |
| T04 | Coberto pelos testes automatizados (OrquestradorTests.T04): o peso cai 0,50 → 0,35 → 0,245 e o template é descartado no 3º feedback ruim (0,17 < 0,20); a pergunta seguinte volta ao LLM | Aprovado |
| T05 | 31 chamadas ao LLM no protótipo contra 150 na linha de base (redução de 79,3%) | Aprovado |
| T06 | Comparação cega: protótipo melhor em 0, linha de base em 0, empate em 150; corretas: 100,0% × 100,0% | Aprovado |

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Qual a capital da Argentina? | A capital da Argentina é Buenos Aires. | llm | A capital da Argentina é Buenos Aires. |
| 2 | Qual é a capital da Alemanha? | A capital da Alemanha é Berlim. | template | A capital da Alemanha é Berlim. |
| 3 | Que língua se fala no México? | No México fala-se espanhol. | llm | No México fala-se espanhol. |
| 4 | Qual é o símbolo químico do oxigênio? | O símbolo químico do oxigênio é O. | llm | O símbolo químico do oxigênio é O. |
| 5 | Me diga a capital da Polônia. | A capital da Polônia é Varsóvia. | template | A capital da Polônia é Varsóvia. |
| 6 | Em que continente fica a Colômbia? | A Colômbia fica na América do Sul. | llm | A Colômbia fica na América do Sul. |
| 7 | Qual o símbolo químico da prata? | O símbolo químico da prata é Ag. | template | O símbolo químico da prata é Ag. |
| 8 | Qual é a capital do Vietnã? | A capital do Vietnã é Hanói. | template | A capital do Vietnã é Hanói. |
| 9 | Qual a moeda da Nigéria? | A moeda da Nigéria é a naira. | llm | A moeda da Nigéria é a naira. |
| 10 | Qual o símbolo químico do ouro? | O símbolo químico do ouro é Au. | template | O símbolo químico do ouro é Au. |
| 11 | Qual é a capital da Itália? | A capital da Itália é Roma. | template | A capital da Itália é Roma. |
| 12 | Qual é o número atômico do enxofre? | O número atômico do enxofre é 16. | llm | O número atômico do enxofre é 16. |
| 13 | Qual é o símbolo químico do hidrogênio? | O símbolo químico do hidrogênio é H. | template | O símbolo químico do hidrogênio é H. |
| 14 | Em que continente fica a China? | A China fica na Ásia. | template | A China fica na Ásia. |
| 15 | Qual o símbolo químico do ouro? | O símbolo químico do ouro é Au. | template | O símbolo químico do ouro é Au. |
| 16 | Qual é a capital do México? | A capital do México é Cidade do México. | template | A capital do México é a Cidade do México. |
| 17 | Que língua se fala na China? | Na China fala-se mandarim. | template | Na China fala-se principalmente o mandarim. |
| 18 | Qual é o número atômico do enxofre? | O número atômico do enxofre é 16. | template | O número atômico do enxofre é 16. |
| 19 | Qual é a capital do Egito? | A capital do Egito é Cairo. | template | A capital do Egito é o Cairo. |
| 20 | Qual é a moeda do Canadá? | A moeda do Canadá é o dólar canadense. | llm | A moeda do Canadá é o dólar canadense. |
