# Resultados do experimento — cenário gsm8k

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "gsm8k"}` · 30 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 30 | 30 | 0,0% |
| Chamadas ao LLM do parser | 0 | — | |
| Taxa de acerto de template | 0,0% (0) | — | |
| Tokens | 3.586 | 3.586 | 0,0% |
| Custo estimado | US$ 0,02 | US$ 0,02 | 0,0% |
| Respostas corretas (gabarito) | 100,0% (30) | 100,0% (30) | |

**Comparação automática pelo gabarito (a cega humana está em avaliacao-humana.csv):** protótipo melhor em 0, linha de base melhor em 0, empate em 30 de 30.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| gsm8k | 30 | 30 | 0 (0,0%) |

Templates salvos na base chave-valor: 0.

## As primeiras perguntas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Se Ann tem 9 anos e seu irmão tem o dobro da idade dela, quantos anos terá o irmão dela em 3 anos? | Em 3 anos, o irmão de Ann terá 21 anos. | llm | Em 3 anos, o irmão de Ann terá 21 anos. |
| 2 | Maddy está comprando pizza para o jogo de futebol do primo dela. Há 12 membros na equipe e 3 treinadores. Cada membro da equipe traz 2 convidados. Uma pizza serve 3 pessoas. Se cada pizza custa $15, quantos dólares Maddy vai gastar? | Maddy vai gastar 195 dólares em pizza. | llm | Maddy vai gastar 195 dólares em pizza. |
| 3 | Gretchen tem 110 moedas. Há 30 moedas de ouro a mais do que moedas de prata. Quantas moedas de ouro Gretchen tem? | Gretchen tem 70 moedas de ouro. | llm | Gretchen tem 70 moedas de ouro. |
| 4 | Há 50 livros em uma pequena biblioteca. Metade deles são escritos em inglês e 10% em alemão. Todos os outros são escritos em espanhol. Quantos livros em espanhol existem? | Existem 20 livros em espanhol na biblioteca. | llm | Existem 20 livros em espanhol na biblioteca. |
| 5 | Jeff possui uma empresa de buffet. Durante um evento recente, ele enviou 8 dezenas de copos e 4 dezenas de pratos para a festa. Quando foram devolvidos, 10 copos estavam quebrados, assim como 6 pratos. Quantos copos e pratos Jeff tem agora? | Jeff tem agora 128 copos e pratos. | llm | Jeff tem agora 128 copos e pratos. |
| 6 | A família Smith está se preparando para o verão e precisa encher sua piscina. A empresa de piscinas instruiu-os a medir para encontrar o volume da piscina e, em seguida, multiplicar por 5,9 para calcular quantos galões de água eles precisam. O custo para a empresa vir e encher a piscina é de US$ 0,10 por galão. O Sr. Smith mediu e descobriu que a piscina tem 14 pés de largura, 25 pés de comprimento e 4 pés de profundidade. Quanto custará para encher a piscina? | Encher a piscina custará US$ 826. | llm | Encher a piscina custará US$ 826. |
| 7 | A sacola de doces de Halloween do Joe tem 25 barras de chocolate e 80 maçãs caramelizadas. Cada barra de chocolate pesa o dobro de cada maçã caramelizada. Se cada barra de chocolate pesa 40g, quanto pesa a sacola de doces do Joe, em gramas? | A sacola de doces do Joe pesa 2600 gramas. | llm | A sacola de doces do Joe pesa 2600 gramas. |
| 8 | Lisa e Peter estão vendendo barras de chocolate de porta em porta. Lisa vendeu três caixas e meia de barras de chocolate, e Peter vendeu quatro caixas e meia. Eles venderam 64 barras de chocolate juntos. Quantas barras de chocolate há em uma caixa? | Há 8 barras de chocolate em uma caixa. | llm | Há 8 barras de chocolate em uma caixa. |
| 9 | Rose comprou cinco dúzias de ovos por $2,40 a dúzia. Ela vai vendê-los a $1 por 3 ovos. Qual será o lucro de Rose? | O lucro de Rose será de 8 dólares. | llm | O lucro de Rose será de 8 dólares. |
| 10 | Jo tem feito máscaras faciais. Ela pode fazer 4 máscaras pequenas com 2 jardas de material e 3 máscaras grandes com 2,25 jardas de material. Quanto material ela precisaria para fazer 20 máscaras pequenas e 8 grandes para sua turma? | Jo precisaria de 16 jardas de material. | llm | Jo precisaria de 16 jardas de material. |
| 11 | Carly tinha 80 cartas, 2/5 das cartas tinham a letra A, 1/2 das restantes tinham a letra B, 5/8 do restante tinham a letra C, e as outras tinham a letra D. Quantas cartas tinham a letra D? | 9 cartas tinham a letra D. | llm | 9 cartas tinham a letra D. |
| 12 | Maria compra 8 ações de uma ação por $8 cada. O preço da ação aumenta 50% no primeiro ano em que Maria a possui, depois diminui 25% no segundo ano. Qual é o valor final de todas as ações de Maria? | O valor final das ações de Maria é de 72 dólares. | llm | O valor final das ações de Maria é de 72 dólares. |
| 13 | Dominick foi ao vestiário da sua equipe e viu metade do número de robôs em relação aos capacetes e metade do número de capacetes em relação às bolas de futebol guardadas lá. Se havia 20 capacetes, calcule o número total de itens que Dominick viu. | Dominick viu 70 itens no total. | llm | Dominick viu 70 itens no total. |
| 14 | Eu tenho 10 litros de suco de laranja que são dois terços de água e desejo adicioná-lo a 15 litros de suco de abacaxi que é três quintos de água. Mas ao despejar, derramo um litro do suco de laranja. Quanto de água há nos 24 litros restantes? | Há 15 litros de água nos 24 litros restantes. | llm | Há 15 litros de água nos 24 litros restantes. |
| 15 | Sunny está vendendo bolo de gengibre e torta de maçã para uma arrecadação de fundos. No sábado, ele vendeu 10 caixas de bolo de gengibre e 4 caixas a menos de torta de maçã do que no domingo. No domingo, ele vendeu 5 caixas a mais de bolo de gengibre do que no sábado e 15 caixas de torta de maçã. Se o bolo de gengibre custa $6 e a torta de maçã custa $15, quanto Sunny ganhou em dois dias? | Sunny ganhou 540 dólares nos dois dias. | llm | Sunny ganhou 540 dólares nos dois dias. |
| 16 | Henry fez duas paradas durante sua viagem de bicicleta de 60 milhas. Ele parou primeiro após 20 milhas. Sua segunda parada foi 15 milhas antes do fim da viagem. Quantas milhas ele percorreu entre sua primeira e segunda paradas? | Henry percorreu 25 milhas entre a primeira e a segunda parada. | llm | Henry percorreu 25 milhas entre a primeira e a segunda parada. |
| 17 | Quatro estudantes marcaram um total de 251 pontos em um jogo de tabuleiro. Naomi marcou 68 dos pontos. Yuri marcou 10 pontos a mais que metade dos pontos de Naomi e Brianna marcou 17 pontos a mais que Naomi. Quantos pontos Jojo marcou? | Jojo marcou 54 pontos. | llm | Jojo marcou 54 pontos. |
| 18 | Helena é uma cientista louca que está testando sua última criação, o Soro de Muitos Membros. O soro faz com que quem o bebe cresça um braço extra a cada três dias e uma perna extra a cada cinco dias. Após quinze dias, quantos membros novos o soro de Helena fará uma pessoa crescer se ela beber? | O soro de Helena fará a pessoa crescer 8 membros novos em quinze dias. | llm | O soro de Helena fará a pessoa crescer 8 membros novos em quinze dias. |
| 19 | Martha tem colecionado conchas desde que completou 5 anos de idade, a cada mês ela coleta uma concha. Até seu 10º aniversário, quantas conchas Martha terá colecionado? | Até seu 10º aniversário, Martha terá colecionado 60 conchas. | llm | Até seu 10º aniversário, Martha terá colecionado 60 conchas. |
| 20 | Chatty preparou três dezenas de ovos para a atividade de Páscoa de seus quatro filhos. Supondo que cada criança receba o mesmo número de ovos, quantos ovos cada criança recebe? | Cada criança recebe 9 ovos. | llm | Cada criança recebe 9 ovos. |
