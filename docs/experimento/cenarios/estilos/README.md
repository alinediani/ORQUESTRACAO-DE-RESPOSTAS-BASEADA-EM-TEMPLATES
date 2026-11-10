# Cenário 7 — Respostas em estilo (e a extrapolação para imagens)

**Motivação.** Em março de 2025, a onda de imagens "no estilo Studio Ghibli" sobrecarregou o gerador de imagens do
ChatGPT, e a OpenAI limitou temporariamente o uso ("our GPUs are melting", Sam Altman, 27/03/2025). Era uma demanda
repetida, sempre o mesmo estilo sobre assuntos diferentes, que o modelo grande atendia do zero a cada pedido. A pergunta deste
cenário é se o orquestrador pode aliviar esse tipo de demanda. Ele é testado com **estilos de texto**, que dá para medir
aqui, e o resultado é extrapolado para imagens.

A analogia:

| Imagem (a proposta) | Texto em estilo (o experimento) |
|---|---|
| Fundo no estilo, reaproveitável | Camada de estilo ("Arr, marujo!…") |
| A pessoa da foto, a parte que muda | O núcleo factual ("a capital da França é Paris") |
| Paleta e traço fixos, para evitar artefatos | O dado vem da fonte, para evitar alucinação |
| Um modelo pequeno estiliza só a pessoa | Um modelo pequeno (Haiku) aplica o estilo sobre o núcleo |

## 7a — A demanda real por estilos (WildChat)

**Base:** WildChat-1M (Zhao et al., 2024), [allenai/WildChat-1M](https://huggingface.co/datasets/allenai/WildChat-1M),
com conversas reais de usuários com o ChatGPT, sob a licença **ODC-BY**. Os arquivos guardam só o primeiro pedido de
cada conversa, cortado em 600 caracteres:

- `wildchat-pt.jsonl`: as 2.300 primeiras conversas em português da base (abril e maio de 2023). A API de filtro do
  Hugging Face ficou lenta demais para baixar as 9.835;
- `wildchat-en-amostra.jsonl`: 1.219 conversas em inglês, de 100 blocos espalhados na base (abril a junho de 2023).

O resultado completo está em [analise.md](analise.md), gerado por `--cenario estilos --analisar`:

| Medida | Português | Inglês |
|---|---:|---:|
| Pedidos com marcador de estilo ("no estilo de", "como um", "in the style of"…) | 1,3% | 3,2% |
| Estilos distintos entre esses pedidos | 26 de 30 | 24 de 39 |
| Pedidos idênticos a outro (pelos primeiros 200 caracteres) | **12,6%** | **15,8%** |

**Leitura.** Pedidos explícitos de estilo são raros e dispersos, e não houve onda nesse período. A onda Ghibli foi em
2025 e era de imagens; o WildChat, de 2023, não a captura. O que mais se repete são **pedidos idênticos**: saudações,
a mesma pessoa refazendo o pedido ("Descreva o projeto da RRT…", 20 vezes) e estruturas como "Componha um micro conto
sobre {tema}". Isso é terreno do cache de respostas, e não do template. A premissa da "onda de estilo" fica sustentada
pelo relato público da onda Ghibli, e não por estes dados.

## 7b — O experimento com 5 estilos

**Conjunto** (`conjunto.json`, por `--cenario estilos --preparar`): 10 fatos da fonte padrão (5 capitais e 5 símbolos
químicos), cada um pedido em 5 estilos: pirata, cordel, para criança de 5 anos, cena de novela e teleprompter de telejornal.
São 50 perguntas, como "Responda como um pirata: Qual é a capital do Peru?".

**Estratégias** (o Claude Opus como modelo grande, via MCP; o Claude Haiku via subagentes do Claude Code):

1. **Linha de base:** o modelo grande responde cada pergunta, com o estilo.
2. **Template inteiro:** o protótipo como era. A chave inclui o estilo, e o template congela a primeira resposta estilizada.
3. **Estilizador:** o núcleo factual sai do template com dado da fonte, e o Haiku aplica o estilo no fim
   (`estilizadas-haiku.tsv`).
4. **Haiku sozinho:** o modelo pequeno responde tudo, sem template e sem fonte (`respostas-haiku-sozinho.tsv`).

A trava (o validador Haiku) foi testada nas estratégias 2 e 3 (`validacao-haiku.tsv`).

| Estratégia | Chamadas ao modelo grande | Custo em relação à linha de base | Dado pedido correto | Afirmações falsas congeladas | Variedade* |
|---|---:|---:|---:|---:|---:|
| 1. Linha de base (Opus) | 50 | — | 50 | 0 | 5,0 |
| 2. Template inteiro | 10 | −79% | 50 | **12** | 3,0 |
| 2 + trava | 46 | −8% | 50 | 0 | 4,9 |
| **3. Estilizador** | **2** (+ 50 Haiku) | **−96%** | 50 | **0** | 4,6 |
| **3 + trava** | 2 (+ 50 Haiku + 2 validações) | −96% | 50 | 0 | 4,6 |
| 4. Haiku sozinho | 0 (50 Haiku) | −97,5% | 50 | — | 4,7 |

\* Variedade: média de redações distintas em cada grupo de 5 respostas do mesmo estilo e família (o máximo é 5).

**O template inteiro mente com o dado certo.** Em 12 das 40 respostas montadas pelo template, a frase em volta do dado
afirma algo falso que veio da primeira resposta:

- "O símbolo químico do ouro é Au, **do latim natrium**";
- "A capital do Japão é Tóquio, **na província de Ontário**";
- "Sabe o Peru…? A capital se chama Lima, **e tem uma torre gigante de ferro**".

Outras 7 têm um detalhe inadequado ("terra de queijo fino e vinho" para o Japão). A correção automática não vê nada disso,
porque o dado pedido está certo. A trava recusou 9 dos 10 templates em estilo e eliminou o problema, mas devolveu quase
tudo ao modelo grande.

**O estilizador separa as camadas e resolve as duas coisas.** O modelo grande respondeu só 2 núcleos factuais (um por
família). O template montou os outros 48 com o dado da fonte, e o Haiku aplicou o estilo sem acrescentar fatos. O
resultado foi 96% mais barato, sem nenhuma afirmação falsa e com variedade perto da do modelo grande. O validador aprovou
os dois templates do núcleo, então a trava não custou nada aqui.

**O Haiku sozinho foi ainda mais barato e acertou tudo.** São fatos muito conhecidos (capitais, símbolos). O valor da fonte
aparece quando o dado é pouco conhecido ou muda com o tempo: no quiz, o modelo grande errou ou não sabia dados que a fonte
tinha. O estilizador tem o custo próximo ao do modelo pequeno sozinho e mantém a garantia de que o dado vem da fonte.

## Extrapolação para imagens

A mesma separação se aplica à geração de imagens no estilo de uma onda:

1. **Camada fixa (o "template"):** o fundo e a base de estilo (paleta, traço) de uma cena, extraídos das primeiras
   gerações do modelo grande para aquele estilo e tipo de cena, e validados antes de serem reaproveitados (a trava).
2. **Camada variável (o "dado"):** a pessoa ou o objeto da foto do usuário, recortado por um passo determinístico.
3. **Estilização barata:** um modelo pequeno, especializado no estilo, transforma só a camada variável, que é depois
   composta sobre o fundo.

Os resultados de texto indicam o que esperar:

- **Congelar a cena inteira repete detalhes de uma imagem em outras**, como o "latim natrium": um objeto de fundo que só
  fazia sentido na primeira foto.
- **Separar as camadas e estilizar só o que muda** preserva a economia, a variedade e a fidelidade ao que vem do usuário.

Os limites próprios da imagem, que o texto não testa:

- **A coerência de luz, sombra e perspectiva** entre a pessoa e o fundo;
- **A perda da cena própria do usuário:** na onda Ghibli, parte do encanto era ver a própria cozinha ou a própria praia no
  estilo.

> **Viés e limitações:**
> - o Claude escreveu as respostas da linha de base e contou as afirmações falsas;
> - o Haiku rodou via subagentes, com várias entradas no mesmo pedido;
> - o conjunto tem fatos muito conhecidos;
> - a qualidade do estilo, como a rima do cordel e o tom do telejornal, fica para a avaliação humana, nas planilhas
>   `avaliacao-humana*.csv`.
