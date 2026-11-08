# Rodadas manuais pelo painel (Playwright)

Quatro rodadas de 15 perguntas (60 no total) feitas **pela interface**, como uma pessoa faria. O script [`rodadas.js`](rodadas.js) abriu o painel do orquestrador no navegador (Playwright), escolheu o motor **MCP · Claude Code**, digitou cada pergunta e deu 👍/👎. As perguntas de estrutura nova esperaram o Claude Code responder pelo `/mcp`, como LLM de geração. Tudo ficou gravado na base do app, [`dados/orquestrador.db`](../../../dados/orquestrador.db).

- **Perguntas:** sorteadas (semente fixa) nas seis famílias da base de fatos, mais uma pergunta fora da base por rodada.
- **Critério do avaliador:** 👍 quando o dado confere com a base de fatos **e** o artigo da moeda está certo; 👎 caso contrário. Perguntas fora da base ficaram sem avaliação.
- **Base de partida:** a do app, com três templates de uma sessão anterior (capital, moeda e símbolo químico).

## Resultado

| Rodada | Pelo template | Ao LLM | 👎 |
|---|---:|---:|---:|
| 1 | 11 | 4 (3 famílias novas + 1 fora da base) | 1 |
| 2 | 14 | 1 (fora da base) | 0 |
| 3 | 14 | 1 (fora da base) | 1 |
| 4 | 14 | 1 (fora da base) | 0 |
| **Total** | **53 (88%)** | **7** | **2** |

Depois da primeira rodada, só as perguntas fora da base foram ao LLM: "Qual é a capital do Acre?", "Qual a moeda de Wakanda?" e "Quem pintou a Mona Lisa?". Essas não viram template, porque a entidade não está na base de fatos.

## O que o feedback mostrou

- **Moeda:** o template ficou "A moeda {de_entidade} é **o** {moeda}." Ele errou "o rupia indiana" e "o libra esterlina", que receberam 👎. Mesmo assim, o peso só caiu para 0,84, porque a maioria das moedas é masculina e recebe 👍, e o template não chegou a ser descartado. O mecanismo de peso, sozinho, não corrige o artigo.
- **Continente:** o template veio de "Portugal fica na Europa." (sem artigo) e gera "Rússia fica na Europa." Como o critério do avaliador não olhava o artigo da entidade, esse erro passou.

Os dois casos são do mesmo tipo dos encontrados no experimento real ([`analise-mcp.md`](../analise-mcp.md)): o template fixa as palavras que dependem do dado.

## Arquivos

- `rodadas.jsonl`: uma linha por pergunta, com a resposta, o caminho, a chave, o template, o peso, a avaliação e o feedback.
- `rodada1.png` … `rodada4.png`: o painel ao fim de cada rodada.
- `rodadas.js`: o script. Para rodar de novo: `node rodadas.js <repo> 4 15 rodadas.jsonl`, com o app no ar e o Claude Code atendendo o `/mcp`.
